using Milvus.Client.V2.Utils;

namespace Milvus.Client.V2.Requests.Collection;

/// <summary>
/// Represents a request to get the load state of a collection.
/// </summary>
public sealed class GetLoadStateReq
{
    /// <summary>
    /// The name of the collection.
    /// </summary>
    public string CollectionName { get; set; } = "";

    /// <summary>
    /// Optional partition names to check the load state of. When empty, the state of the whole collection is returned.
    /// </summary>
    public IReadOnlyList<string> PartitionNames { get; set; } = Array.Empty<string>();

    internal Grpc.GetLoadStateRequest ToGrpcGetLoadStateRequest()
    {
        Verify.NotNullOrWhiteSpace(CollectionName);
        var request = new Grpc.GetLoadStateRequest { CollectionName = CollectionName };
        request.PartitionNames.AddRange(PartitionNames);
        return request;
    }
}
