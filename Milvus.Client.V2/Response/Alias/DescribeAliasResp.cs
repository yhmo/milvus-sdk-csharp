namespace Milvus.Client.V2.Responses.Aliases;

/// <summary>
/// Represents the result of a <c>DescribeAlias</c> operation.
/// </summary>
public sealed class DescribeAliasResp
{
    private DescribeAliasResp(string collectionName, string alias)
    {
        CollectionName = collectionName;
        Alias = alias;
    }
    internal static DescribeAliasResp FromGrpc(Grpc.DescribeAliasResponse response)
        => new(response.Collection, response.Alias);

    /// <summary>
    /// The name of the collection that the alias points to.
    /// </summary>
    public string CollectionName { get; }

    /// <summary>
    /// The name of the alias.
    /// </summary>
    public string Alias { get; }
}
