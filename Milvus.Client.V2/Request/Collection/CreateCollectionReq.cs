using System.Globalization;
using System.Text.Json;
using Milvus.Client.V2.Types;
using Milvus.Client.V2.Utils;

namespace Milvus.Client.V2.Requests.Collection;

/// <summary>
/// Represents a request to create a collection.
/// </summary>
public sealed class CreateCollectionReq
{
    /// <summary>
    /// The name of the collection to create.
    /// </summary>
    public string CollectionName { get; set; } = "";

    /// <summary>
    /// The schema definition for the collection.
    /// </summary>
    public CollectionSchema? Schema { get; set; }

    /// <summary>
    /// The consistency level to be used by the collection. Defaults to <see cref="ConsistencyLevel.BoundedStaleness" />,
    /// matching the Java and C++ SDKs.
    /// </summary>
    public ConsistencyLevel ConsistencyLevel { get; set; } = ConsistencyLevel.BoundedStaleness;

    /// <summary>
    /// Number of the shards for the collection to create.
    /// </summary>
    public int ShardsNum { get; set; } = 1;

    /// <summary>
    /// Collection-level properties (e.g. TTL, consistency override, timezone, warmup), applied at creation time.
    /// </summary>
    public IDictionary<string, string> Properties { get; } = new Dictionary<string, string>();

    /// <summary>
    /// The number of default physical partitions, only used in partition-key mode.
    /// </summary>
    public long? NumPartitions { get; set; }

    /// <summary>
    /// Optional indexes to create immediately after the collection is created. When set, the collection is
    /// also loaded automatically after the indexes are built.
    /// </summary>
    public IReadOnlyList<IndexDesc> Indexes { get; set; } = Array.Empty<IndexDesc>();

    internal Grpc.CreateCollectionRequest ToGrpcCreateCollectionRequest()
    {
        Verify.NotNullOrWhiteSpace(CollectionName);
        Verify.NotNull(Schema);

        Grpc.CollectionSchema grpcSchema = new()
        {
            Name = Schema.Name ?? CollectionName,
            EnableDynamicField = Schema.EnableDynamicFields
        };

        if (Schema.Description is not null)
        {
            grpcSchema.Description = Schema.Description;
        }

        foreach (FunctionSchema function in Schema.Functions)
        {
            grpcSchema.Functions.Add(function.ToGrpcFunctionSchema());
        }

        foreach (FieldSchema field in Schema.Fields)
        {
            Grpc.FieldSchema grpcField = new()
            {
                Name = field.Name,
                DataType = (Grpc.DataType)(int)field.DataType,
                ElementType = field.ElementDataType is { } edt ? (Grpc.DataType)(int)edt : Grpc.DataType.None,
                IsPrimaryKey = field.IsPrimaryKey,
                IsPartitionKey = field.IsPartitionKey,
                AutoID = field.AutoId,
                Description = field.Description,
                Nullable = field.Nullable,
                IsClusteringKey = field.IsClusteringKey
            };

            if (field.DefaultValue is not null)
            {
                grpcField.DefaultValue = ConvertToValueField(field.DefaultValue, field.DataType);
            }

            if (field.EnableAnalyzer)
            {
                grpcField.TypeParams.Add(new Grpc.KeyValuePair { Key = Constants.EnableAnalyzer, Value = "true" });
            }

            if (field.EnableMatch)
            {
                grpcField.TypeParams.Add(new Grpc.KeyValuePair { Key = Constants.EnableMatch, Value = "true" });
            }

            if (field.AnalyzerParams is not null)
            {
                grpcField.TypeParams.Add(new Grpc.KeyValuePair
                {
                    Key = Constants.AnalyzerParams,
                    Value = JsonSerializer.Serialize(field.AnalyzerParams)
                });
            }

            if (field.MultiAnalyzerParams is not null)
            {
                grpcField.TypeParams.Add(new Grpc.KeyValuePair
                {
                    Key = Constants.MultiAnalyzerParams,
                    Value = JsonSerializer.Serialize(field.MultiAnalyzerParams)
                });
            }

            if (field.MaxLength is not null)
            {
                grpcField.TypeParams.Add(new Grpc.KeyValuePair
                {
                    Key = Constants.VarcharMaxLength,
                    Value = field.MaxLength.Value.ToString(CultureInfo.InvariantCulture)
                });
            }

            if (field.Dimension is not null)
            {
                grpcField.TypeParams.Add(new Grpc.KeyValuePair
                {
                    Key = Constants.VectorDim,
                    Value = field.Dimension.Value.ToString(CultureInfo.InvariantCulture)
                });
            }

            if (field.MaxCapacity is not null)
            {
                grpcField.TypeParams.Add(new Grpc.KeyValuePair
                {
                    Key = Constants.MaxCapacity,
                    Value = field.MaxCapacity.Value.ToString(CultureInfo.InvariantCulture)
                });
            }

            grpcSchema.Fields.Add(grpcField);
        }

        var result = new Grpc.CreateCollectionRequest
        {
            CollectionName = CollectionName,
            ConsistencyLevel = (Grpc.ConsistencyLevel)(int)ConsistencyLevel,
            ShardsNum = ShardsNum,
            Schema = grpcSchema.ToByteString()
        };

        foreach (KeyValuePair<string, string> property in Properties)
        {
            result.Properties.Add(new Grpc.KeyValuePair { Key = property.Key, Value = property.Value });
        }

        if (NumPartitions is { } numPartitions)
        {
            result.NumPartitions = numPartitions;
        }

        return result;
    }

    internal static Grpc.ValueField ConvertToValueField(object value, DataType dataType)
    {
        var result = new Grpc.ValueField();
        switch (dataType)
        {
            case DataType.Bool:
                result.BoolData = (bool)value;
                break;
            case DataType.Int8:
            case DataType.Int16:
            case DataType.Int32:
                result.IntData = Convert.ToInt32(value, CultureInfo.InvariantCulture);
                break;
            case DataType.Int64:
                result.LongData = Convert.ToInt64(value, CultureInfo.InvariantCulture);
                break;
            case DataType.Float:
                result.FloatData = Convert.ToSingle(value, CultureInfo.InvariantCulture);
                break;
            case DataType.Double:
                result.DoubleData = Convert.ToDouble(value, CultureInfo.InvariantCulture);
                break;
            case DataType.VarChar:
            case DataType.String:
                result.StringData = (string)value;
                break;
            case DataType.Json:
                // The Milvus proxy only accepts JSON default values on dynamic fields, which cannot be created
                // through this SDK, so any JSON default would be rejected server-side. Fail fast instead.
                throw new NotSupportedException("Default values are not supported for JSON fields.");
            case DataType.Timestamptz:
                result.TimestamptzData = Convert.ToInt64(value, CultureInfo.InvariantCulture);
                break;
            default:
                throw new NotSupportedException($"Default value is not supported for data type {dataType}");
        }

        return result;
    }
}
