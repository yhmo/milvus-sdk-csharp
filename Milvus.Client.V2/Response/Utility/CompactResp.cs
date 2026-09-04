namespace Milvus.Client.V2.Responses.Utility;

/// <summary>
/// The result of a compaction operation.
/// </summary>
public sealed class CompactResp
{
    internal CompactResp(long compactionId) => CompactionId = compactionId;
    internal static CompactResp FromGrpc(Grpc.ManualCompactionResponse response) => new(response.CompactionID);

    /// <summary>
    /// The id of the compaction, used to poll its state and plans.
    /// </summary>
    public long CompactionId { get; }
}
