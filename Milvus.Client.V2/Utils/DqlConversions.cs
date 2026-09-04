using Google.Protobuf.Collections;
using Milvus.Client.V2.Types;

namespace Milvus.Client.V2.Utils;

/// <summary>
/// Shared conversions between proto and V2 DTOs for the DQL domain (search/query results).
/// </summary>
internal static class DqlConversions
{
    /// <summary>
    /// Converts the proto field data of a search/query result into V2 <see cref="FieldData" /> objects.
    /// </summary>
    public static List<FieldData> ProcessReturnedFieldData(RepeatedField<Grpc.FieldData> grpcFields)
    {
        var results = new List<FieldData>(grpcFields.Count);
        foreach (Grpc.FieldData grpcField in grpcFields)
        {
            if (grpcField.IsDynamic)
            {
                // Surface dynamic fields as their raw JSON metadata (the server-side "$meta" column)
                // instead of silently dropping them. The aggregated field carries no schema-level name.
                if (grpcField.Scalars?.DataCase == Grpc.ScalarField.DataOneofCase.JsonData)
                {
                    results.Add(FieldData.CreateDynamicJson(
                        grpcField.Scalars.JsonData.Data.Select(p => p.ToStringUtf8()).ToList()));
                }

                continue;
            }

            results.Add(FromGrpcFieldData(grpcField));
        }

        return results;
    }

    // The group-by search response carries the per-group field value in a FieldData whose FieldName is
    // empty on the wire; FieldData.Create requires a non-blank name, so synthesize one. The synthesized
    // name is only a local label; callers access the value through the response's GroupByFieldValue.
    private const string GroupByFieldValueName = "group_by_field_value";

    internal static FieldData? ProcessGroupByFieldValue(Grpc.FieldData groupByFieldValue)
    {
        if (groupByFieldValue.FieldName is { Length: > 0 })
        {
            return FromGrpcFieldData(groupByFieldValue);
        }

        // FieldName has a public setter on the generated message; copy the value into a fresh message so
        // we do not mutate the server's response object.
        Grpc.FieldData renamed = groupByFieldValue.Clone();
        renamed.FieldName = GroupByFieldValueName;
        return FromGrpcFieldData(renamed);
    }

    /// <summary>
    /// Trims each column of the given field-data list to the rows in <c>[start, start+count)</c>, preserving
    /// each field's name, data type and dynamic flag. Used by the query iterator to cap a page that the server
    /// over-delivers when <c>reduce_stop_for_best</c> is enabled, and by <c>SingleResult</c> slicing.
    /// </summary>
    public static IReadOnlyList<FieldData> TakeRows(IReadOnlyList<FieldData> fields, int start, int count)
    {
        if (fields.Count == 0)
        {
            return fields;
        }

        var result = new List<FieldData>(fields.Count);
        foreach (FieldData field in fields)
        {
            result.Add(field.Slice(start, count));
        }

        return result;
    }

    /// <summary>
    /// Decodes the server-reported <c>report_value</c> (mutation cost in milliseconds) from a status's
    /// <c>extra_info</c> map, returning 0 when absent or unparseable. Mirrors the Java SDK's
    /// <c>getCost</c> (and PyMilvus's <c>get_cost_from_status</c>).
    /// </summary>
    public static long GetReportValue(Grpc.Status? status)
        => status is null ? 0L : GetExtraInfoLong(status, "report_value");

    /// <summary>
    /// Decodes a <see cref="long" /> value from a status's <c>extra_info</c> map, returning 0 when absent or
    /// unparseable. Used for the search metrics the server reports alongside results (scanned bytes).
    /// </summary>
    public static long GetExtraInfoLong(Grpc.Status? status, string key)
    {
        if (status is null || !status.ExtraInfo.TryGetValue(key, out string? value) || string.IsNullOrWhiteSpace(value))
        {
            return 0L;
        }

        return long.TryParse(value.Trim(), System.Globalization.NumberStyles.Integer,
            System.Globalization.CultureInfo.InvariantCulture, out long parsed) ? parsed : 0L;
    }

