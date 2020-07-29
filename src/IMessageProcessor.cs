using System.Threading;
using System.Threading.Tasks;

namespace Likvido.Worker.AzureStorageQueue
{
    public interface IMessageProcessor<TMessage>
    {
        /// <summary>
        /// Processes queue message
        /// </summary>
        /// <param name="message"></param>
        /// <param name="lastAttempt"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        Task ProcessMessage(TMessage message, bool lastAttempt, CancellationToken cancellationToken);
    }
}
