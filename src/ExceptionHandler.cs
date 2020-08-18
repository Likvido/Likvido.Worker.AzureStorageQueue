using System;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Likvido.Worker.AzureStorageQueue
{
    public class ExceptionHandler
    {
        private readonly ILogger _logger;
        private readonly AzureStorageQueueWorkerOptions _workerOptions;
        private readonly IServiceProvider _serviceProvider;
        private readonly IHostApplicationLifetime _hostApplicationLifetime;

        public ExceptionHandler(
            ILogger logger,
            AzureStorageQueueWorkerOptions workerOptions,
            IServiceProvider serviceProvider,
            IHostApplicationLifetime hostApplicationLifetime)
        {
            _logger = logger;
            _workerOptions = workerOptions;
            _serviceProvider = serviceProvider;
            _hostApplicationLifetime = hostApplicationLifetime;
        }

        /// <summary>
        /// Makes logging
        /// Executes UnhandledExceptionHandler if any
        /// Stops Host based on exitCode passed. If it's null no stop
        /// </summary>
        /// <param name="scope"></param>
        /// <param name="exception"></param>
        /// <param name="exitCode"></param>
        public async Task HandleUnhandledExceptionAsync(IServiceScope? scope, Exception exception, int? exitCode)
        {
            IServiceScope? localScope = null;
            try
            {
                var handler = _workerOptions.UnhandledExceptionHandler;
                if (handler != null)
                {
                    if (scope == null)
                    {
                        scope = localScope = _serviceProvider.CreateScope();
                    }
                    await handler.Invoke(scope.ServiceProvider, exception);
                }
            }
            catch (Exception e)
            {
                _logger.CustomErrorHandlerExceptionOccured(e);
            }
            finally
            {
                localScope?.Dispose();
            }

            if (exitCode.HasValue)
            {
                Environment.ExitCode = exitCode.Value;
                _hostApplicationLifetime.StopApplication();
            }
        }
    }
}
