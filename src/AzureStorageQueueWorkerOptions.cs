using System;
using System.ComponentModel.DataAnnotations;
using System.Threading.Tasks;
using JetBrains.Annotations;

namespace Likvido.Worker.AzureStorageQueue
{
    [PublicAPI]
    public class AzureStorageQueueWorkerOptions
    {
        private string? _poisonQueueName;
        private string? _operationName;

        /// <summary>
        /// This will add a delay after flushing logs to Application Insights when the application is stopping
        /// Ideally should be set only for the first background service, with a value of 2-15 seconds
        /// </summary>
        public TimeSpan? FlushTimeout { get; set; }

        /// <summary>
        /// The time the message is invisible in the queue after it has been read
        /// Default 30 seconds which is default for QueueClient.ReceiveMessagesAsync
        /// </summary>
        public TimeSpan VisibilityTimeout { get; set; } = TimeSpan.FromSeconds(30);

        public required string QueueName { get; set; }

        public string OperationName
        {
            get 
            {
                if (string.IsNullOrWhiteSpace(_operationName))
                {
                    _operationName = $"Process {QueueName}";
                }

                return _operationName;
            }
            set { _operationName = value; }
        }

        /// <summary>
        /// Default {QueueName}-poison
        /// </summary>
        public string PoisonQueueName
        {
            get
            {
                if (string.IsNullOrWhiteSpace(_poisonQueueName))
                {
                    _poisonQueueName = QueueName + "-poison";
                }
                return _poisonQueueName;
            }
            set { _poisonQueueName = value; }
        }

        public required string AzureStorageConnectionString { get; set; }

        public Func<IServiceProvider, Exception, Task>? UnhandledExceptionHandler { get; set; }
        public bool Base64Decode { get; set; } = true;
        public int? SetupIssueStopHostCode { get; set; } // null means no stop useful
        public int? ProcessingIssueStopHostCode { get; set; } // null means no stop useful

        /// <summary>
        /// The time the worker sleeps when there are no messages in the queue
        /// Default is a random number of seconds between 5 and 40
        /// </summary>
        public TimeSpan? NoMessagesSleepDuration { get; set; }

        /// <summary>
        /// The maximum number of times a message will be retried
        /// Default is 5
        /// </summary>
        public int MaxRetryCount { get; set; } = 5;

        /// <summary>
        /// Optional sleep duration after processing each message
        /// </summary>
        public TimeSpan? SleepBetweenEachMessage { get; set; }

        internal void Validate()
        {
            if (string.IsNullOrWhiteSpace(AzureStorageConnectionString))
            {
                throw new ValidationException($"{nameof(AzureStorageConnectionString)} must be defined.");
            }

            if (string.IsNullOrWhiteSpace(QueueName))
            {
                throw new ValidationException($"{nameof(QueueName)} must be defined.");
            }

            if (TimeSpan.FromSeconds(5) > VisibilityTimeout)
            {
                throw new ValidationException($"{nameof(VisibilityTimeout)} must be minimum 5 seconds.");
            }

            if (NoMessagesSleepDuration.HasValue && TimeSpan.FromSeconds(5) > NoMessagesSleepDuration)
            {
                throw new ValidationException($"{nameof(NoMessagesSleepDuration)} must be minimum 5 seconds.");
            }

            if (MaxRetryCount < 1)
            {
                throw new ValidationException($"{nameof(MaxRetryCount)} cannot be less than 1");
            }
        }
    }
}
