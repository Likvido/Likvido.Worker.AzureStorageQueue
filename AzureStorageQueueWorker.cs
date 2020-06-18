namespace Likvido.Worker.AzureStorageQueue
{
    using System;
    using System.Linq;
    using System.Text.Json;
    using System.Threading;
    using System.Threading.Tasks;
    using Azure.Storage.Queues;
    using Microsoft.Extensions.Hosting;
    using Microsoft.Extensions.Logging;

    public abstract class AzureStorageQueueWorker<T> : BackgroundService
    {
        private readonly ILogger logger;
        private readonly string azureStorageConnectionString;
        private readonly string queueName;
        private readonly TimeSpan? visibilityTimeout;

        public AzureStorageQueueWorker(
            ILogger logger,
            string azureStorageConnectionString,
            string queueName,
            TimeSpan? visibilityTimeout = null)
        {
            this.logger = logger;
            this.azureStorageConnectionString = azureStorageConnectionString;
            this.queueName = queueName;
            this.visibilityTimeout = visibilityTimeout;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            logger.LogInformation("Starting...");

            var queueClient = new QueueClient(azureStorageConnectionString, queueName);
            await queueClient.CreateIfNotExistsAsync();

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    var queueMessageResponse = await queueClient.ReceiveMessagesAsync(1, visibilityTimeout, stoppingToken);
                    var queueMessage = queueMessageResponse.Value.FirstOrDefault();

                    if (queueMessage == null)
                    {
                        logger.LogInformation("No messages, sleeping...");
                        await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
                        continue;
                    }

                    var message = JsonSerializer.Deserialize<T>(queueMessage.MessageText);
                    await ProcessMessage(message);
                    await queueClient.DeleteMessageAsync(queueMessage.MessageId, queueMessage.PopReceipt, stoppingToken);
                }
                catch (TaskCanceledException) { }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Error handling message");
                }
            }
        }

        protected abstract Task ProcessMessage(T message);
    }
}
