using Milvus.Client.V2.Types;
using Milvus.Client.V2.Utils;

namespace Milvus.Client.V2.Responses.Dql;

/// <summary>
/// Represents the result of a query operation.
/// </summary>
public sealed class QueryResp
{
    private QueryResp(string collectionName, IReadOnlyList<FieldData> fieldsData, ulong sessionTs)
    {
        CollectionName = collectionName;
        FieldsData = fieldsData;
        SessionTs = sessionTs;
    }

    internal static QueryResp FromGrpc(Grpc.QueryResults response)
        => new(
            response.CollectionName,
            DqlConversions.ProcessReturnedFieldData(response.FieldsData),
            response.SessionTs);

    /// <summary>
    /// The name of the queried collection.
    /// </summary>
    public string CollectionName { get; }

    /// <summary>
    /// The returned fields data.
    /// </summary>
    public IReadOnlyList<FieldData> FieldsData { get; }

    /// <summary>
    /// The server-side timestamp at which the query was executed, used for session-like operations such as
    /// iterators (read-your-writes consistency).
    /// </summary>
    public ulong SessionTs { get; }
}
