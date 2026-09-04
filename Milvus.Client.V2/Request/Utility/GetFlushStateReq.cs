namespace Milvus.Client.V2.Requests.Utility;

/// <summary>
/// Represents a request to check whether the given segments have been flushed.
/// </summary>
public sealed class GetFlushStateReq
{
    /// <summary>
    /// The segment IDs to check.
    /// </summary>
    public IReadOnlyList<long> SegmentIds { get; set; } = Array.Empty<long>();

    /// <summary>
    /// The collection name the segments belong to.
    /// </summary>
    public string CollectionName { get; set; } = "";

    internal Grpc.GetFlushStateRequest ToGrpcGetFlushStateRequest()
    {
        var request = new Grpc.GetFlushStateRequest { CollectionName = CollectionName };
        request.SegmentIDs.AddRange(SegmentIds);
        return request;
    }
}
