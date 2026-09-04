using Milvus.Client.V2.Utils;
namespace Milvus.Client.V2.Requests.Aliases;

/// <summary>
/// Represents a request to drop an alias.
/// </summary>
public sealed class DropAliasReq
{
    /// <summary>
    /// The name of the alias to drop.
    /// </summary>
    public string Alias { get; set; } = "";
    internal Grpc.DropAliasRequest ToGrpcDropAliasRequest()
    {
        Verify.NotNullOrWhiteSpace(Alias);
        return new Grpc.DropAliasRequest { Alias = Alias };
    }
}
