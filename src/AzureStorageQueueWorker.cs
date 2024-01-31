using System;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Azure;
using Azure.Storage.Queues;
using Azure.Storage.Queues.Models;
using Microsoft.ApplicationInsights;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Likvido.Worker.AzureStorageQueue
{
    internal class AzureStorageQueueWorker : BackgroundService
    {
        private readonly ILogger _logger;
        private readonly AzureStorageQueueWorkerOptions _workerOptions;
        private readonly ExceptionHandler _exceptionHandler;
        private readonly TelemetryClient _telemetryClient;
        private readonly IServiceProvider _serviceProvider;

        public AzureStorageQueueWorker(IServiceProvider serviceProvider, AzureStorageQueueWorkerOptions workerOptions)
        {
            _serviceProvider = serviceProvider;
            _workerOptions = workerOptions;

            _logger = serviceProvider.GetRequiredService<ILogger<AzureStorageQueueWorker>>();
            _exceptionHandler = new ExceptionHandler(_logger, workerOptions, serviceProvider, serviceProvider.GetRequiredService<IHostApplicationLifetime>());
            _telemetryClient = serviceProvider.GetRequiredService<TelemetryClient>();
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            var processorName = Assembly.GetEntryAssembly()?.GetName().Name;
            using var logScope = _logger.BeginScope("{Processor} reads {queueName}", processorName, _workerOptions.QueueName);
            try
            {
                var queueClient = new QueueClient(_workerOptions.AzureStorageConnectionString, _workerOptions.QueueName);
                await queueClient.CreateIfNotExistsAsync(cancellationToken: CancellationToken.None);
                using var processor = new QueueMessageProcessor(
                            _logger,
                            queueClient,
                            _serviceProvider,
                            _exceptionHandler,
                            _workerOptions,
                            _telemetryClient);

                while (!stoppingToken.IsCancellationRequested)
                {
                    try
                    {
                        Response<QueueMessage[]>? queueMessageResponse = await queueClient
                            .ReceiveMessagesAsync(1, _workerOptions.VisibilityTimeout, stoppingToken);

                        var queueMessage = queueMessageResponse?.Value.FirstOrDefault();

                        if (queueMessage == null)
                        {
                            _logger.LogInformation("No messages, sleeping...");
                            await Task.Delay(GetSleepDuration(), stoppingToken);
                            continue;
                        }

                        await processor.ProcessMessage(queueMessage, stoppingToken);

                        if (_workerOptions.SleepBetweenEachMessage.HasValue)
                        {
                            _logger.LogInformation("Sleep between each message is enabled. Sleeping for {SleepBetweenEachMessage}", _workerOptions.SleepBetweenEachMessage.Value);
                            await Task.Delay(_workerOptions.SleepBetweenEachMessage.Value, stoppingToken);
                        }
                    }
                    catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                    {
                        // If stoppingToken is cancelled, an OperationCanceledException is expected, so just break
                        break;
                    }
                    catch (Exception ex)
                    {
                        _logger.UnhandledMessageProcessingExceptionOccurred(ex);
                    }
                }

                _telemetryClient.Flush();
                if (_workerOptions.FlushTimeout.HasValue)
                {
                    // https://github.com/microsoft/ApplicationInsights-dotnet/issues/407
                    await Task.Delay(_workerOptions.FlushTimeout.Value, CancellationToken.None);
                }
            }
            catch (Exception ex)
            {
                _logger.SetupExceptionOccurred(ex);
                await _exceptionHandler.HandleUnhandledExceptionAsync(null, ex, _workerOptions.SetupIssueStopHostCode);
            }
        }

        private TimeSpan GetSleepDuration()
        {
            return _workerOptions.NoMessagesSleepDuration ?? TimeSpan.FromSeconds(new Random().Next(5, 40));
        }
    }
}
