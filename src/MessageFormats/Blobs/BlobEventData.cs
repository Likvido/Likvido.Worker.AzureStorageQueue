namespace Likvido.Worker.AzureStorageQueue.MessageFormats.Blobs
{
    public class BlobEventData
    {
        public string Api { get; set; } = null!;
        public string ClientRequestId { get; set; } = null!;
        public string RequestId { get; set; } = null!;
        public string ETag { get; set; } = null!;
        public string ContentType { get; set; } = null!;
        public int ContentLength { get; set; }
        public string BlobType { get; set; } = null!;
        public string Url { get; set; } = null!;
        public string Sequencer { get; set; } = null!;
        public StorageDiagnostics StorageDiagnostics { get; set; } = null!;
    }

    public class StorageDiagnostics
    {
        public string BatchId { get; set; } = null!;
    }
}
