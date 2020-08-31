using System;
using System.ComponentModel.DataAnnotations;
using System.Threading.Tasks;

namespace Likvido.Worker.AzureStorageQueue
{
    public class AzureStorageQueueWorkerOptions
    {
        private TimeSpan _noMessagesSleepDuration = TimeSpan.FromSeconds(30);
        private string? _queueName;
        private string? _azureStorageConnectionString;
        private TimeSpan _visibilityTimeout = TimeSpan.FromSeconds(30);
        private string? _poisonQueueName;
        private int _maxRetryCount = 5;
        private string? _operationName;

        /// <summary>
        /// Ideally should be set only for the first background service 2-15 seconds
        /// </summary>
        public TimeSpan? FlushTimeout { get; set; }

        /// <summary>
        /// Default 30 seconds which is default for QueueClient.ReceiveMessagesAsync
        /// </summary>
        public TimeSpan VisibilityTimeout
        {
            get { return _visibilityTimeout; }
            set
            {
                if (TimeSpan.FromSeconds(5) > value)
                {
                    throw new ArgumentException("Timeout must be 5 seconds minimum", nameof(VisibilityTimeout));
                }
                _visibilityTimeout = value;
            }
        }

        public string QueueName
        {
            get { return _queueName!; }
            set { _queueName = value; }
        }

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
        /// Default {QueueName} - poison
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

        public string AzureStorageConnectionString
        {
            get { return _azureStorageConnectionString!; }
            set { _azureStorageConnectionString = value; }
        }

        public Func<IServiceProvider, Exception, Task>? UnhandledExceptionHandler { get; set; }
        public bool Base64Decode { get; set; } = true;
        public int? SetupIssueStopHostCode { get; set; } //null means no stop usefull
        public int? ProcessingIssueStopHostCode { get; set; } //null means no stop usefull

        /// <summary>
        /// Default is 30 seconds
        /// </summary>
        public TimeSpan NoMessagesSleepDuration
        {
            get { return _noMessagesSleepDuration; }
            set
            {
                if (value == null)
                {
                    throw new ArgumentNullException(nameof(NoMessagesSleepDuration));
                }
                if (TimeSpan.FromSeconds(5) > value)
                {
                    throw new ArgumentException("Sleep duration must be 5 seconds minimum", nameof(NoMessagesSleepDuration));
                }
                _noMessagesSleepDuration = value;
            }
        }

        /// <summary>
        /// Default is 5
        /// </summary>
        public int MaxRetryCount
        {
            get { return _maxRetryCount; }
            set
            {
                if (value < 1)
                {
                    throw new ArgumentException("Sleep duration must be 5 seconds minimum", nameof(MaxRetryCount));
                }

                _maxRetryCount = value;
            }
        }

        internal void Validate()
        {
            if (string.IsNullOrWhiteSpace(_azureStorageConnectionString))
            {
                throw new ValidationException($"{nameof(AzureStorageConnectionString)} must be defined.");
            }
            if (string.IsNullOrWhiteSpace(_queueName))
            {
                throw new ArgumentNullException($"{nameof(QueueName)} must be defined.");
            }
        }
    }

    public class AzureStorageQueueWorkerOptions<TMessageProcessor> : AzureStorageQueueWorkerOptions
    {
    }
}
