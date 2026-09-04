using Milvus.Client.V2.Utils;

namespace Milvus.Client.V2.Requests.Dml;

/// <summary>
/// Represents a request to delete rows from a collection.
/// </summary>
public sealed class DeleteReq
{
    /// <summary>
    /// The name of the collection to delete from.
    /// </summary>
    public string CollectionName { get; set; } = "";

    /// <summary>
    /// The boolean expression identifying the rows to delete (e.g. <c>"id in [1, 2, 3]"</c>).
    /// </summary>
    public string Expression { get; set; } = "";

    /// <summary>
    /// The primary key values identifying the rows to delete. Mutually exclusive with <see cref="Expression" />;
    /// when set, an expression is generated from the collection's primary key field.
    /// </summary>
    public IReadOnlyList<object>? Ids { get; set; }

    /// <summary>
    /// An optional partition to delete from.
    /// </summary>
    public string? PartitionName { get; set; }

    /// <summary>
    /// The consistency level to use for the delete. When set, the request carries an explicit consistency
    /// level and a matching guarantee timestamp (matching the Java SDK's <c>DeleteReq.consistencyLevel</c>).
    /// When <c>null</c>, the server applies the collection's configured level.
    /// </summary>
    public Milvus.Client.V2.Types.ConsistencyLevel? ConsistencyLevel { get; set; }

    /// <summary>
    /// Named expression-template values used by the delete expression, for parameterized expressions.
    /// </summary>
    public IDictionary<string, object> FilterTemplates { get; } = new Dictionary<string, object>();

    internal Grpc.DeleteRequest ToGrpcDeleteRequest(string? primaryKeyField = null)
    {
        Verify.NotNullOrWhiteSpace(CollectionName);

        string expression;
        if (Ids is { Count: > 0 })
        {
            if (!string.IsNullOrWhiteSpace(Expression))
            {
                throw new ArgumentException("Expression and Ids cannot be set at the same time.");
            }

            if (primaryKeyField is null)
            {
                throw new ArgumentException("Ids requires a primary key field to build the delete expression.");
            }

            expression = MilvusClientV2.BuildPrimaryKeyExpression(primaryKeyField, Ids);
        }
        else
        {
            Verify.NotNullOrWhiteSpace(Expression);
            expression = Expression;
        }

        var request = new Grpc.DeleteRequest
        {
            CollectionName = CollectionName,
            PartitionName = PartitionName ?? "",
            Expr = expression
        };

        if (ConsistencyLevel is { } cl)
        {
            request.ConsistencyLevel = (Grpc.ConsistencyLevel)(int)cl;
        }

        foreach (KeyValuePair<string, object> template in FilterTemplates)
        {
            request.ExprTemplateValues[template.Key] = MilvusClientV2.ToTemplateValue(template.Value);
        }

        return request;
    }
}
