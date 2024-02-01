[![GitHub Workflow Status](https://img.shields.io/github/actions/workflow/status/likvido/Likvido.Worker.AzureStorageQueue/nuget.yml)](https://img.shields.io/github/actions/workflow/status/likvido/Likvido.Worker.AzureStorageQueue/nuget.yml)
[![Nuget](https://img.shields.io/nuget/v/Likvido.Worker.AzureStorageQueue)](https://www.nuget.org/packages/Likvido.Worker.AzureStorageQueue/)
# Likvido.Worker.AzureStorageQueue
Small wrapper to ease creating services that read from Azure Storage Queues

## Usage
Create a new Worker service project, and install this package. Add one or more message handlers, which either inherit from `MessageHandlerBase<TMessage>` or implements `IMessageHandler<CloudEvent<TMessage>, TMessage>` where `TMessage` is the type of message you expect to receive (a POCO object or string):

```
public class MessageHandler : MessageHandlerBase<MyCustomMessage>
{
    private readonly ILogger<MessageHandler> logger;

    public MessageHandler(ILogger<MessageHandler> logger)
    {
        this.logger = logger;
    }

    public async Task HandleMessage(CloudEvent<MyCustomMessage> cloudEvent, bool lastAttempt, CancellationToken stoppingToken)
    {
        // TODO: Process the message
    }
}
```
Register created class in Program:
```
services.AddMessageProcessor(
    new AzureStorageQueueWorkerOptions
        {
            QueueName = "",
            AzureStorageConnectionString = "",
            EventTypeHandlerMapping =
            {
                { "invoice.created", typeof(MyInvoiceCreatedMessage)},
                { "*", typeof(MyCustomMessage)}
            }
        });
```
The library will automagically register all of the message handlers in the entry assembly, if they inherit from `MessageHandlerBase<TMessage>` or directly from `IMessageHandler<CloudEvent<TMessage>, TMessage>`. If you have other message handlers, then you can manually register these, like this:
```
services.AddScoped<IMessageHandler<CloudEvent<MyMessage>, MyMessage>, MyMessageHandler>();
```
### EventTypeHandlerMapping
The `EventTypeHandlerMapping` is used to map the `type` property of the CloudEvent to the correct message type. 
An example of an event type is ``invoice.created``. The ``*`` wildcard can be used to match all event types, but use it at your own risk. We should prefer to use the specific event type, as that makes it easier to create the correct C# classes for the data. 
