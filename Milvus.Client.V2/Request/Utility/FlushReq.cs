using Milvus.Client.V2.Utils;
namespace Milvus.Client.V2.Requests.Utility;

/// <summary>
/// Represents a request to flush the given collections, sealing their in-memory segments.
/// </summary>
public sealed class FlushReq
{
    /// <summary>
    /// The names of the collections to flush.
    /// </summary>
    public IReadOnlyList<string> CollectionNames { get; set; } = Array.Empty<string>();

    /// <summary>
    /// The maximum time in milliseconds to wait for the flush to complete. When zero (default), the flush waits
    /// forever until all segments are flushed. When greater than zero, the flush returns once this time elapses.
    /// </summary>
    public long WaitFlushedMs { get; set; }

    internal Grpc.FlushRequest ToGrpcFlushRequest()
    {
        Verify.NotNullOrEmpty(CollectionNames);
        var request = new Grpc.FlushRequest();
        request.CollectionNames.AddRange(CollectionNames);
        return request;
    }
}
