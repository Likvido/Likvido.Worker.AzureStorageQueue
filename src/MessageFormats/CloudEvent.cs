using System;
namespace Likvido.Worker.AzureStorageQueue.MessageFormats
{
    public class CloudEvent<TMessage>
    {
        public string Id { get; set; } = null!;
        public string Source { get; set; } = null!;
        public TMessage Data { get; set; } = default!;
        public string Type { get; set; } = null!;
        public DateTime Time { get; set; }
        public string SpecVersion { get; set; } = null!;
    }
}
