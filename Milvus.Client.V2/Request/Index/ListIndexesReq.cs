using Milvus.Client.V2.Utils;

namespace Milvus.Client.V2.Requests.Index;

/// <summary>
/// Represents a request to list the indexes of a collection.
/// </summary>
public sealed class ListIndexesReq
{
    /// <summary>
    /// The name of the collection.
    /// </summary>
    public string CollectionName { get; set; } = "";

    /// <summary>
    /// An optional field name to filter the listed indexes to those built on that field. When empty, all
    /// indexes of the collection are returned.
    /// </summary>
    public string? FieldName { get; set; }

    internal Grpc.DescribeIndexRequest ToGrpcDescribeIndexRequest()
    {
        Verify.NotNullOrWhiteSpace(CollectionName);

        return new Grpc.DescribeIndexRequest
        {
            CollectionName = CollectionName,
            FieldName = FieldName ?? ""
        };
    }
}
