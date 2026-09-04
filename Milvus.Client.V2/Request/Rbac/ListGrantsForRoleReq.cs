using Milvus.Client.V2.Utils;
namespace Milvus.Client.V2.Requests.Rbac;

/// <summary>
/// Represents a request to list the privilege grants of a role.
/// </summary>
public sealed class ListGrantsForRoleReq
{
    /// <summary>
    /// The name of the role whose grants to list.
    /// </summary>
    public string RoleName { get; set; } = "";
    internal Grpc.SelectGrantRequest ToGrpcSelectGrantRequest()
    {
        Verify.NotNullOrWhiteSpace(RoleName);
        return new Grpc.SelectGrantRequest
        {
            Entity = new Grpc.GrantEntity { Role = new Grpc.RoleEntity { Name = RoleName } }
        };
    }
}
