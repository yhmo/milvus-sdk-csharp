using Milvus.Client.V2.Utils;

namespace Milvus.Client.V2.Requests.Index;

/// <summary>
/// Represents a request to describe an index.
/// </summary>
public sealed class DescribeIndexReq
{
    /// <summary>
    /// The name of the collection.
    /// </summary>
    public string CollectionName { get; set; } = "";

    /// <summary>
    /// The field name the index is built on.
    /// </summary>
    public string FieldName { get; set; } = "";

    /// <summary>
    /// The index name. When unset, the empty name is sent, which the proxy treats as "all indexes of the
    /// collection" (matching Java/PyMilvus). Only set it to describe a specific named index.
    /// </summary>
    public string? IndexName { get; set; }

    /// <summary>
    /// Only checks index state at this timestamp. All segments are checked when zero.
    /// </summary>
    public ulong Timestamp { get; set; }

    internal Grpc.DescribeIndexRequest ToGrpcDescribeIndexRequest()
    {
        Verify.NotNullOrWhiteSpace(CollectionName);
        Verify.NotNullOrWhiteSpace(FieldName);

        return new Grpc.DescribeIndexRequest
        {
            CollectionName = CollectionName,
            FieldName = FieldName,
            IndexName = IndexName ?? "",
            Timestamp = Timestamp
        };
    }
}
