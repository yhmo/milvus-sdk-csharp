using Milvus.Client.V2.Utils;
namespace Milvus.Client.V2.Requests.Partition;

/// <summary>
/// Represents a request to list the partitions of a collection.
/// </summary>
public sealed class ListPartitionsReq
{
    /// <summary>
    /// The name of the collection to list the partitions of.
    /// </summary>
    public string CollectionName { get; set; } = "";
    internal Grpc.ShowPartitionsRequest ToGrpcShowPartitionsRequest()
    {
        Verify.NotNullOrWhiteSpace(CollectionName);
        return new Grpc.ShowPartitionsRequest { CollectionName = CollectionName };
    }
}
