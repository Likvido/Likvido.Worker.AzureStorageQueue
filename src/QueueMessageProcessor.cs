using System;
using System.Globalization;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Azure.Storage.Queues;
using Azure.Storage.Queues.Models;
using Microsoft.ApplicationInsights;
using Microsoft.ApplicationInsights.DataContracts;
using Microsoft.ApplicationInsights.Extensibility;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Polly;

namespace Likvido.Worker.AzureStorageQueue
{
    public sealed class QueueMessageProcessor<TMessage, TMessageProcessor> : IDisposable
        where TMessageProcessor : IMessageProcessor<TMessage>
    {
        private readonly ILogger _logger;
        private readonly AzureStorageQueueWorkerOptions _workerOptions;
        private readonly IServiceProvider _serviceProvider;
        private readonly ExceptionHandler _exceptionHandler;
        private readonly AsyncPolicy _deleteMessagePolicy;
        private readonly AsyncPolicy _updateMessagePolicy;
        private readonly SemaphoreSlim _messageReceiptSemaphore = new SemaphoreSlim(1, 1);
        private QueueClient? _poisonQueueClient;
        private readonly TelemetryClient _telemetryClient;
        private readonly QueueClient _queueClient;

        private QueueClient PoisonQueueClient
        {
            get
            {
                if (_poisonQueueClient == null)
                {
                    _poisonQueueClient = new QueueClient(_workerOptions.AzureStorageConnectionString, _workerOptions.PoisonQueueName);
                }

                return _poisonQueueClient;
            }
        }

        public QueueMessageProcessor(
            ILogger logger,
            QueueClient queueClient,
            IServiceProvider serviceProvider,
            ExceptionHandler exceptionHandler,
            AzureStorageQueueWorkerOptions<TMessageProcessor> workerOptions,
            TelemetryClient telemetryClient)
        {
            _logger = logger;
            _queueClient = queueClient;
            _workerOptions = workerOptions;
            _serviceProvider = serviceProvider;
            _exceptionHandler = exceptionHandler;
            _deleteMessagePolicy = GetDeleteMessagePolicy("Message deletion from a queue \"{queueName}\" failed #{retryAttempt}");
            _updateMessagePolicy = GetDeleteMessagePolicy("Message update in a queue \"{queueName}\" failed #{retryAttempt}");
            _telemetryClient = telemetryClient;
        }

        public async Task ProcessMessage(QueueMessage queueMessage, CancellationToken stoppingToken)
        {
            if (queueMessage == null)
            {
                throw new ArgumentNullException(nameof(queueMessage));
            }

            //Running this as separate task gives the following benefits
            //1. ExecutionContext isn't overlap between message processors
            //2. Makes message parallel processing easier
            await Task.Yield();//makes execution parallel immidiatly for the reasons above

            var processed = false;
            MessageDetails? messageDetails = null;
            IServiceScope? scope = null;
            IAsyncDisposable? updateVisibilityStopAction = null;
            IOperationHolder<RequestTelemetry>? operation = null;
            try
            {
                messageDetails = new MessageDetails(queueMessage);

                operation = _telemetryClient.StartOperation<RequestTelemetry>(_workerOptions.OperationName);
                operation.Telemetry.Properties["InvocationId"] = queueMessage.MessageId;
                operation.Telemetry.Properties["MessageId"] = queueMessage.MessageId;
                operation.Telemetry.Properties["OperationName"] = _workerOptions.OperationName;
                operation.Telemetry.Properties["TriggerReason"] = $"New queue message detected on '{_workerOptions.QueueName}'.";
                operation.Telemetry.Properties["QueueName"] = _workerOptions.QueueName;
                operation.Telemetry.Properties["Processor"] = typeof(TMessageProcessor).Name;

                updateVisibilityStopAction = await StartKeepMessageInvisibleAsync(_queueClient, messageDetails);

                scope = _serviceProvider.CreateScope();
                var messageProcessor = scope.ServiceProvider.GetRequiredService<TMessageProcessor>();
                var lastAttempt = messageDetails.Message.DequeueCount >= _workerOptions.MaxRetryCount;
                await messageProcessor.ProcessMessage(GetTypedMessage(queueMessage), lastAttempt, stoppingToken);
                processed = true;
                _telemetryClient.TrackTrace("Processor finished"); //short cut for now. TOOD:// wrap processing in a separate operation

                //stoppingToken aren't passed here. We should try to delete message even if cancellation was requested
                //if operation was done
                await DeleteMessageAsync(_queueClient, messageDetails, updateVisibilityStopAction);
                operation.Telemetry.Success = true;
            }
            catch (OperationCanceledException)
            {
                _logger.OperationCancelledExceptionOccurred();
                if (operation != null)
                {
                    operation.Telemetry.Success = false;
                }
            }
            catch (Exception ex)
            {
                if (!processed && messageDetails != null)
                {
                    await TryMoveToPoisonAsync(_queueClient, messageDetails, updateVisibilityStopAction);
                }

                _logger.UnhandledMessageProcessingExceptionOccurred(ex);
                await _exceptionHandler.HandleUnhandledExceptionAsync(scope, ex, _workerOptions.ProcessingIssueStopHostCode);
                if (operation != null)
                {
                    operation.Telemetry.Success = false;
                }
            }
            finally
            {
                if (updateVisibilityStopAction != null)
                {
                    await updateVisibilityStopAction.DisposeAsync();
                }
                scope?.Dispose();
                operation?.Dispose();
            }
        }

        private async Task DeleteMessageAsync(
            QueueClient queueClient,
            MessageDetails messageDetails,
            IAsyncDisposable? updateVisibilityStopAction)
        {
            await ModifyMessageAsync(async () =>
            {
                if (updateVisibilityStopAction != null)
                {
                    await updateVisibilityStopAction.DisposeAsync();
                }
                await _deleteMessagePolicy
                    .ExecuteAsync(() =>
                        queueClient.DeleteMessageAsync(messageDetails.Message.MessageId, messageDetails.Receipt));
            });
        }

        private async Task TryMoveToPoisonAsync(
            QueueClient queueClient,
            MessageDetails messageDetails,
            IAsyncDisposable? updateVisibilityStopAction)
        {
            if (messageDetails.Message.DequeueCount >= _workerOptions.MaxRetryCount)
            {
                try
                {
                    await PoisonQueueClient.AddMessageAndCreateIfNotExistsAsync(messageDetails.Message.MessageText);
                    await DeleteMessageAsync(queueClient, messageDetails, updateVisibilityStopAction);
                }
                catch (Exception e)
                {
                    _logger.LogError(e, "TryMoveToPoisonAsync failed poison queue{poison_queue}", _workerOptions.PoisonQueueName);
                }
            }
        }

        private static TMessage GetTypedMessage(QueueMessage message)
        {
            if (typeof(TMessage) == typeof(string))
            {
                return (TMessage)Convert.ChangeType(message.GetDecodedMessageText(), typeof(TMessage), CultureInfo.InvariantCulture);
            }

            if (typeof(TMessage) == typeof(QueueMessage))
            {
                return (TMessage)Convert.ChangeType(message, typeof(TMessage), CultureInfo.InvariantCulture);
            }

            return JsonSerializer.Deserialize<TMessage>(
                message.GetDecodedMessageText(),
                new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true,
                    AllowTrailingCommas = true,
                    ReadCommentHandling = JsonCommentHandling.Skip
                });
        }

