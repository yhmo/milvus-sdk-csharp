using Milvus.Client.V2.Utils;
namespace Milvus.Client.V2.Requests.Aliases;

/// <summary>
/// Represents a request to create an alias for a collection.
/// </summary>
public sealed class CreateAliasReq
{
    /// <summary>
    /// The name of the collection the alias points to.
    /// </summary>
    public string CollectionName { get; set; } = "";

    /// <summary>
    /// The name of the alias to create.
    /// </summary>
    public string Alias { get; set; } = "";
    internal Grpc.CreateAliasRequest ToGrpcCreateAliasRequest()
    {
        Verify.NotNullOrWhiteSpace(CollectionName);
        Verify.NotNullOrWhiteSpace(Alias);
        return new Grpc.CreateAliasRequest { CollectionName = CollectionName, Alias = Alias };
    }
}
