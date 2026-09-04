using Milvus.Client.V2.Utils;
namespace Milvus.Client.V2.Requests.Aliases;

/// <summary>
/// Represents a request to describe an alias.
/// </summary>
public sealed class DescribeAliasReq
{
    /// <summary>
    /// The name of the alias to describe.
    /// </summary>
    public string Alias { get; set; } = "";
    internal Grpc.DescribeAliasRequest ToGrpcDescribeAliasRequest()
    {
        Verify.NotNullOrWhiteSpace(Alias);
        return new Grpc.DescribeAliasRequest { Alias = Alias };
    }
}