    /// <summary>
    /// Decodes a <see cref="float" /> value from a status's <c>extra_info</c> map, returning <c>null</c> when
    /// absent or unparseable. Used for the search cache-hit ratio the server reports alongside results.
    /// </summary>
    public static float? GetExtraInfoFloat(Grpc.Status? status, string key)
    {
        if (status is null || !status.ExtraInfo.TryGetValue(key, out string? value) || string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return float.TryParse(value.Trim(), System.Globalization.NumberStyles.Float,
            System.Globalization.CultureInfo.InvariantCulture, out float parsed) ? parsed : null;
    }

    private static FieldData FromGrpcFieldData(Grpc.FieldData fieldData)
    {
        switch (fieldData.FieldCase)
        {
            case Grpc.FieldData.FieldOneofCase.Vectors:
                return ConvertVectors(fieldData);

            case Grpc.FieldData.FieldOneofCase.Scalars:
                bool nullable = fieldData.ValidData.Count > 0;
                return fieldData.Scalars.DataCase switch
                {
                    Grpc.ScalarField.DataOneofCase.BoolData => nullable
                        ? FieldData.Create(fieldData.FieldName, ExpandNullable(fieldData.Scalars.BoolData.Data.Select(x => (bool?)x).ToList(), fieldData.ValidData, null))
                        : FieldData.Create(fieldData.FieldName, fieldData.Scalars.BoolData.Data),
                    Grpc.ScalarField.DataOneofCase.IntData => ConvertIntData(fieldData),
                    Grpc.ScalarField.DataOneofCase.LongData => nullable
                        ? FieldData.Create(fieldData.FieldName, ExpandNullable(fieldData.Scalars.LongData.Data.Select(x => (long?)x).ToList(), fieldData.ValidData, null))
                        : FieldData.Create(fieldData.FieldName, fieldData.Scalars.LongData.Data),
                    Grpc.ScalarField.DataOneofCase.FloatData => nullable
                        ? FieldData.Create(fieldData.FieldName, ExpandNullable(fieldData.Scalars.FloatData.Data.Select(x => (float?)x).ToList(), fieldData.ValidData, null))
                        : FieldData.Create(fieldData.FieldName, fieldData.Scalars.FloatData.Data),
                    Grpc.ScalarField.DataOneofCase.DoubleData => nullable
                        ? FieldData.Create(fieldData.FieldName, ExpandNullable(fieldData.Scalars.DoubleData.Data.Select(x => (double?)x).ToList(), fieldData.ValidData, null))
                        : FieldData.Create(fieldData.FieldName, fieldData.Scalars.DoubleData.Data),
                    Grpc.ScalarField.DataOneofCase.StringData => ConvertStringData(fieldData),
                    Grpc.ScalarField.DataOneofCase.TimestamptzData => ConvertTimestamptzData(fieldData),
                    Grpc.ScalarField.DataOneofCase.JsonData => FieldData.CreateJson(
                        fieldData.FieldName, fieldData.Scalars.JsonData.Data.Select(p => p.ToStringUtf8()).ToList()),
                    Grpc.ScalarField.DataOneofCase.ArrayData => ConvertArray(fieldData),
                    _ => throw new NotSupportedException($"{fieldData.Scalars.DataCase} not supported")
                };

            default:
                throw new NotSupportedException($"{fieldData.FieldCase} not supported");
        }
    }

    // Rebuilds the logical row sequence from the server's field data plus the per-row valid_data mask.
    // Milvus sends scalar (and array) columns positionally: one data slot per row (zero/empty-filled for
    // null rows) with a same-length valid_data mask, while only vector columns advance a compacted cursor
    // (valid-only values) against the same mask. The two encodings are told apart by length: when the data
    // and mask lengths match the column is positional; otherwise it is compacted and the false positions
    // need null placeholders re-inserted. This matches the Java SDK's FieldDataWrapper.setNoneData, which
    // masks positionally only when validData.size() == data.size().
    private static IReadOnlyList<T> ExpandNullable<T>(
        IReadOnlyList<T> data, IReadOnlyList<bool> validData, T nullValue)
    {
        if (validData.Count == 0)
        {
            return data;
        }

        bool positional = validData.Count == data.Count;
        var expanded = new List<T>(validData.Count);
        int dataIndex = 0;
        for (int i = 0; i < validData.Count; i++)
        {
            if (validData[i])
            {
                expanded.Add(positional ? data[i] : data[dataIndex++]);
            }
            else
            {
                expanded.Add(nullValue);
            }
        }

        return expanded;
    }

    // The server encodes Int8/Int16/Int32 scalar fields through the same IntData (int32) array; branch on the
    // field's declared type so a decoded Int8/Int16/Int32 column round-trips with its original DataType instead
    // of being widened to Int64 (matching the V1 SDK's behavior).
    private static FieldData ConvertIntData(Grpc.FieldData fieldData)
    {
        IReadOnlyList<int> data = fieldData.Scalars.IntData.Data;
        bool nullable = fieldData.ValidData.Count > 0;
        return (fieldData.Type, nullable) switch
        {
            (Grpc.DataType.Int8, false) => FieldData.Create(fieldData.FieldName, data.Select(x => (sbyte)x).ToList()),
            (Grpc.DataType.Int8, true) => FieldData.Create(fieldData.FieldName, ExpandNullable(data.Select(x => (sbyte?)x).ToList(), fieldData.ValidData, null)),
            (Grpc.DataType.Int16, false) => FieldData.Create(fieldData.FieldName, data.Select(x => (short)x).ToList()),
            (Grpc.DataType.Int16, true) => FieldData.Create(fieldData.FieldName, ExpandNullable(data.Select(x => (short?)x).ToList(), fieldData.ValidData, null)),
            (Grpc.DataType.Int32, false) => FieldData.Create(fieldData.FieldName, data),
            (Grpc.DataType.Int32, true) => FieldData.Create(fieldData.FieldName, ExpandNullable(data.Select(x => (int?)x).ToList(), fieldData.ValidData, null)),
            _ => nullable
                ? FieldData.Create(fieldData.FieldName, ExpandNullable(data.Select(x => (long?)x).ToList(), fieldData.ValidData, null))
                : FieldData.Create(fieldData.FieldName, data.Select(x => (long)x).ToList())
        };
    }

    // Decodes string-slot scalar columns. Timestamptz fields travel as ISO-8601 strings in the string_data
    // slot (the proxy's timestamptzUTC2IsoStr rewrite); decode them with their real data type rather than
    // widening to VarChar, mirroring the V1 decoder which branches on Grpc.DataType.Timestamptz.
    private static FieldData<string> ConvertStringData(Grpc.FieldData fieldData)
    {
        bool nullable = fieldData.ValidData.Count > 0;
        bool isTimestamptz = fieldData.Type == Grpc.DataType.Timestamptz;
        IReadOnlyList<string> data = fieldData.Scalars.StringData.Data;
        if (!nullable)
        {
            return isTimestamptz
                ? new FieldData<string>(fieldData.FieldName, data, DataType.Timestamptz)
                : FieldData.CreateVarChar(fieldData.FieldName, data);
        }

        // A nullable string column expands to a List<string?>; the string generic parameter carries the
        // reference-type nulls in the same list, so cast to IReadOnlyList<string> for the DTO constructor.
        IReadOnlyList<string?> expanded = ExpandNullable(
            data.Select(x => (string?)x).ToList(), fieldData.ValidData, null);
        return isTimestamptz
            ? new FieldData<string>(fieldData.FieldName, (IReadOnlyList<string>)(object)expanded, DataType.Timestamptz)
            : new FieldData<string>(fieldData.FieldName, (IReadOnlyList<string>)(object)expanded);
    }

    // Decodes the native TimestamptzArray slot (int64 epoch milliseconds) when a server emits it, converting
    // to the ISO-8601 string representation used everywhere else in the SDK, instead of falling through to
    // NotSupportedException.
    private static FieldData<string> ConvertTimestamptzData(Grpc.FieldData fieldData)
    {
        IReadOnlyList<string> data = fieldData.Scalars.TimestamptzData.Data
            .Select(ts => DateTimeOffset.FromUnixTimeMilliseconds(ts).ToString("O", System.Globalization.CultureInfo.InvariantCulture))
            .ToList();
        return new FieldData<string>(fieldData.FieldName, data, DataType.Timestamptz);
    }

    private static FieldData ConvertVectors(Grpc.FieldData fieldData)
    {
        Grpc.VectorField vectors = fieldData.Vectors;
        return vectors.DataCase switch
        {
            Grpc.VectorField.DataOneofCase.FloatVector
                => FieldData.CreateFloatVector(fieldData.FieldName,
                    ExpandNullable(ChunkFloats(vectors.FloatVector.Data, (int)vectors.Dim), fieldData.ValidData, ReadOnlyMemory<float>.Empty)),

            Grpc.VectorField.DataOneofCase.Float16Vector => ConvertFloat16Vectors(fieldData, vectors),

            Grpc.VectorField.DataOneofCase.Bfloat16Vector => ConvertBFloat16Vectors(fieldData, vectors),

            Grpc.VectorField.DataOneofCase.Int8Vector => ConvertInt8Vectors(fieldData, vectors),

            Grpc.VectorField.DataOneofCase.BinaryVector => ConvertBinaryVectors(fieldData, vectors),

            Grpc.VectorField.DataOneofCase.SparseFloatVector => ConvertSparseVectors(fieldData, vectors),

            _ => throw new NotSupportedException($"VectorField.DataOneofCase.{vectors.DataCase} not supported")
        };
    }

    private static ReadOnlyMemory<float>[] ChunkFloats(RepeatedField<float> data, int dim)
    {
        int vectorCount = data.Count / dim;
        var vectors = new ReadOnlyMemory<float>[vectorCount];
        for (int i = 0; i < vectorCount; i++)
        {
            var vector = new float[dim];
            for (int j = 0; j < dim; j++)
            {
                vector[j] = data[i * dim + j];
            }
            vectors[i] = vector;
        }

        return vectors;
    }

    private static BinaryVectorFieldData ConvertBinaryVectors(Grpc.FieldData fieldData, Grpc.VectorField vectors)
    {
        int dim = (int)vectors.Dim;
        int bytesPerVector = dim / 8;
        byte[] raw = vectors.BinaryVector.ToByteArray();
        var rows = new ReadOnlyMemory<byte>[raw.Length / bytesPerVector];
        for (int i = 0; i < rows.Length; i++)
        {
            rows[i] = raw.AsMemory(i * bytesPerVector, bytesPerVector);
        }

        return FieldData.CreateBinaryVectors(fieldData.FieldName,
            ExpandNullable(rows, fieldData.ValidData, ReadOnlyMemory<byte>.Empty));
    }

    private static Float16VectorFieldData ConvertFloat16Vectors(Grpc.FieldData fieldData, Grpc.VectorField vectors)
    {
        int dim = (int)vectors.Dim;
        byte[] raw = vectors.Float16Vector.ToByteArray();
        var rows = new ReadOnlyMemory<ushort>[raw.Length / (dim * 2)];
        for (int i = 0; i < rows.Length; i++)
        {
            var row = new ushort[dim];
            int offset = i * dim * 2;
            for (int j = 0; j < dim; j++)
            {
                row[j] = (ushort)(raw[offset + j * 2] | (raw[offset + j * 2 + 1] << 8));
            }

            rows[i] = row;
        }

        return new Float16VectorFieldData(fieldData.FieldName,
            ExpandNullable(rows, fieldData.ValidData, ReadOnlyMemory<ushort>.Empty));
    }

    private static BFloat16VectorFieldData ConvertBFloat16Vectors(Grpc.FieldData fieldData, Grpc.VectorField vectors)
    {
        int dim = (int)vectors.Dim;
        byte[] raw = vectors.Bfloat16Vector.ToByteArray();
        var rows = new ReadOnlyMemory<ushort>[raw.Length / (dim * 2)];
        for (int i = 0; i < rows.Length; i++)
        {
            var row = new ushort[dim];
            int offset = i * dim * 2;
            for (int j = 0; j < dim; j++)
            {
                row[j] = (ushort)(raw[offset + j * 2] | (raw[offset + j * 2 + 1] << 8));
            }

            rows[i] = row;
        }

        return new BFloat16VectorFieldData(fieldData.FieldName,
            ExpandNullable(rows, fieldData.ValidData, ReadOnlyMemory<ushort>.Empty));
    }

    private static Int8VectorFieldData ConvertInt8Vectors(Grpc.FieldData fieldData, Grpc.VectorField vectors)
    {
        int dim = (int)vectors.Dim;
        byte[] raw = vectors.Int8Vector.ToByteArray();
        var rows = new ReadOnlyMemory<sbyte>[raw.Length / dim];
        for (int i = 0; i < rows.Length; i++)
        {
            var row = new sbyte[dim];
            for (int j = 0; j < dim; j++)
            {
                row[j] = unchecked((sbyte)raw[i * dim + j]);
            }

            rows[i] = row;
        }

        return new Int8VectorFieldData(fieldData.FieldName,
            ExpandNullable(rows, fieldData.ValidData, ReadOnlyMemory<sbyte>.Empty));
    }

    private static SparseFloatVectorFieldData ConvertSparseVectors(Grpc.FieldData fieldData, Grpc.VectorField vectors)
    {
        var sparseVectors = new MilvusSparseVector<float>[vectors.SparseFloatVector.Contents.Count];
        for (int i = 0; i < sparseVectors.Length; i++)
        {
            sparseVectors[i] = MilvusSparseVector<float>.FromBytes(vectors.SparseFloatVector.Contents[i].Span);
        }

        return FieldData.CreateSparseFloatVector(fieldData.FieldName,
            ExpandNullable(sparseVectors, fieldData.ValidData, default(MilvusSparseVector<float>)));
    }

    private static FieldData ConvertArray(Grpc.FieldData fieldData)
    {
        Grpc.ArrayArray arrayData = fieldData.Scalars.ArrayData;
        return arrayData.ElementType switch
        {
            Grpc.DataType.Bool => ConvertArrayElements<bool>(fieldData, arrayData, x => x.BoolData?.Data ?? []),
            Grpc.DataType.Int8 => ConvertArrayElements<sbyte>(fieldData, arrayData, x => x.IntData?.Data.Select(v => (sbyte)v) ?? []),
            Grpc.DataType.Int16 => ConvertArrayElements<short>(fieldData, arrayData, x => x.IntData?.Data.Select(v => (short)v) ?? []),
            Grpc.DataType.Int32 => ConvertArrayElements<int>(fieldData, arrayData, x => x.IntData?.Data ?? []),
            Grpc.DataType.Int64 => ConvertArrayElements<long>(fieldData, arrayData, x => x.LongData?.Data ?? []),
            Grpc.DataType.Float => ConvertArrayElements<float>(fieldData, arrayData, x => x.FloatData?.Data ?? []),
            Grpc.DataType.Double => ConvertArrayElements<double>(fieldData, arrayData, x => x.DoubleData?.Data ?? []),
            Grpc.DataType.String or Grpc.DataType.VarChar => ConvertArrayElements<string>(fieldData, arrayData, x => x.StringData?.Data ?? []),
            _ => throw new NotSupportedException($"Array element type {arrayData.ElementType} not supported")
        };
    }

    // Converts an Array column into one FieldData row per entity, expanding the compact arrayData.Data against
    // ValidData so null array rows keep the field row-aligned with its sibling fields (like the scalar/vector
    // branches). A null row is emitted for each invalid position. The concrete ArrayFieldData<TElement> type
    // preserves the element type (Int8/Int16 stay narrow instead of widening to Int32).
    private static ArrayFieldData<T> ConvertArrayElements<T>(
        Grpc.FieldData fieldData, Grpc.ArrayArray arrayData, Func<Grpc.ScalarField, IEnumerable<T>> selector)
    {
        List<IReadOnlyList<T>> rows = arrayData.Data
            .Select(x => (IReadOnlyList<T>)selector(x).ToList())
            .ToList();

        IReadOnlyList<IReadOnlyList<T>?> expanded =
            (IReadOnlyList<IReadOnlyList<T>?>)(object)ExpandNullable(rows, fieldData.ValidData, null);
        return new ArrayFieldData<T>(fieldData.FieldName, expanded);
    }
}
