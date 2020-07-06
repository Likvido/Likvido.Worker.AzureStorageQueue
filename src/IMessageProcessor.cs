using System.Threading;
using System.Threading.Tasks;

namespace Likvido.Worker.AzureStorageQueue
{
    public interface IMessageProcessor<TMessage>
    {
        Task ProcessMessage(TMessage message, CancellationToken cancellationToken);
    }
}
