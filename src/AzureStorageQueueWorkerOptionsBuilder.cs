namespace Likvido.Worker.AzureStorageQueue
{
    public class AzureStorageQueueWorkerOptionsBuilder
    {
        public AzureStorageQueueWorkerOptionsBuilder(AzureStorageQueueWorkerOptions options)
        {
            Options = options;
        }

        public AzureStorageQueueWorkerOptions Options { get; }
    }
}
