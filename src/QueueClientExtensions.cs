using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Azure;
using Azure.Storage.Queues;
using Azure.Storage.Queues.Models;

namespace Likvido.Worker.AzureStorageQueue
{
    public static class QueueClientExtensions
    {
        public static async Task AddMessageAndCreateIfNotExistsAsync(
            this QueueClient queueClient,
            string message,
            TimeSpan? visibilityTimeout = null,
            CancellationToken cancellationToken = default)
        {
            if (queueClient == null)
            {
                throw new ArgumentNullException(nameof(queueClient));
            }

            try
            {
                await queueClient.SendMessageAsync(message, visibilityTimeout, cancellationToken: cancellationToken);
                return;
            }
            catch (RequestFailedException exception)
            when (exception.ErrorCode == QueueErrorCode.QueueNotFound
               && exception.Status == 404)
            {
                //swallow
            }

            await queueClient.CreateIfNotExistsAsync(cancellationToken: cancellationToken);
            await queueClient.SendMessageAsync(message, cancellationToken);
        }
    }
}
