using System;
using System.Diagnostics.CodeAnalysis;
using System.Threading.Tasks;

namespace Likvido.Worker.AzureStorageQueue
{
    public class AzureStorageQueueWorkerOptions
    {
        private TimeSpan _noMessagesSleepDuration = TimeSpan.FromSeconds(30);
        private string _queueName;
        private string _azureStorageConnectionString;
        private TimeSpan _visibilityTimeout = TimeSpan.FromSeconds(30);
        private string _poisonQueueName;
        private int _maxRetryCount = 5;

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

        [NotNull]
        public string QueueName
        {
            get { return _queueName; }
            set
            {
                if (value == null)
                {
                    throw new ArgumentNullException(nameof(QueueName));
                }
                _queueName = value;
            }
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

        [NotNull]
        public string AzureStorageConnectionString
        {
            get { return _azureStorageConnectionString; }
            set
            {
                if (value == null)
                {
                    throw new ArgumentNullException(nameof(AzureStorageConnectionString));
                }
                _azureStorageConnectionString = value;
            }
        }

        public Func<IServiceProvider, Exception, Task> UnhandledExceptionHandler { get; set; }
        public bool Base64Decode { get; set; } = true;
        public int? SetupIssueStopHostCode { get; set; } //null means no stop usefull
        public int? ProcessingIssueStopHostCode { get; set; } //null means no stop usefull

        /// <summary>
        /// Default is 30 seconds
        /// </summary>
        [NotNull]
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
    }

    public class AzureStorageQueueWorkerOptions<TMessageProcessor> : AzureStorageQueueWorkerOptions
    {
    }
}
