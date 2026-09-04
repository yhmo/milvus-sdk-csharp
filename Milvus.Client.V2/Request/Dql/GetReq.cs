using Milvus.Client.V2.Utils;

namespace Milvus.Client.V2.Requests.Dql;

/// <summary>
/// Represents a request to fetch rows by primary key.
/// </summary>
public sealed class GetReq
{
    /// <summary>
    /// The name of the collection.
    /// </summary>
    public string CollectionName { get; set; } = "";

    /// <summary>
    /// The primary key values to fetch.
    /// </summary>
    public IReadOnlyList<object> Ids { get; set; } = Array.Empty<object>();

    /// <summary>
    /// An optional partition name to restrict the fetch to. When empty, all partitions are queried.
    /// </summary>
    public string? PartitionName { get; set; }

    /// <summary>
    /// The fields to return. Empty means all fields.
    /// </summary>
    public IReadOnlyList<string>? OutputFields { get; set; }

    internal Grpc.QueryRequest ToGrpcQueryRequest(string primaryKeyField)
    {
        Verify.NotNullOrWhiteSpace(CollectionName);
        Verify.NotNullOrEmpty(Ids);

        var request = new Grpc.QueryRequest
        {
            CollectionName = CollectionName,
            Expr = MilvusClientV2.BuildPrimaryKeyExpression(primaryKeyField, Ids)
        };

        if (PartitionName is not null)
        {
            request.PartitionNames.Add(PartitionName);
        }

        if (OutputFields is { Count: > 0 })
        {
            request.OutputFields.AddRange(OutputFields);
        }

        return request;
    }
}
