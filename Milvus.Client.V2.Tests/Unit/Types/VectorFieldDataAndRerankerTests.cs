using Xunit;

using Milvus.Client.V2.Types;

namespace Milvus.Client.V2.Tests.Unit.Types;

[Trait("Category", "Unit")]
public class VectorFieldDataAndRerankerTests
{
    [Fact]
    public void BinaryVectorFieldData_serializes_bytes_and_bit_dim()
    {
        var field = FieldData.CreateBinaryVectors("vec", new[]
        {
            new ReadOnlyMemory<byte>(new byte[] { 0x0F, 0xF0 }),
            new ReadOnlyMemory<byte>(new byte[] { 0xAA, 0x55 })
        });

        Grpc.FieldData grpc = field.ToGrpcFieldData();

        Assert.Equal(Grpc.DataType.BinaryVector, grpc.Type);
        Assert.Equal(16, grpc.Vectors.Dim); // 2 bytes * 8 bits per row
        Assert.Equal(new byte[] { 0x0F, 0xF0, 0xAA, 0x55 }, grpc.Vectors.BinaryVector.ToByteArray());
    }

    [Fact]
    public void SparseFloatVectorFieldData_serializes_rows_and_max_dim()
    {
        var field = FieldData.CreateSparseFloatVector("vec", new[]
        {
            new MilvusSparseVector<float>(new[] { 0, 3 }, new[] { 1.5f, 2.5f }),
            new MilvusSparseVector<float>(new[] { 5 }, new[] { 3.5f })
        });

        Grpc.FieldData grpc = field.ToGrpcFieldData();

        Assert.Equal(Grpc.DataType.SparseFloatVector, grpc.Type);
        Assert.Equal(2, grpc.Vectors.SparseFloatVector.Contents.Count);
        Assert.Equal(6, grpc.Vectors.Dim); // max index (5) + 1

        MilvusSparseVector<float> row0 = MilvusSparseVector<float>.FromBytes(grpc.Vectors.SparseFloatVector.Contents[0].Span);
        Assert.Equal(new[] { 0, 3 }, row0.Indices.ToArray());
        Assert.Equal(new[] { 1.5f, 2.5f }, row0.Values.ToArray());

        MilvusSparseVector<float> row1 = MilvusSparseVector<float>.FromBytes(grpc.Vectors.SparseFloatVector.Contents[1].Span);
        Assert.Equal(new[] { 5 }, row1.Indices.ToArray());
        Assert.Equal(new[] { 3.5f }, row1.Values.ToArray());
    }

    [Fact]
    public void Int8VectorFieldData_serializes_rows_as_bytes()
    {
        var field = new Int8VectorFieldData("vec", new[]
        {
            new ReadOnlyMemory<sbyte>(new sbyte[] { 0, -1, 127, -128 }),
            new ReadOnlyMemory<sbyte>(new sbyte[] { 1, 2, -3, 4 })
        });

        Grpc.FieldData grpc = field.ToGrpcFieldData();

        Assert.Equal(Grpc.DataType.Int8Vector, grpc.Type);
        Assert.Equal(4, grpc.Vectors.Dim);
        Assert.Equal(new byte[] { 0x00, 0xFF, 0x7F, 0x80, 0x01, 0x02, 0xFD, 0x04 }, grpc.Vectors.Int8Vector.ToByteArray());
    }

    [Fact]
    public void VectorFieldData_rejects_mismatched_row_dimensions()
    {
        var field = new Int8VectorFieldData("vec", new[]
        {
            new ReadOnlyMemory<sbyte>(new sbyte[] { 0, 1, 2, 3 }),
            new ReadOnlyMemory<sbyte>(new sbyte[] { 4, 5 })
        });

        Assert.Throws<ArgumentException>(() => field.ToGrpcFieldData());
    }

    [Fact]
    public void RrfReranker_uses_default_k_of_60()
    {
        var reranker = new RrfReranker();

        Assert.Equal(60f, reranker.K);

        IReadOnlyList<KeyValuePair<string, string>> param = reranker.ToRankParams();
        Assert.Equal("rrf", param[0].Value);
        Assert.Equal("params", param[1].Key);
        Assert.Contains("\"k\": 60", param[1].Value);
    }

    [Fact]
    public void RrfReranker_rejects_k_below_one()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new RrfReranker(0.5f));
    }

    [Fact]
    public void RrfReranker_rejects_nan_and_infinity_k()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new RrfReranker(float.NaN));
        Assert.Throws<ArgumentOutOfRangeException>(() => new RrfReranker(float.PositiveInfinity));
    }

    [Fact]
    public void WeightedReranker_requires_at_least_one_weight()
    {
        Assert.Throws<ArgumentException>(() => new WeightedReranker());
    }

    [Fact]
    public void WeightedReranker_rejects_nan_and_infinity_weights()
    {
        Assert.Throws<ArgumentException>(() => new WeightedReranker(0.3f, float.NaN));
        Assert.Throws<ArgumentException>(() => new WeightedReranker(float.NegativeInfinity));
    }

    [Fact]
    public void WeightedReranker_maps_params()
    {
        var reranker = new WeightedReranker(0.3f, 0.7f);

        IReadOnlyList<KeyValuePair<string, string>> param = reranker.ToRankParams();
        Assert.Equal("weighted", param[0].Value);
        Assert.Equal("params", param[1].Key);
        Assert.Contains("[0.3, 0.7]", param[1].Value);
    }

    [Fact]
    public void Rerankers_implement_common_interface()
    {
        IReranker rrf = new RrfReranker();
        IReranker weighted = new WeightedReranker(1f);

        Assert.Equal("rrf", rrf.ToRankParams()[0].Value);
        Assert.Equal("weighted", weighted.ToRankParams()[0].Value);
    }
}
