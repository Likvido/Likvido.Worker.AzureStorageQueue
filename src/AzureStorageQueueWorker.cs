using System;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Azure.Storage.Queues;
using Azure.Storage.Queues.Models;
using Microsoft.ApplicationInsights;
using Microsoft.ApplicationInsights.DataContracts;
using Microsoft.ApplicationInsights.Extensibility;
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
        private readonly ExceptionHandler _exceptionHandler;
        private readonly TelemetryClient _telemetryClient;
        private readonly IServiceProvider _serviceProvider;

        public AzureStorageQueueWorker(
            ILogger<AzureStorageQueueWorker<TMessage, TMessageProcessor>> logger,
            IServiceProvider serviceProvider,
            IHostApplicationLifetime hostApplicationLifetime,
            AzureStorageQueueWorkerOptions<TMessageProcessor> workerOptions,
            TelemetryClient telemetryClient)
        {
            _logger = logger;
            _workerOptions = workerOptions;
            _exceptionHandler = new ExceptionHandler(logger, workerOptions, serviceProvider, hostApplicationLifetime);
            _telemetryClient = telemetryClient;
            _serviceProvider = serviceProvider;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            var processorName = typeof(TMessageProcessor).FullName;
            using var logScope = _logger.BeginScope("{Processor} reads {queueName}", processorName, _workerOptions.QueueName);
            try
            {
                var queueClient = new QueueClient(_workerOptions.AzureStorageConnectionString, _workerOptions.QueueName);
                await queueClient.CreateIfNotExistsAsync();
                using var processor = new QueueMessageProcessor<TMessage, TMessageProcessor>(
                            _logger,
                            queueClient,
                            _serviceProvider,
                            _exceptionHandler,
                            _workerOptions,
                            _telemetryClient);

                while (!stoppingToken.IsCancellationRequested)
                {
                    QueueMessage? queueMessage = null;
                    try
                    {
                        var queueMessageResponse = await queueClient
                            .ReceiveMessagesAsync(1, _workerOptions.VisibilityTimeout, stoppingToken);

                        queueMessage = queueMessageResponse?.Value.FirstOrDefault();

                        if (queueMessage == null)
                        {
                            _logger.LogInformation("No messages, sleeping...");
                            await Task.Delay(_workerOptions.NoMessagesSleepDuration, stoppingToken);
                            continue;
                        }

                        await processor.ProcessMessage(queueMessage, stoppingToken);
                    }
                    catch (Exception ex)
                    {
                        _logger.UnhandledMessageProcessingExceptionOccurred(ex);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.SetupExceptionOccurred(ex);
                await _exceptionHandler.HandleUnhandledExceptionAsync(null, ex, _workerOptions.SetupIssueStopHostCode);
            }
        }

    }
}
