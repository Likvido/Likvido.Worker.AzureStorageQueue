using System;
using Microsoft.Extensions.Logging;

namespace Likvido.Worker.AzureStorageQueue
{
    public static class LoggerExtensions
    {
        public static class EventIds
        {
            public static readonly EventId OperationCancelledExceptionCaught = new EventId(1000, nameof(OperationCancelledExceptionCaught));
            public static readonly EventId UnhandledMessageProcessingException = new EventId(1001, nameof(UnhandledMessageProcessingException));
            public static readonly EventId SetupException = new EventId(1002, nameof(SetupException));
            public static readonly EventId UpdateMessageVisibilityException = new EventId(1003, nameof(UpdateMessageVisibilityException));
            public static readonly EventId CustomErrorHandlerException = new EventId(1004, nameof(CustomErrorHandlerException));
        }

        public static void OperationCancelledExceptionOccurred(this ILogger logger) => logger.Log(LogLevel.Information, EventIds.OperationCancelledExceptionCaught, "A task/operation cancelled exception was caught.");

        public static void UnhandledMessageProcessingExceptionOccurred(this ILogger logger, Exception ex)
            => logger.Log(
                LogLevel.Error,
                EventIds.UnhandledMessageProcessingException,
                ex,
                "An unhandled exception during message processing was caught.");

        public static void SetupExceptionOccurred(this ILogger logger, Exception ex, string? message = null, params object[] args)
            => logger.Log(
                LogLevel.Critical,
                EventIds.SetupException,
                ex,
                message ?? "An unknown setup exception was caught.",
                args);

        public static void CustomErrorHandlerExceptionOccured(this ILogger logger, Exception ex)
            => logger.Log(
                LogLevel.Error,
                EventIds.CustomErrorHandlerException,
                ex,
                "An unhandled exception was thrown on a custom exception handler call.");
    }
}
