using System;
using System.Collections.Generic;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Likvido.Worker.AzureStorageQueue
{
    public static class AzureStorageQueueWorkerServiceCollectionExtensions
    {
        private static bool _timeoutConfigured = false;
        private static bool _telemetryConfigured = false;

        public static IServiceCollection AddMessageProcessor<TMessage, TMessageProcessor>(
            this IServiceCollection serviceCollection,
            Action<IServiceProvider, AzureStorageQueueWorkerOptionsBuilder> optionsAction,
            ServiceLifetime serviceLifetime = ServiceLifetime.Scoped,
            bool configureTelemetry = true)
            where TMessageProcessor : IMessageProcessor<TMessage>
        {
            if (!_timeoutConfigured)
            {
                _timeoutConfigured = true;
                serviceCollection.PostConfigure<HostOptions>(o => o.ShutdownTimeout = TimeSpan.FromSeconds(20));
            }
            if (!_telemetryConfigured && configureTelemetry)
            {
                _telemetryConfigured = true;
                serviceCollection.AddApplicationInsightsTelemetryWorkerService();
            }

            serviceCollection.TryAdd(
                new ServiceDescriptor(
                    typeof(AzureStorageQueueWorkerOptions<TMessageProcessor>),
                    p => CreateAzureStorageQueueWorkerOptions<TMessage, TMessageProcessor>(p, optionsAction),
                    ServiceLifetime.Singleton));

            serviceCollection.TryAdd(
                new ServiceDescriptor(
                    typeof(TMessageProcessor),
                    typeof(TMessageProcessor),
                    serviceLifetime));

            serviceCollection.AddHostedService<AzureStorageQueueWorker<TMessage, TMessageProcessor>>();

            return serviceCollection;
        }

        private static AzureStorageQueueWorkerOptions<TMessageProcessor> CreateAzureStorageQueueWorkerOptions<TMessage, TMessageProcessor>(
            IServiceProvider applicationServiceProvider,
            Action<IServiceProvider, AzureStorageQueueWorkerOptionsBuilder> optionsAction)
            where TMessageProcessor : IMessageProcessor<TMessage>
        {
            var options = new AzureStorageQueueWorkerOptions<TMessageProcessor>();
            var builder = new AzureStorageQueueWorkerOptionsBuilder(options);

            optionsAction?.Invoke(applicationServiceProvider, builder);

            try
            {
                options.Validate();
            }
            catch (Exception ex)
            {
                var logger = applicationServiceProvider
                    .GetRequiredService<ILogger<AzureStorageQueueWorker<TMessage, TMessageProcessor>>>();
                logger.SetupExceptionOccurred(
                    ex,
                    "Missed required settings setup exception was caught. MessageProcessor {Processor}",
                    typeof(TMessageProcessor).FullName);
                throw;
            }

            return options;
        }
    }
}
