using Milvus.Client.V2.Types;

using System.Globalization;
using System.Text.Json;

namespace Milvus.Client.V2.Responses.Collection;

/// <summary>
/// Represents the result of a <c>DescribeCollection</c> operation.
/// </summary>
public sealed class DescribeCollectionResp
{
    private DescribeCollectionResp(
        long collectionId, string collectionName, CollectionSchema schema, int shardsNum,
        ConsistencyLevel consistencyLevel, ulong createdTimestamp, ulong updateTimestamp,
        IReadOnlyList<string> aliases, IReadOnlyDictionary<string, string> properties)
    {
        CollectionId = collectionId;
        CollectionName = collectionName;
        Schema = schema;
        ShardsNum = shardsNum;
        ConsistencyLevel = consistencyLevel;
        CreatedTimestamp = createdTimestamp;
        UpdateTimestamp = updateTimestamp;
        Aliases = aliases;
        Properties = properties;
    }

    internal static DescribeCollectionResp FromGrpc(Grpc.DescribeCollectionResponse response)
        => new(
            response.CollectionID,
            response.Schema.Name,
            ConvertSchema(response.Schema),
            response.ShardsNum,
            (ConsistencyLevel)response.ConsistencyLevel,
            response.CreatedTimestamp,
            response.UpdateTimestamp,
            response.Aliases.ToList(),
            response.Properties.ToDictionary(p => p.Key, p => p.Value));

    /// <summary>
    /// The collection id.
    /// </summary>
    public long CollectionId { get; }

    /// <summary>
    /// The collection name.
    /// </summary>
    public string CollectionName { get; }

    /// <summary>
    /// The collection schema.
    /// </summary>
    public CollectionSchema Schema { get; }

    /// <summary>
    /// The number of shards.
    /// </summary>
    public int ShardsNum { get; }

    /// <summary>
    /// The consistency level of the collection.
    /// </summary>
    public ConsistencyLevel ConsistencyLevel { get; }

    /// <summary>
    /// The hybrid timestamp at which the collection was created.
    /// </summary>
    public ulong CreatedTimestamp { get; }

    /// <summary>
    /// The hybrid timestamp at which the collection's schema was last updated (e.g. by a field or property
    /// change). Matching the C++ SDK's <c>CollectionDesc::UpdateTime()</c>.
    /// </summary>
    public ulong UpdateTimestamp { get; }

    /// <summary>
    /// The aliases of the collection.
    /// </summary>
    public IReadOnlyList<string> Aliases { get; }

    /// <summary>
    /// The collection-level properties (e.g. TTL, consistency override, timezone), as set at creation time or
    /// via <c>AlterCollectionProperties</c>.
    /// </summary>
    public IReadOnlyDictionary<string, string> Properties { get; }

    internal static CollectionSchema ConvertSchema(Grpc.CollectionSchema grpcSchema)
    {
        var schema = new CollectionSchema
        {
            Name = grpcSchema.Name,
            Description = string.IsNullOrEmpty(grpcSchema.Description) ? null : grpcSchema.Description,
            EnableDynamicFields = grpcSchema.EnableDynamicField
        };

        foreach (Grpc.KeyValuePair property in grpcSchema.Properties)
        {
            schema.Properties[property.Key] = property.Value;
        }

        foreach (Grpc.FunctionSchema grpcFunction in grpcSchema.Functions)
        {
            schema.Functions.Add(FunctionSchema.FromGrpc(grpcFunction));
        }

        foreach (Grpc.FieldSchema grpcField in grpcSchema.Fields)
        {
            var field = new FieldSchema(
                grpcField.Name,
                (DataType)grpcField.DataType,
                grpcField.IsPrimaryKey,
                grpcField.AutoID,
                grpcField.IsPartitionKey,
                grpcField.Description)
            {
                ElementDataType = grpcField.ElementType == Grpc.DataType.None ? null : (DataType)grpcField.ElementType,
                Nullable = grpcField.Nullable,
                IsFunctionOutput = grpcField.IsFunctionOutput,
                IsClusteringKey = grpcField.IsClusteringKey,
                IsDynamic = grpcField.IsDynamic
            };

            if (grpcField.DefaultValue is not null)
            {
                field.DefaultValue = ConvertDefaultValue(grpcField.DefaultValue, field.DataType);
            }

            foreach (Grpc.KeyValuePair parameter in grpcField.TypeParams)
            {
                // The kernel does not validate type_params, so a malformed value must degrade to the default
                // rather than abort the whole DescribeCollection (and any SchemaCache population) -- the Java
                // SDK deliberately try/catches these same parses.
                switch (parameter.Key)
                {
                    case "max_length" when long.TryParse(parameter.Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out long maxLength):
                        field.MaxLength = (int)maxLength;
                        break;
                    case "dim" when long.TryParse(parameter.Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out long dim):
                        field.Dimension = (int)dim;
                        break;
                    case "max_capacity" when long.TryParse(parameter.Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out long maxCapacity):
                        field.MaxCapacity = (int)maxCapacity;
                        break;
                    case "enable_analyzer" when bool.TryParse(parameter.Value, out bool enableAnalyzer):
                        field.EnableAnalyzer = enableAnalyzer;
                        break;
                    case "analyzer_params":
                        try
                        {
                            field.AnalyzerParams = DeserializeAnalyzerParams(parameter.Value);
                        }
                        catch (JsonException)
                        {
                            // Malformed analyzer params degrade to null instead of aborting the describe.
                        }

                        break;
                    case "enable_match" when bool.TryParse(parameter.Value, out bool enableMatch):
                        field.EnableMatch = enableMatch;
                        break;
                    case "multi_analyzer_params":
                        try
                        {
                            field.MultiAnalyzerParams = DeserializeAnalyzerParams(parameter.Value);
                        }
                        catch (JsonException)
                        {
                            // Malformed multi-analyzer params degrade to null instead of aborting the describe.
                        }

                        break;
                }
            }

            schema.Fields.Add(field);
        }

        return schema;
    }

    private static object? ConvertDefaultValue(Grpc.ValueField value, DataType dataType)
        => dataType switch
        {
            DataType.Bool => value.BoolData,
            DataType.Int8 or DataType.Int16 or DataType.Int32 => value.IntData,
            DataType.Int64 => value.LongData,
            DataType.Float => value.FloatData,
            DataType.Double => value.DoubleData,
            DataType.VarChar or DataType.String => value.StringData,
            DataType.Timestamptz => value.TimestamptzData,
            _ => null
        };

    // Analyzer params are round-tripped through JSON. Deserialize into the same CLR value types the request
    // accepts (string/long/double/bool and nested dictionaries) so that, unlike raw JsonElement values,
    // comparisons like AnalyzerParams["type"] == "english" work and re-serialization keeps the same shape.
    private static Dictionary<string, object>? DeserializeAnalyzerParams(string json)
    {
        using JsonDocument document = JsonDocument.Parse(json);
        return document.RootElement.EnumerateObject()
            .ToDictionary(p => p.Name, p => ConvertJsonValue(p.Value));
    }

    private static object ConvertJsonValue(JsonElement element)
        => element.ValueKind switch
        {
            JsonValueKind.String => element.GetString()!,
            JsonValueKind.Number when element.TryGetInt64(out long longValue) => longValue,
            JsonValueKind.Number => element.GetDouble(),
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.Null => null!,
            JsonValueKind.Array => element.EnumerateArray().Select(ConvertJsonValue).ToList(),
            _ => element.EnumerateObject().ToDictionary(p => p.Name, p => ConvertJsonValue(p.Value))
        };
}
