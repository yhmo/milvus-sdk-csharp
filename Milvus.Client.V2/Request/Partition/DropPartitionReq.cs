using Milvus.Client.V2.Utils;
namespace Milvus.Client.V2.Requests.Partition;

/// <summary>
/// Represents a request to drop a partition from a collection.
/// </summary>
public sealed class DropPartitionReq
{
    /// <summary>
    /// The name of the collection that contains the partition to drop.
    /// </summary>
    public string CollectionName { get; set; } = "";

    /// <summary>
    /// The name of the partition to drop.
    /// </summary>
    public string PartitionName { get; set; } = "";
    internal Grpc.DropPartitionRequest ToGrpcDropPartitionRequest()
    {
        Verify.NotNullOrWhiteSpace(CollectionName);
        Verify.NotNullOrWhiteSpace(PartitionName);
        return new Grpc.DropPartitionRequest { CollectionName = CollectionName, PartitionName = PartitionName };
    }
}
