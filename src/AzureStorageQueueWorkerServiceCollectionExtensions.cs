using System;
using Microsoft.ApplicationInsights.DataContracts;
using Microsoft.ApplicationInsights.DependencyCollector;
using Microsoft.ApplicationInsights.Extensibility;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Likvido.Worker.AzureStorageQueue
{
    public static class AzureStorageQueueWorkerServiceCollectionExtensions
    {
        private static bool _timeoutConfigured;
        private static bool _telemetryConfigured;

        public static IServiceCollection AddMessageProcessor<TMessage, TMessageProcessor>(
            this IServiceCollection serviceCollection,
            Action<IServiceProvider, AzureStorageQueueWorkerOptionsBuilder> optionsAction,
            ServiceLifetime serviceLifetime = ServiceLifetime.Scoped,
            bool configureTelemetry = true,
            bool avoidMessageSampling = true,
            TimeSpan? shoutdownTimeout = null)
            where TMessageProcessor : IMessageProcessor<TMessage>
        {
            if (!_timeoutConfigured)
            {
                _timeoutConfigured = true;
                shoutdownTimeout ??= TimeSpan.FromSeconds(20);
                serviceCollection.PostConfigure<HostOptions>(o => o.ShutdownTimeout = shoutdownTimeout.Value);
            }

            if (configureTelemetry && !_telemetryConfigured)
            {
                _telemetryConfigured = true;
                serviceCollection.AddApplicationInsightsTelemetryWorkerService();
                serviceCollection.ConfigureTelemetryModule<DependencyTrackingTelemetryModule>((module, o) => { module.EnableSqlCommandTextInstrumentation = true; });
            }

            if (avoidMessageSampling)
            {
                ServiceCollectionDescriptorExtensions
                    .Add(
                        serviceCollection,
                        new ServiceDescriptor(
                            typeof(ITelemetryInitializer),
                            CreateAvoidSamplingTelemetryInitializer<TMessageProcessor>,
                            ServiceLifetime.Singleton));
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

        private static ITelemetryInitializer CreateAvoidSamplingTelemetryInitializer<TMessageProcessor>(IServiceProvider applicationServiceProvider)
        {
            var options = applicationServiceProvider
                .GetRequiredService<AzureStorageQueueWorkerOptions<TMessageProcessor>>();
            return new AvoidSamplingTelemetryInitializer(
                t => t is RequestTelemetry telemetry
                    && telemetry.Name == options.OperationName);
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
