namespace Milvus.Client.V2.Requests.Utility;

/// <summary>
/// Represents a request to flush all collections.
/// </summary>
public sealed class FlushAllReq
{
    internal static Grpc.FlushAllRequest ToGrpcFlushAllRequest() => new();
}
