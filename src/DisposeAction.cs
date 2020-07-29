using System;
using System.Threading.Tasks;

namespace Likvido.Worker.AzureStorageQueue
{
    public sealed class DisposeAction : IAsyncDisposable
    {
        private Func<Task>? _action;

        public DisposeAction(Func<Task> action)
        {
            _action = action;
        }

        public async ValueTask DisposeAsync()
        {
            if (_action == null)
            {
                return;
            }
            Func<Task>? action = null;
            lock (_action)
            {
                action = _action;
                _action = null;
            }

            if (action != null)
            {
                await action().ConfigureAwait(false);
            }
        }
    }
}
