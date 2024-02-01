using System.Threading;
using System.Threading.Tasks;
using Likvido.CloudEvents;

namespace Likvido.Worker.AzureStorageQueue.MessageHandling
{
    public interface IMessageHandler<in TCloudEvent, TMessage> : IMessageHandlerBase where TCloudEvent : CloudEvent<TMessage>
    {
        /// <summary>
        /// Handles the queue message
        /// </summary>
        /// <param name="cloudEvent"></param>
        /// <param name="lastAttempt"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        Task HandleMessage(TCloudEvent cloudEvent, bool lastAttempt, CancellationToken cancellationToken);
    }
}
