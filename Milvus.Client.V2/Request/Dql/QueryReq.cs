using Milvus.Client.V2.Types;
using Milvus.Client.V2.Utils;

namespace Milvus.Client.V2.Requests.Dql;

/// <summary>
/// Represents a request to query rows from a collection by expression.
/// </summary>
public sealed class QueryReq
{
    /// <summary>
    /// The name of the collection to query.
    /// </summary>
    public string CollectionName { get; set; } = "";

    /// <summary>
    /// The boolean expression identifying the rows to return (e.g. <c>"id in [1, 2, 3]"</c>).
    /// </summary>
    public string Expression { get; set; } = "";

    /// <summary>
    /// The optional query parameters.
    /// </summary>
    public QueryParameters? Parameters { get; set; }

    internal Grpc.QueryRequest ToGrpcQueryRequest(string? primaryKeyField = null)
    {
        Verify.NotNullOrWhiteSpace(CollectionName);

        bool hasIds = Parameters?.Ids is { Count: > 0 };
        bool hasExpression = !string.IsNullOrWhiteSpace(Expression);
        if (hasIds && hasExpression)
        {
            throw new ArgumentException("Expression and Ids cannot be set at the same time.");
        }
        if (!hasIds && !hasExpression)
        {
            throw new ArgumentException("Either Expression or Ids must be set.");
        }
        if (hasIds && primaryKeyField is null)
        {
            throw new ArgumentException("Ids requires a primary key field to build the query expression.");
        }

        // When either Limit or Offset is set, their sum must be in [1, 16384] (the server bound), matching the
        // search path; a type-valid 0/negative limit would otherwise make the server treat the query as
        // unlimited. When both are unset the server applies its own default.
        if (Parameters?.Limit is not null || Parameters?.Offset is not null)
        {
            long total = (Parameters.Limit ?? 0) + (Parameters.Offset ?? 0);
            if (total < 1 || total > 16384)
            {
                throw new ArgumentException(
                    $"The sum of Limit and Offset ({total}) must be between 1 and 16384.");
            }
        }

        var request = new Grpc.QueryRequest
        {
            CollectionName = CollectionName,
            Expr = hasIds ? MilvusClientV2.BuildPrimaryKeyExpression(primaryKeyField!, Parameters!.Ids!) : Expression
        };

        if (Parameters is not null)
        {
            if (Parameters.PartitionNamesInternal?.Count > 0)
            {
                request.PartitionNames.AddRange(Parameters.PartitionNamesInternal);
            }
            if (Parameters.OutputFieldsInternal?.Count > 0)
            {
                request.OutputFields.AddRange(Parameters.OutputFieldsInternal);
            }
            if (Parameters.Limit is not null)
            {
                request.QueryParams.Add(new Grpc.KeyValuePair { Key = "limit", Value = Parameters.Limit.Value.ToString(System.Globalization.CultureInfo.InvariantCulture) });
            }
            if (Parameters.Offset is not null)
            {
                request.QueryParams.Add(new Grpc.KeyValuePair { Key = "offset", Value = Parameters.Offset.Value.ToString(System.Globalization.CultureInfo.InvariantCulture) });
            }
            if (Parameters.TimeTravelTimestamp is not null)
            {
                request.TravelTimestamp = Parameters.TimeTravelTimestamp.Value;
            }
            if (Parameters.IgnoreGrowing is not null)
            {
                request.QueryParams.Add(new Grpc.KeyValuePair
                {
                    Key = "ignore_growing",
                    Value = Parameters.IgnoreGrowing.Value ? "true" : "false"
                });
            }
            if (Parameters.Timezone is not null)
            {
                request.QueryParams.Add(new Grpc.KeyValuePair { Key = "timezone", Value = Parameters.Timezone });
            }
            foreach (KeyValuePair<string, object> template in Parameters.FilterTemplates)
            {
                request.ExprTemplateValues[template.Key] = Milvus.Client.V2.MilvusClientV2.ToTemplateValue(template.Value);
            }
            if (Parameters.ConsistencyLevel is { } cl)
            {
                request.ConsistencyLevel = (Grpc.ConsistencyLevel)(int)cl;
            }
            else
            {
                // Unset consistency falls back to the collection's configured level (server default), the
                // same as when no QueryParameters are provided at all.
                request.UseDefaultConsistency = true;
            }
        }
        else
        {
            request.UseDefaultConsistency = true;
        }

        return request;
    }
}