        private Task<IAsyncDisposable> StartKeepMessageInvisibleAsync(
            QueueClient queueClient,
            MessageDetails messageDetails)
        {
#pragma warning disable CA2000 // Dispose objects before losing scope
            var updateMessageTokenSource = new CancellationTokenSource();
#pragma warning restore CA2000 // Dispose objects before losing scope
            var updateQueueMessageTask = KeepMessageInvisibleAsync(queueClient, messageDetails, updateMessageTokenSource.Token);

            var disposeAction = new DisposeAction(async () =>
            {
                updateMessageTokenSource.Cancel();
                updateMessageTokenSource.Dispose();
                await updateQueueMessageTask;
            });

            return Task.FromResult((IAsyncDisposable)disposeAction);
        }

        private async Task KeepMessageInvisibleAsync(
            QueueClient queueClient,
            MessageDetails messageDetails,
            CancellationToken token)
        {
            var timeout = _workerOptions.VisibilityTimeout;
            var sleep = timeout;
            if (sleep.TotalSeconds > 15)
            {
                sleep = sleep.Subtract(TimeSpan.FromSeconds(10));
            }
            else
            {
                sleep = sleep.Divide(2);
            }
            try
            {
                do
                {
                    await Task.Delay(sleep, token);

                    await ModifyMessageAsync(async () =>
                    {
                        var result = await _updateMessagePolicy.ExecuteAsync(() =>
                        queueClient.UpdateMessageAsync(
                            messageDetails.Message.MessageId,
                            messageDetails.Receipt,
                            messageDetails.Message.MessageText,
                            timeout,
                            token));

                        //all further operations should be done with the new receipt otherwise 404
                        messageDetails.Receipt = result.Value.PopReceipt;
                    }, token);
                } while (true);
            }
            catch (OperationCanceledException)
            {
                //skip
            }
            catch (Exception e)
            {
                _logger.LogError(e, "Update message visibility has failed.");
            }
        }

        private AsyncPolicy GetDeleteMessagePolicy(string failureText)
        {
            var attemptsCount = 3;
            //the delay has to be short otherwise we will block other messages from being processed
            Func<int, TimeSpan> defaultWaitCalc = retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt - attemptsCount));
            Action<Exception, int> onRetry = (exception, retryAttempt)
                    => _logger.LogError(
                        exception,
                        failureText, _workerOptions.QueueName, retryAttempt);

            return Policy
                .Handle<Exception>()
                .WaitAndRetryAsync(
                attemptsCount,
                defaultWaitCalc,
                (exception, _, retryAttempt, __) => onRetry.Invoke(exception, retryAttempt));
        }

        /// <summary>
        /// Any operations to a message need to be done via this helper function
        /// Each message update changes a message receipt and causes 404 result with a previous receipt 
        /// </summary>
        /// <param name="callback"></param>
        /// <param name="token"></param>
        /// <returns></returns>
        private async Task ModifyMessageAsync(Func<Task> callback, CancellationToken token = default)
        {
            bool semaphoreCaptured = false;
            try
            {
                await _messageReceiptSemaphore.WaitAsync(token);
                semaphoreCaptured = true;
                await callback();
            }
            finally
            {
                if (semaphoreCaptured)
                {
                    _messageReceiptSemaphore.Release();
                }
            }
        }

        public void Dispose()
        {
            _messageReceiptSemaphore.Dispose();
        }

        private class MessageDetails
        {
            public MessageDetails(QueueMessage message)
            {
                Message = message;
                Receipt = message.PopReceipt;
            }

            public QueueMessage Message { get; }
            public string Receipt { get; set; }
        }
    }
}
