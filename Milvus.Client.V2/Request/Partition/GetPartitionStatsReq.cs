using Milvus.Client.V2.Utils;
namespace Milvus.Client.V2.Requests.Partition;

/// <summary>
/// Represents a request to get statistics about a partition.
/// </summary>
public sealed class GetPartitionStatsReq
{
    /// <summary>
    /// The name of the collection that contains the partition.
    /// </summary>
    public string CollectionName { get; set; } = "";

    /// <summary>
    /// The name of the partition to get statistics for.
    /// </summary>
    public string PartitionName { get; set; } = "";
    internal Grpc.GetPartitionStatisticsRequest ToGrpcGetPartitionStatisticsRequest()
    {
        Verify.NotNullOrWhiteSpace(CollectionName);
        Verify.NotNullOrWhiteSpace(PartitionName);
        return new Grpc.GetPartitionStatisticsRequest { CollectionName = CollectionName, PartitionName = PartitionName };
    }
}
