# Likvido.Worker.AzureStorageQueue
Small wrapper to ease creating services that read from Azure Storage Queues

## Usage
Create a new Worker service project, and install this package. Then change the base class of your `Worker` to `AzureStorageQueueWorker<T>` where `T` is the type of message you expect to receive (a POCO object):

```
public class Worker : AzureStorageQueueWorker<MyCustomMessage>
{
    private readonly ILogger<Worker> logger;

    public Worker(
        ILogger<Worker> logger,
        IConfiguration configuration)
        : base(logger, configuration["AZURE_STORAGE_CONNECTION_STRING"], configuration["QUEUE_NAME"])
    {
        this.logger = logger;
    }

    protected override async Task ProcessMessage(MyCustomMessage message)
    {
        // TODO: Process the message
    }
}
```
