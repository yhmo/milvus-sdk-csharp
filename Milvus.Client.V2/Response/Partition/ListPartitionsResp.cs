namespace Milvus.Client.V2.Responses.Partition;

/// <summary>
/// The result of a <c>ListPartitions</c> operation.
/// </summary>
public sealed class ListPartitionsResp
{
    private ListPartitionsResp(IReadOnlyList<string> partitionNames, IReadOnlyList<long> partitionIds)
    {
        PartitionNames = partitionNames;
        PartitionIds = partitionIds;
    }
    internal static ListPartitionsResp FromGrpc(Grpc.ShowPartitionsResponse response)
        => new(response.PartitionNames.ToList(), response.PartitionIDs.ToList());

    /// <summary>
    /// The names of the partitions in the collection.
    /// </summary>
    public IReadOnlyList<string> PartitionNames { get; }

    /// <summary>
    /// The IDs of the partitions in the collection.
    /// </summary>
    public IReadOnlyList<long> PartitionIds { get; }
}
