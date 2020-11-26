using System;

namespace Likvido.Worker.AzureStorageQueue.MessageFormats
{
    /// <summary>
    /// Implementation of a cloud event, using v1.0
    /// Spec: https://github.com/cloudevents/spec/blob/v1.0/spec.md
    /// </summary>
    public class CloudEvent
    {
        public string Id { get; set; } = null!;
        public string Source { get; set; } = null!;
        public string Type { get; set; } = null!;
        public DateTime? Time { get; set; }
        public string SpecVersion { get; set; } = null!;
    }

    public class CloudEvent<TMessage> : CloudEvent
    {
        public TMessage Data { get; set; } = default!;
    }
}
