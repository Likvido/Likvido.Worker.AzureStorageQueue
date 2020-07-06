using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Azure.Storage.Queues;
using Azure.Storage.Queues.Models;
using Microsoft.ApplicationInsights;
using Microsoft.ApplicationInsights.DataContracts;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Polly;

namespace Likvido.Worker.AzureStorageQueue
{
    public class AzureStorageQueueWorker<TMessage, TMessageProcessor> : BackgroundService
        where TMessageProcessor : IMessageProcessor<TMessage>
    {
        private readonly ILogger _logger;
        private readonly AzureStorageQueueWorkerOptions<TMessageProcessor> _workerOptions;
        private readonly IServiceProvider _serviceProvider;
        private readonly IHostApplicationLifetime _hostApplicationLifetime;
        private readonly AsyncPolicy _deleteMessagePolcy;
        private readonly AsyncPolicy _updateMessagePolcy;
        private readonly SemaphoreSlim _messageReceiptSemaphore = new SemaphoreSlim(1, 1);
        private QueueClient _posionQueueClient = null;
        private readonly TelemetryClient _telemetryClient;

        private QueueClient PosionQueueClient
        {
            get
            {
                if (_posionQueueClient == null)
                {
                    _posionQueueClient = new QueueClient(_workerOptions.AzureStorageConnectionString, _workerOptions.PoisonQueueName);
                }

                return _posionQueueClient;
            }
        }

        public AzureStorageQueueWorker(
            ILogger<AzureStorageQueueWorker<TMessage, TMessageProcessor>> logger,
            IServiceProvider serviceProvider,
            IHostApplicationLifetime hostApplicationLifetime,
            AzureStorageQueueWorkerOptions<TMessageProcessor> workerOptions,
            TelemetryClient telemetryClient)
        {
            _logger = logger;
            _workerOptions = workerOptions;
            _serviceProvider = serviceProvider;
            _hostApplicationLifetime = hostApplicationLifetime;
            _deleteMessagePolcy = GetDeleteMessagePolcy("Message deletion from a queue \"{queueName}\" failed #{retryAttempt}");
            _updateMessagePolcy = GetDeleteMessagePolcy("Message update in a queue \"{queueName}\" failed #{retryAttempt}");
            _telemetryClient = telemetryClient;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            var processorName = typeof(TMessageProcessor).FullName;
            using var logScope = _logger.BeginScope("{Processor} reads {queueName}", processorName, _workerOptions.QueueName);
            try
            {
                var queueClient = new QueueClient(_workerOptions.AzureStorageConnectionString, _workerOptions.QueueName);
                await queueClient.CreateIfNotExistsAsync();
                while (!stoppingToken.IsCancellationRequested)
                {
                    var processed = false;
                    MessageDetails messageDetails = null;
                    QueueMessage queueMessage = null;
                    CancellationTokenSource updateMessageTokenSource = null;
                    Task updateQueueMessageTask = null;
                    IServiceScope scope = null;
                    try
                    {
                        var queueMessageResponse = await queueClient.ReceiveMessagesAsync(1, _workerOptions.VisibilityTimeout, stoppingToken);
                        queueMessage = queueMessageResponse.Value.FirstOrDefault();

                        if (queueMessage == null)
                        {
                            _logger.LogInformation("No messages, sleeping...");
                            await Task.Delay(_workerOptions.NoMessagesSleepDuration, stoppingToken);
                            continue;
                        }

                        //TODO: we need more metric. This is just very early version to check. How it'll work
                        using var tc = _telemetryClient.StartOperation<DependencyTelemetry>($"{processorName} queue: {_workerOptions.QueueName}");
                        messageDetails = new MessageDetails
                        {
                            Message = queueMessage,
                            Receipt = queueMessage.PopReceipt
                        };

                        updateMessageTokenSource = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
                        var visibilityToken = updateMessageTokenSource.Token;
                        updateQueueMessageTask = Task.Run(() =>
                            KeepMessageInvisibleAsync(queueClient, messageDetails, visibilityToken), visibilityToken);

                        scope = _serviceProvider.CreateScope();
                        var messageProcessor = scope.ServiceProvider.GetRequiredService<TMessageProcessor>();
                        await messageProcessor.ProcessMessage(GetTypedMessage(queueMessage), stoppingToken);
                        processed = true;

                        //stoppingToken aren't passed here. We should try to delete message even if cancellation was requested
                        //if operation was done
                        await DeleteMessageAsync(queueClient, messageDetails, updateMessageTokenSource);

                    }
                    catch (OperationCanceledException)
                    {
                        _logger.OperationCancelledExceptionOccurred();
                    }
                    catch (Exception ex)
                    {
                        if (!processed && queueMessage != null)
                        {
                            await TryMoveToPoisonAsync(queueClient, messageDetails, updateMessageTokenSource);
                        }
                        await HandleUnhandledExceptionAsync(scope, ex, _workerOptions.ProcessingIssueStopHostCode);
                    }
                    finally
                    {
                        updateMessageTokenSource?.Cancel(); //the second cancellation won't make things worse but useful in case of errors
                        if (updateQueueMessageTask != null)
                        {
                            await updateQueueMessageTask;
                        }
                        scope?.Dispose();
                        scope = null;
                    }
                }
            }
            catch (Exception ex)
            {
                await HandleUnhandledExceptionAsync(null, ex, _workerOptions.SetupIssueStopHostCode);
            }
        }

        private async Task DeleteMessageAsync(QueueClient queueClient, MessageDetails messageDetails, CancellationTokenSource updateMessageTokenSource)
        {
            //stoppingToken aren't passed here. We should try to delete message even if cancellation was requested
            //if operation was done
            await ModifyMessageAsync(async () =>
            {
                updateMessageTokenSource.Cancel(); //stops update visibility attempts
                await _deleteMessagePolcy
                    .ExecuteAsync(() =>
                        queueClient.DeleteMessageAsync(messageDetails.Message.MessageId, messageDetails.Receipt));
            });
        }

        private async Task TryMoveToPoisonAsync(
            QueueClient queueClient,
            MessageDetails messageDetails,
            CancellationTokenSource updateMessageTokenSource)
        {
            if (messageDetails.Message.DequeueCount >= _workerOptions.MaxRetryCount)
            {
                try
                {
                    await PosionQueueClient.AddMessageAndCreateIfNotExistsAsync(messageDetails.Message.MessageText);
                    await DeleteMessageAsync(queueClient, messageDetails, updateMessageTokenSource);
                }
                catch (Exception e)
                {
                    _logger.LogError(e, "TryMoveToPoisonAsync failed poison queue{poison_queue}", _workerOptions.PoisonQueueName);
                }
            }
        }


        /// <summary>
        /// Makes logging
        /// Executes UnhandledExceptionHandler if any
        /// Stops Host based on exitCode passed. If it's null no stop
        /// </summary>
        /// <param name="scope"></param>
        /// <param name="exception"></param>
        /// <param name="exitCode"></param>
        private async Task HandleUnhandledExceptionAsync(IServiceScope scope, Exception exception, int? exitCode)
        {
            IServiceScope localScope = null;
            try
            {
                var handler = _workerOptions.UnhandledExceptionHandler;
                if (handler != null)
                {
                    if (scope == null)
                    {
                        scope = localScope = _serviceProvider.CreateScope();
                    }
                    await handler.Invoke(scope.ServiceProvider, exception);
                }
            }
            catch (Exception e)
            {
                _logger.LogError(e, "An unhandled exception was thrown.");
            }
            finally
            {
                localScope?.Dispose();
            }

            _logger.LogError(exception, "An unhandled exception was thrown.");

            if (exitCode.HasValue)
            {
                Environment.ExitCode = exitCode.Value;
                _hostApplicationLifetime.StopApplication();
            }
        }

        private TMessage GetTypedMessage(QueueMessage message)
        {
            if (typeof(TMessage) == typeof(string))
            {
                return (TMessage)Convert.ChangeType(message.GetDecodedMessageText(), typeof(TMessage));
            }

            if (typeof(TMessage) == typeof(QueueMessage))
            {
                return (TMessage)Convert.ChangeType(message, typeof(TMessage));
            }

            return System.Text.Json.JsonSerializer.Deserialize<TMessage>(
                message.GetDecodedMessageText(),
                new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true,
                    AllowTrailingCommas = true,
                    ReadCommentHandling = JsonCommentHandling.Skip
                });
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
                        var result = await _updateMessagePolcy.ExecuteAsync(() =>
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

        private AsyncPolicy GetDeleteMessagePolcy(string failureText)
        {
            var attemptsCount = 3;
            //delay short be short otherwise we will block other messages to be processed
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

        private class MessageDetails
        {
            public QueueMessage Message { get; set; }
            public string Receipt { get; set; }
        }
    }
}
