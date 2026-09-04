using Milvus.Client.V2.Types;
using Milvus.Client.V2.Utils;

namespace Milvus.Client.V2.Requests.Dml;

/// <summary>
/// Represents a request to upsert (insert-or-update) rows into a collection.
/// </summary>
public sealed class UpsertReq
{
    /// <summary>
    /// The name of the collection to upsert into.
    /// </summary>
    public string CollectionName { get; set; } = "";

    /// <summary>
    /// The field data to upsert; each field contains one value per row.
    /// </summary>
    public IReadOnlyList<FieldData> Data { get; set; } = Array.Empty<FieldData>();

    /// <summary>
    /// An optional partition to upsert into.
    /// </summary>
    public string? PartitionName { get; set; }

    /// <summary>
    /// When true, only the specified fields are updated while the other fields of existing rows remain unchanged.
    /// </summary>
    public bool PartialUpdate { get; set; }

    /// <summary>
    /// Per-field partial-update operations describing how each field is merged during a partial upsert.
    /// A non-replace operation implies <see cref="PartialUpdate" /> regardless of the flag.
    /// </summary>
    public IReadOnlyList<FieldPartialUpdateOp> FieldOps { get; set; } = Array.Empty<FieldPartialUpdateOp>();

    /// <summary>
    /// The timestamp of the collection schema used to encode the data, sent as <c>schema_timestamp</c>.
    /// When non-zero, the server can detect a stale schema and return <c>SchemaMismatch</c>, letting the
    /// client refresh its cache and retry. Mirrors the Java SDK's <c>setSchemaTimestamp</c>.
    /// </summary>
    internal ulong SchemaTimestamp { get; set; }

    internal Grpc.UpsertRequest ToGrpcUpsertRequest()
    {
        Verify.NotNullOrWhiteSpace(CollectionName);
        Verify.NotNullOrEmpty(Data);

        var request = new Grpc.UpsertRequest
        {
            CollectionName = CollectionName,
            PartitionName = PartitionName ?? "",
            NumRows = (uint)Data[0].RowCount
        };

        if (SchemaTimestamp != 0)
        {
            request.SchemaTimestamp = SchemaTimestamp;
        }

        foreach (FieldPartialUpdateOp fieldOp in FieldOps)
        {
            request.FieldOps.Add(fieldOp.ToGrpc());
        }

        if (PartialUpdate || FieldOps.Any(op => op.OpType != FieldPartialUpdateOpType.Replace))
        {
            request.PartialUpdate = true;
        }

        // Dynamic fields are aggregated into a single JSON metadata field (mirroring V1).
        Dictionary<string, object?>?[]? dynamicFieldsData = null;
        int rowCount = (int)request.NumRows;

        foreach (FieldData field in Data)
        {
            if (field.RowCount != rowCount)
            {
                throw new ArgumentException(
                    $"Field '{field.FieldName}' contains {field.RowCount} rows, but {rowCount} were expected; " +
                    "all fields in Data must contain the same number of rows.");
            }

            if (field.IsDynamic)
            {
                dynamicFieldsData ??= new Dictionary<string, object?>[rowCount];
                for (int rowNum = 0; rowNum < rowCount; rowNum++)
                {
                    Dictionary<string, object?> rowDynamicData =
                        dynamicFieldsData[rowNum] ?? (dynamicFieldsData[rowNum] = new Dictionary<string, object?>());

                    object? value = field.GetValueAsObject(rowNum);

                    // Dynamic fields are stored per row, so each row's $meta object carries only the keys that
                    // row actually has. A null means this row has no value for the key, and omitting it is not
                    // the same as writing an explicit JSON null -- the latter would make the key exist with a
                    // null value (matching the V1 SDK).
                    if (value is not null)
                    {
                        rowDynamicData[field.FieldName] = value;
                    }
                }
            }
            else
            {
                request.FieldsData.Add(field.ToGrpcFieldData());
            }
        }

        if (dynamicFieldsData is not null)
        {
            var encodedJson = new string[rowCount];
            for (int rowNum = 0; rowNum < rowCount; rowNum++)
            {
                encodedJson[rowNum] = System.Text.Json.JsonSerializer.Serialize(dynamicFieldsData[rowNum]);
            }

            request.FieldsData.Add(FieldData.CreateDynamicJson(encodedJson).ToGrpcFieldData());
        }

        return request;
    }
}
