using Milvus.Client.V2.Utils;
namespace Milvus.Client.V2.Requests.Collection;

/// <summary>
/// Represents a request to describe multiple collections at once, by name and/or collection ID.
/// </summary>
public sealed class BatchDescribeCollectionsReq
{
    /// <summary>
    /// The names of the collections to describe.
    /// </summary>
    public IReadOnlyList<string> CollectionNames { get; set; } = Array.Empty<string>();

    /// <summary>
    /// The IDs of the collections to describe.
    /// </summary>
    public IReadOnlyList<long> CollectionIds { get; set; } = Array.Empty<long>();

    internal Grpc.BatchDescribeCollectionRequest ToGrpcBatchDescribeCollectionRequest()
    {
        if (CollectionNames.Count == 0 && CollectionIds.Count == 0)
        {
            throw new ArgumentException("At least one collection name or collection ID must be provided.");
        }

        var request = new Grpc.BatchDescribeCollectionRequest();
        request.CollectionName.AddRange(CollectionNames);
        request.CollectionID.AddRange(CollectionIds);
        return request;
    }
}