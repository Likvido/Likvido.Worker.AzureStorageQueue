using Microsoft.Extensions.Logging;

namespace Likvido.Worker.AzureStorageQueue
{
    public static class LoggerExtensions
    {
        public static class EventIds
        {
            public static readonly EventId OperationCancelledExceptionCaught = new EventId(1000, "OperationCancelledExceptionCaught");
        }

        public static void OperationCancelledExceptionOccurred(this ILogger logger) => logger.Log(LogLevel.Information, EventIds.OperationCancelledExceptionCaught, "A task/operation cancelled exception was caught.");
    }
}
