using Milvus.Client.V2.Types;
using Milvus.Client.V2.Utils;

namespace Milvus.Client.V2.Responses.Dql;

/// <summary>
/// Represents the result of a <c>Get</c> (fetch by primary key) operation.
/// </summary>
public sealed class GetResp
{
    private GetResp(IReadOnlyList<FieldData> fieldsData, ulong sessionTs, string? primaryKeyName)
    {
        FieldsData = fieldsData;
        SessionTs = sessionTs;
        PrimaryKeyName = primaryKeyName;
    }

    internal static GetResp FromGrpc(Grpc.QueryResults response)
        => new(
            DqlConversions.ProcessReturnedFieldData(response.FieldsData),
            response.SessionTs,
            string.IsNullOrEmpty(response.PrimaryFieldName) ? null : response.PrimaryFieldName);

    /// <summary>
    /// The returned fields data.
    /// </summary>
    public IReadOnlyList<FieldData> FieldsData { get; }

    /// <summary>
    /// The server-side timestamp at which the fetch was executed, used for session-like operations such as
    /// iterators (read-your-writes consistency), matching <see cref="QueryResp.SessionTs" />.
    /// </summary>
    public ulong SessionTs { get; }

    /// <summary>
    /// The name of the primary-key field of the collection, as reported by the server, so callers can
    /// identify the pk column in <see cref="FieldsData" /> without re-describing the collection.
    /// </summary>
    public string? PrimaryKeyName { get; }
}
