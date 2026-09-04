namespace Milvus.Client.V2.Requests.Utility;

/// <summary>
/// Represents a request to flush all collections.
/// </summary>
public sealed class FlushAllReq
{
    /// <summary>
    /// When greater than zero, <c>FlushAllAsync</c> waits for the flush-all operation to complete by polling
    /// <see cref="MilvusClientV2.GetFlushAllStateAsync" /> for up to this many milliseconds, throwing on
    /// timeout. When zero (default), the RPC returns immediately. Mirrors the C++ SDK's <c>WaitFlushedMs</c>.
    /// </summary>
    public int WaitFlushedMs { get; set; }

    internal static Grpc.FlushAllRequest ToGrpcFlushAllRequest() => new();
}
