using System.Threading;
using System.Threading.Tasks;

namespace Likvido.Worker.AzureStorageQueue.MessageHandling
{
    public interface IMessageHandlerBase
    {
        Task HandleMessage(object cloudEvent, bool lastAttempt, CancellationToken cancellationToken);
    }
}
