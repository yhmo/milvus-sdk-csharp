namespace Milvus.Client.V2.Responses.Aliases;

/// <summary>
/// Represents the result of a <c>ListAliases</c> operation.
/// </summary>
public sealed class ListAliasesResp
{
    private ListAliasesResp(IReadOnlyList<string> aliases)
    {
        Aliases = aliases;
    }
    internal static ListAliasesResp FromGrpc(Grpc.ListAliasesResponse response)
        => new(response.Aliases.ToList());

    /// <summary>
    /// The aliases of the requested collection or collections.
    /// </summary>
    public IReadOnlyList<string> Aliases { get; }
}
