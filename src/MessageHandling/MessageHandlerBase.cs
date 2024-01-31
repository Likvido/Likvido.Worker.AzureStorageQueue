using System.Threading;
using System.Threading.Tasks;
using Likvido.CloudEvents;

namespace Likvido.Worker.AzureStorageQueue.MessageHandling
{
    public abstract class MessageHandlerBase<TEvent> : IMessageHandler<CloudEvent<TEvent>, TEvent>
    {
        public abstract Task HandleMessage(CloudEvent<TEvent> cloudEvent, bool lastAttempt, CancellationToken cancellationToken);

        Task IMessageHandlerBase.HandleMessage(object cloudEvent, bool lastAttempt, CancellationToken cancellationToken)
        {
            return HandleMessage((CloudEvent<TEvent>)cloudEvent, lastAttempt, cancellationToken);
        }
    }
}
