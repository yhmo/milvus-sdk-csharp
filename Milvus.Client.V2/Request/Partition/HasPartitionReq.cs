using Milvus.Client.V2.Utils;
namespace Milvus.Client.V2.Requests.Partition;

/// <summary>
/// Represents a request to check whether a partition exists in a collection.
/// </summary>
public sealed class HasPartitionReq
{
    /// <summary>
    /// The name of the collection that the partition belongs to.
    /// </summary>
    public string CollectionName { get; set; } = "";

    /// <summary>
    /// The name of the partition to check.
    /// </summary>
    public string PartitionName { get; set; } = "";
    internal Grpc.HasPartitionRequest ToGrpcHasPartitionRequest()
    {
        Verify.NotNullOrWhiteSpace(CollectionName);
        Verify.NotNullOrWhiteSpace(PartitionName);
        return new Grpc.HasPartitionRequest { CollectionName = CollectionName, PartitionName = PartitionName };
    }
}
