[![GitHub Workflow Status](https://img.shields.io/github/workflow/status/likvido/Likvido.Worker.AzureStorageQueue/Publish%20to%20nuget)](https://github.com/Likvido/Likvido.Worker.AzureStorageQueue/actions?query=workflow%3A%22Publish+to+nuget%22)
[![Nuget](https://img.shields.io/nuget/v/Likvido.Worker.AzureStorageQueue)](https://www.nuget.org/packages/Likvido.Worker.AzureStorageQueue/)
# Likvido.Worker.AzureStorageQueue
Small wrapper to ease creating services that read from Azure Storage Queues

## Usage
Create a new Worker service project, and install this package.
Implement `IMessageProcessor<TMessage>` where `TMessage` is the type of message you expect to receive (a POCO object, string or QueueMessage):

```
public class MessageProcessor : IMessageProcessor<MyCustomMessage>
{
    private readonly ILogger<Worker> logger;

    public Worker(ILogger<Worker> logger)
    {
        this.logger = logger;
    }

    public async Task ProcessMessage(MyCustomMessage message, CancellationToken stoppingToken)
    {
        // TODO: Process the message
    }
}
```
Register created class in Program:
```
services.AddMessageProcessor<MyCustomMessage, MessageProcessor>(
    (sp, b) =>
    {
        b.Options.AzureStorageConnectionString = "";
        b.Options.QueueName = "";
    });
```