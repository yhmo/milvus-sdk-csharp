using Milvus.Client.V2.Utils;
namespace Milvus.Client.V2.Requests.Utility;

/// <summary>
/// Represents a request to get the persistent segment info of a collection.
/// </summary>
public sealed class GetPersistentSegmentInfoReq
{
    /// <summary>
    /// The name of the collection whose persistent segment info to retrieve.
    /// </summary>
    public string CollectionName { get; set; } = "";
    internal Grpc.GetPersistentSegmentInfoRequest ToGrpcGetPersistentSegmentInfoRequest()
    {
        Verify.NotNullOrWhiteSpace(CollectionName);
        return new Grpc.GetPersistentSegmentInfoRequest { CollectionName = CollectionName };
    }
}
