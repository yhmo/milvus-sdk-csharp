using Milvus.Client.V2.Utils;
namespace Milvus.Client.V2.Requests.Partition;

/// <summary>
/// Represents a request to create a partition in a collection.
/// </summary>
public sealed class CreatePartitionReq
{
    /// <summary>
    /// The name of the collection to create the partition in.
    /// </summary>
    public string CollectionName { get; set; } = "";

    /// <summary>
    /// The name of the partition to create.
    /// </summary>
    public string PartitionName { get; set; } = "";
    internal Grpc.CreatePartitionRequest ToGrpcCreatePartitionRequest()
    {
        Verify.NotNullOrWhiteSpace(CollectionName);
        Verify.NotNullOrWhiteSpace(PartitionName);
        return new Grpc.CreatePartitionRequest { CollectionName = CollectionName, PartitionName = PartitionName };
    }
}
