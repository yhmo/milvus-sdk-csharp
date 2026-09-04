using Milvus.Client.V2.Utils;
namespace Milvus.Client.V2.Requests.Collection;

/// <summary>
/// Represents a request to drop a function from an existing collection.
/// </summary>
public sealed class DropCollectionFunctionReq
{
    /// <summary>
    /// The name of the collection containing the function to drop.
    /// </summary>
    public string CollectionName { get; set; } = "";

    /// <summary>
    /// The name of the function to drop.
    /// </summary>
    public string FunctionName { get; set; } = "";
    internal Grpc.DropCollectionFunctionRequest ToGrpcDropCollectionFunctionRequest(long collectionId)
    {
        Verify.NotNullOrWhiteSpace(CollectionName);
        Verify.NotNullOrWhiteSpace(FunctionName);
        return new Grpc.DropCollectionFunctionRequest
        {
            CollectionName = CollectionName,
            CollectionID = collectionId,
            FunctionName = FunctionName
        };
    }
}
