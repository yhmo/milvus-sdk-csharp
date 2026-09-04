using Milvus.Client.V2.Utils;
namespace Milvus.Client.V2.Requests.Collection;

/// <summary>
/// Represents a request to alter the properties of a field in an existing collection.
/// </summary>
public sealed class AlterCollectionFieldReq
{
    /// <summary>
    /// The name of the collection containing the field to alter.
    /// </summary>
    public string CollectionName { get; set; } = "";

    /// <summary>
    /// The name of the field to alter.
    /// </summary>
    public string FieldName { get; set; } = "";

    /// <summary>
    /// The key-value pairs of properties to set on the field.
    /// </summary>
    public IDictionary<string, string> Properties { get; } = new Dictionary<string, string>();

    /// <summary>
    /// The keys of properties to remove from the field, if any.
    /// </summary>
    public IReadOnlyList<string>? DeleteKeys { get; set; }
    internal Grpc.AlterCollectionFieldRequest ToGrpcAlterCollectionFieldRequest()
    {
        Verify.NotNullOrWhiteSpace(CollectionName);
        Verify.NotNullOrWhiteSpace(FieldName);
        var request = new Grpc.AlterCollectionFieldRequest
        {
            CollectionName = CollectionName,
            FieldName = FieldName
        };
        foreach (KeyValuePair<string, string> property in Properties)
        {
            request.Properties.Add(new Grpc.KeyValuePair { Key = property.Key, Value = property.Value });
        }
        if (DeleteKeys is not null)
        {
            request.DeleteKeys.AddRange(DeleteKeys);
        }
        return request;
    }
}
