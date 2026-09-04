using Milvus.Client.V2.Utils;
namespace Milvus.Client.V2.Requests.Utility;

/// <summary>
/// Represents a request to get the loaded query-segment info of a collection.
/// </summary>
public sealed class GetQuerySegmentInfoReq
{
    /// <summary>
    /// The name of the collection whose query segment info to retrieve.
    /// </summary>
    public string CollectionName { get; set; } = "";
    internal Grpc.GetQuerySegmentInfoRequest ToGrpcGetQuerySegmentInfoRequest()
    {
        Verify.NotNullOrWhiteSpace(CollectionName);
        return new Grpc.GetQuerySegmentInfoRequest { CollectionName = CollectionName };
    }
}
