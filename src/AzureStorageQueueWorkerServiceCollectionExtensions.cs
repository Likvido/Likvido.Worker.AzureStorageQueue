using System;
using System.Linq;
using System.Reflection;
using JetBrains.Annotations;
using Likvido.CloudEvents;
using Likvido.Worker.AzureStorageQueue.MessageHandling;
using Microsoft.ApplicationInsights.DataContracts;
using Microsoft.ApplicationInsights.DependencyCollector;
using Microsoft.ApplicationInsights.Extensibility;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

namespace Likvido.Worker.AzureStorageQueue
{
    [PublicAPI]
    public static class AzureStorageQueueWorkerServiceCollectionExtensions
    {
        private static bool _timeoutConfigured;
        private static bool _telemetryConfigured;
        private static bool _messageHandlersRegistered;

        public static IServiceCollection AddMessageProcessor(
            this IServiceCollection serviceCollection,
            AzureStorageQueueWorkerOptions options,
            ServiceLifetime serviceLifetime = ServiceLifetime.Scoped,
            bool configureTelemetry = true,
            bool avoidMessageSampling = true,
            TimeSpan? shutdownTimeout = null)
        {
            options.Validate();

            if (!_timeoutConfigured)
            {
                _timeoutConfigured = true;
                shutdownTimeout ??= TimeSpan.FromMinutes(5);
                serviceCollection.PostConfigure<HostOptions>(o => o.ShutdownTimeout = shutdownTimeout.Value);
            }

            if (configureTelemetry && !_telemetryConfigured)
            {
                _telemetryConfigured = true;
                serviceCollection.AddApplicationInsightsTelemetryWorkerService();
                serviceCollection.ConfigureTelemetryModule<DependencyTrackingTelemetryModule>((module, _) => { module.EnableSqlCommandTextInstrumentation = true; });
            }

            if (!_messageHandlersRegistered)
            {
                _messageHandlersRegistered = true;
                RegisterMessageHandlersFromTheExecutingAssembly(serviceCollection, serviceLifetime);
            }

            if (avoidMessageSampling)
            {
                ServiceCollectionDescriptorExtensions
                    .Add(
                        serviceCollection,
                        new ServiceDescriptor(
                            typeof(ITelemetryInitializer),
                            CreateAvoidSamplingTelemetryInitializer(options.OperationName),
                            ServiceLifetime.Singleton));
            }

            serviceCollection.AddHostedService(sp => new AzureStorageQueueWorker(sp, options));

            return serviceCollection;
        }

        /// <summary>
        /// This is a helper method to automatically register all message handlers from the executing assembly
        /// If the developer needs to register other message handlers, they can do so manually, like this:
        /// services.AddScoped&lt;IMessageHandler&lt;CloudEvent&lt;MyMessage&gt;, MyMessage&gt;, MyMessageHandler&gt;();
        /// </summary>
        private static void RegisterMessageHandlersFromTheExecutingAssembly(IServiceCollection serviceCollection, ServiceLifetime serviceLifetime)
        {
            var assembly = Assembly.GetEntryAssembly();

            if (assembly == null)
            {
                return;
            }

            var messageHandlerTypes = assembly.GetTypes()
                .Where(x => x.GetInterfaces().Any(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IMessageHandler<,>)));

            foreach (var handlerType in messageHandlerTypes)
            {
                var interfaceType = handlerType.GetInterfaces().First(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IMessageHandler<,>));
                var genericArguments = interfaceType.GetGenericArguments();

                if (genericArguments[0].IsGenericType && genericArguments[0].GetGenericTypeDefinition() == typeof(CloudEvent<>))
                {
                    serviceCollection.TryAdd(new ServiceDescriptor(interfaceType, handlerType, serviceLifetime));
                }
            }
        }

        private static AvoidSamplingTelemetryInitializer CreateAvoidSamplingTelemetryInitializer(string operationName) =>
            new(x => x is RequestTelemetry telemetry && telemetry.Name == operationName);
    }
}
