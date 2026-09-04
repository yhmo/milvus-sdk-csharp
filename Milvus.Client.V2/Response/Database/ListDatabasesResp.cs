namespace Milvus.Client.V2.Responses.Database;

/// <summary>
/// The result of a list-databases request.
/// </summary>
public sealed class ListDatabasesResp
{
    private ListDatabasesResp(IReadOnlyList<string> databaseNames)
    {
        DatabaseNames = databaseNames;
    }
    internal static ListDatabasesResp FromGrpc(Grpc.ListDatabasesResponse response)
        => new(response.DbNames.ToList());

    /// <summary>
    /// The names of the databases in the cluster.
    /// </summary>
    public IReadOnlyList<string> DatabaseNames { get; }
}
