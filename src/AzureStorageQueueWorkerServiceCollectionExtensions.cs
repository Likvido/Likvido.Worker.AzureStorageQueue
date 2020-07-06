using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Text;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

namespace Likvido.Worker.AzureStorageQueue
{
    public static class AzureStorageQueueWorkerServiceCollectionExtensions
    {
        private static bool _timeoutConfigured = false;
        private static bool _telemetryConfigured = false;

        public static IServiceCollection AddMessageProcessor<TMessage, TMessageProcessor>(
            [NotNull] this IServiceCollection serviceCollection,
            [NotNull] Action<IServiceProvider, AzureStorageQueueWorkerOptionsBuilder> optionsAction,
            ServiceLifetime serviceLifetime = ServiceLifetime.Scoped,
            bool configureTelemetry = true)
            where TMessageProcessor : IMessageProcessor<TMessage>
        {
            serviceCollection.TryAdd(
                new ServiceDescriptor(
                    typeof(AzureStorageQueueWorkerOptions<TMessageProcessor>),
                    p => CreateAzureStorageQueueWorkerOptions<TMessageProcessor>(p, optionsAction),
                    ServiceLifetime.Singleton));

            serviceCollection.TryAdd(
                new ServiceDescriptor(
                    typeof(TMessageProcessor),
                    typeof(TMessageProcessor),
                    serviceLifetime));

            serviceCollection.AddHostedService<AzureStorageQueueWorker<TMessage, TMessageProcessor>>();

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

            return serviceCollection;
        }

        private static AzureStorageQueueWorkerOptions<TMessageProcessor> CreateAzureStorageQueueWorkerOptions<TMessageProcessor>(
            [NotNull] IServiceProvider applicationServiceProvider,
            [NotNull] Action<IServiceProvider, AzureStorageQueueWorkerOptionsBuilder> optionsAction)
        {
            var options = new AzureStorageQueueWorkerOptions<TMessageProcessor>();
            var builder = new AzureStorageQueueWorkerOptionsBuilder(options);

            optionsAction?.Invoke(applicationServiceProvider, builder);

            return options;
        }

    }
}
