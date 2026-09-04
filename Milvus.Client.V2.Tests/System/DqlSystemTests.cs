using Xunit;

using Milvus.Client.V2.Requests.Collection;
using Milvus.Client.V2.Requests.Dml;
using Milvus.Client.V2.Requests.Dql;
using Milvus.Client.V2.Requests.Index;
using Milvus.Client.V2.Responses.Dml;
using Milvus.Client.V2.Responses.Dql;
using Milvus.Client.V2.Types;

namespace Milvus.Client.V2.Tests;

[Trait("Category", "System")]
[Collection(MilvusV2Collection.Name)]
public class DqlSystemTests : SystemTestBase
{
    public DqlSystemTests(MilvusV2Fixture fixture)
        : base(fixture)
    {
    }

    [Fact]
    public async Task Insert_Search_Query_full_flow()
    {
        const string collectionName = nameof(Insert_Search_Query_full_flow);

        await ResetCollectionAsync(collectionName);

        await Client.CreateCollectionAsync(new CreateCollectionReq
        {
            CollectionName = collectionName,
            Schema = new CollectionSchema
            {
                Fields =
                {
                    new FieldSchema("id", DataType.Int64, isPrimaryKey: true),
                    FieldSchema.CreateVarchar("title", maxLength: 200),
                    FieldSchema.CreateFloatVector("embedding", dimension: 4)
                }
            }
        }, TestContext.Current.CancellationToken);

        await Client.CreateIndexAsync(new CreateIndexReq
        {
            CollectionName = collectionName,
            Indexes = [ new IndexParam("embedding", null, IndexType.Flat, SimilarityMetricType.L2) ]
        }, TestContext.Current.CancellationToken);

        MutationResp mutation = await Client.InsertAsync(new InsertReq
        {
            CollectionName = collectionName,
            ColumnsData =
            [
                FieldData.Create("id", new long[] { 1, 2, 3 }),
                FieldData.CreateVarChar("title", new[] { "first", "second", "third" }),
                FieldData.CreateFloatVector("embedding", new[]
                {
                    new ReadOnlyMemory<float>(new[] { 0.1f, 0.2f, 0.3f, 0.4f }),
                    new ReadOnlyMemory<float>(new[] { 0.5f, 0.1f, 0.2f, 0.3f }),
                    new ReadOnlyMemory<float>(new[] { 0.9f, 0.8f, 0.7f, 0.6f })
                })
            ]
        }, TestContext.Current.CancellationToken);

        Assert.Equal(3, mutation.InsertCount);

        await Client.LoadCollectionAsync(new LoadCollectionReq { CollectionName = collectionName },
            TestContext.Current.CancellationToken);

        await WaitForLoadAsync(collectionName);

        SearchResp search = await Client.SearchAsync(new SearchReq
        {
            CollectionName = collectionName,
            VectorFieldName = "embedding",
            Vectors = new[] { new ReadOnlyMemory<float>(new[] { 0.1f, 0.2f, 0.3f, 0.4f }) },
            MetricType = SimilarityMetricType.L2,
            Limit = 2,
            Parameters = new SearchParameters { OutputFields = { "title" } }
        }, TestContext.Current.CancellationToken);

        Assert.NotNull(search.Ids.LongIds);
        Assert.Equal(1L, search.Ids.LongIds![0]);
        Assert.NotEmpty(search.Scores);

        QueryResp query = await Client.QueryAsync(new QueryReq
        {
            CollectionName = collectionName,
            Expression = "id in [1, 2]",
            Parameters = new QueryParameters { OutputFields = { "id", "title" } }
        }, TestContext.Current.CancellationToken);

        var idField = (FieldData<long>)query.FieldsData.Single(f => f.FieldName == "id");
        Assert.Equal(2, idField.Data.Count);

        await ResetCollectionAsync(collectionName);
    }

    [Fact]
    public async Task Query_with_expression_and_dynamic_filters()
    {
        const string collectionName = nameof(Query_with_expression_and_dynamic_filters);

        await ResetCollectionAsync(collectionName);

        await Client.CreateCollectionAsync(new CreateCollectionReq
        {
            CollectionName = collectionName,
            Schema = new CollectionSchema
            {
                Fields =
                {
                    new FieldSchema("id", DataType.Int64, isPrimaryKey: true),
                    FieldSchema.CreateVarchar("title", maxLength: 200),
                    FieldSchema.CreateFloatVector("dummy_vec", dimension: 2)
                }
            }
        }, TestContext.Current.CancellationToken);

        await Client.CreateIndexAsync(new CreateIndexReq
        {
            CollectionName = collectionName,
            Indexes = [ new IndexParam("dummy_vec", null, IndexType.Flat, SimilarityMetricType.L2) ]
        }, TestContext.Current.CancellationToken);

        await Client.InsertAsync(new InsertReq
        {
            CollectionName = collectionName,
            RowsData =
            [
                new Dictionary<string, object?> { ["id"] = 1L, ["title"] = "alpha", ["dummy_vec"] = new[] { 0.1f, 0.2f } },
                new Dictionary<string, object?> { ["id"] = 2L, ["title"] = "beta", ["dummy_vec"] = new[] { 0.3f, 0.4f } },
                new Dictionary<string, object?> { ["id"] = 3L, ["title"] = "gamma", ["dummy_vec"] = new[] { 0.5f, 0.6f } }
            ]
        }, TestContext.Current.CancellationToken);

        await Client.LoadCollectionAsync(new LoadCollectionReq { CollectionName = collectionName },
            TestContext.Current.CancellationToken);
        await WaitForLoadAsync(collectionName);

        QueryResp query = await Client.QueryAsync(new QueryReq
        {
            CollectionName = collectionName,
            Expression = "title in [\"alpha\", \"gamma\"] and id >= 2",
            Parameters = new QueryParameters { OutputFields = { "id", "title" } }
        }, TestContext.Current.CancellationToken);

        FieldData id = query.FieldsData.Single(f => f.FieldName == "id");
        Assert.Equal(1, id.RowCount);
        Assert.Equal(3L, id.GetValueAsObject(0));

        await ResetCollectionAsync(collectionName);
    }

    [Fact]
    public async Task Non_float_vector_types_round_trip_insert_search_query()
    {
        const string collectionName = nameof(Non_float_vector_types_round_trip_insert_search_query);

        await ResetCollectionAsync(collectionName);

        // A collection with one field per supported non-float vector type.
        await Client.CreateCollectionAsync(new CreateCollectionReq
        {
            CollectionName = collectionName,
            Schema = new CollectionSchema
            {
                Fields =
                {
                    new FieldSchema("id", DataType.Int64, isPrimaryKey: true),
                    new FieldSchema("fp16", DataType.Float16Vector) { Dimension = 2 },
                    new FieldSchema("bf16", DataType.BFloat16Vector) { Dimension = 2 },
                    new FieldSchema("bin", DataType.BinaryVector) { Dimension = 16 },
                    new FieldSchema("i8", DataType.Int8Vector) { Dimension = 2 },
                    new FieldSchema("sparse", DataType.SparseFloatVector)
                }
            }
        }, TestContext.Current.CancellationToken);

        // Index each vector field with a metric appropriate to its type. Each needs a distinct index name
        // (the server allows at most one index per (field, index-name) and index names are global).
        await Client.CreateIndexAsync(new CreateIndexReq { CollectionName = collectionName, Indexes = [ new IndexParam("fp16", "idx_fp16", IndexType.Hnsw, SimilarityMetricType.Cosine) ] }, TestContext.Current.CancellationToken);
        await Client.CreateIndexAsync(new CreateIndexReq { CollectionName = collectionName, Indexes = [ new IndexParam("bf16", "idx_bf16", IndexType.Hnsw, SimilarityMetricType.Cosine) ] }, TestContext.Current.CancellationToken);
        await Client.CreateIndexAsync(new CreateIndexReq { CollectionName = collectionName, Indexes = [ new IndexParam("bin", "idx_bin", IndexType.BinFlat, SimilarityMetricType.Hamming) ] }, TestContext.Current.CancellationToken);
        await Client.CreateIndexAsync(new CreateIndexReq { CollectionName = collectionName, Indexes = [ new IndexParam("i8", "idx_i8", IndexType.Hnsw, SimilarityMetricType.Cosine) ] }, TestContext.Current.CancellationToken);
        await Client.CreateIndexAsync(new CreateIndexReq { CollectionName = collectionName, Indexes = [ new IndexParam("sparse", "idx_sparse", IndexType.SparseInvertedIndex, SimilarityMetricType.Ip) ] }, TestContext.Current.CancellationToken);

        // FP16 bit patterns for 1.0f and 0.0f (little-endian), BFloat16 same; binary 2 bytes; int8 2 values; sparse 2 non-zero entries.
        const ushort fp16One = 0x3C00; // 1.0f
        const ushort bf16One = 0x3F80; // 1.0f in bfloat16
        await Client.InsertAsync(new InsertReq
        {
            CollectionName = collectionName,
            RowsData =
            [
                new Dictionary<string, object?>
                {
                    ["id"] = 1L,
                    ["fp16"] = new[] { fp16One, (ushort)0 },
                    ["bf16"] = new[] { bf16One, (ushort)0 },
                    ["bin"] = new byte[] { 0b10000000, 0b00000000 },
                    ["i8"] = new sbyte[] { 1, 0 },
                    ["sparse"] = new Dictionary<long, float> { [1] = 1.0f, [5] = 0.5f }
                },
                new Dictionary<string, object?>
                {
                    ["id"] = 2L,
                    ["fp16"] = new[] { (ushort)0, (ushort)0 },
                    ["bf16"] = new[] { (ushort)0, (ushort)0 },
                    ["bin"] = new byte[] { 0b00000000, 0b00000000 },
                    ["i8"] = new sbyte[] { 0, 0 },
                    ["sparse"] = new Dictionary<long, float> { [10] = 1.0f }
                }
            ]
        }, TestContext.Current.CancellationToken);

        await Client.LoadCollectionAsync(new LoadCollectionReq { CollectionName = collectionName },
            TestContext.Current.CancellationToken);
        await WaitForLoadAsync(collectionName);

        // Search each vector type with a query vector of the same shape; all should return row 1 as top hit.
        SearchResp fp16Search = await Client.SearchAsync(new SearchReq
        {
            CollectionName = collectionName,
            VectorFieldName = "fp16",
            HalfVectors = new[] { new ReadOnlyMemory<ushort>(new[] { fp16One, (ushort)0 }) },
            MetricType = SimilarityMetricType.Cosine,
            Limit = 1
        }, TestContext.Current.CancellationToken);
        Assert.Equal(1L, fp16Search.Ids.LongIds![0]);

        SearchResp bf16Search = await Client.SearchAsync(new SearchReq
        {
            CollectionName = collectionName,
            VectorFieldName = "bf16",
            BFloat16Vectors = new[] { new ReadOnlyMemory<ushort>(new[] { bf16One, (ushort)0 }) },
            MetricType = SimilarityMetricType.Cosine,
            Limit = 1
        }, TestContext.Current.CancellationToken);
        Assert.Equal(1L, bf16Search.Ids.LongIds![0]);

        SearchResp binSearch = await Client.SearchAsync(new SearchReq
        {
            CollectionName = collectionName,
            VectorFieldName = "bin",
            BinaryVectors = new[] { new ReadOnlyMemory<byte>(new byte[] { 0b10000000, 0b00000000 }) },
            MetricType = SimilarityMetricType.Hamming,
            Limit = 1
        }, TestContext.Current.CancellationToken);
        Assert.Equal(1L, binSearch.Ids.LongIds![0]);

        SearchResp i8Search = await Client.SearchAsync(new SearchReq
        {
            CollectionName = collectionName,
            VectorFieldName = "i8",
            Int8Vectors = new[] { new ReadOnlyMemory<sbyte>(new sbyte[] { 1, 0 }) },
            MetricType = SimilarityMetricType.Cosine,
            Limit = 1
        }, TestContext.Current.CancellationToken);
        Assert.Equal(1L, i8Search.Ids.LongIds![0]);

        SearchResp sparseSearch = await Client.SearchAsync(new SearchReq
        {
            CollectionName = collectionName,
            VectorFieldName = "sparse",
            SparseVectors = new[] { new MilvusSparseVector<float>(new[] { 1 }, new[] { 1.0f }) },
            MetricType = SimilarityMetricType.Ip,
            Limit = 1
        }, TestContext.Current.CancellationToken);
        Assert.Equal(1L, sparseSearch.Ids.LongIds![0]);

        // Query back one row and verify the non-float vector columns decode.
        QueryResp query = await Client.QueryAsync(new QueryReq
        {
            CollectionName = collectionName,
            Expression = "id == 1",
            Parameters = new QueryParameters { OutputFields = { "fp16", "bf16", "bin", "i8", "sparse" } }
        }, TestContext.Current.CancellationToken);

        var fp16Field = Assert.IsType<Float16VectorFieldData>(query.FieldsData.Single(f => f.FieldName == "fp16"));
        Assert.Equal(fp16One, fp16Field.Data[0].Span[0]);
        var bf16Field = Assert.IsType<BFloat16VectorFieldData>(query.FieldsData.Single(f => f.FieldName == "bf16"));
        Assert.Equal(bf16One, bf16Field.Data[0].Span[0]);
        var binField = Assert.IsType<BinaryVectorFieldData>(query.FieldsData.Single(f => f.FieldName == "bin"));
        Assert.Equal((byte)0b10000000, binField.Data[0].Span[0]);
        var i8Field = Assert.IsType<Int8VectorFieldData>(query.FieldsData.Single(f => f.FieldName == "i8"));
        Assert.Equal((sbyte)1, i8Field.Data[0].Span[0]);
        var sparseField = Assert.IsType<SparseFloatVectorFieldData>(query.FieldsData.Single(f => f.FieldName == "sparse"));
        Assert.Equal(2, sparseField.Data[0].Indices.Length);

        await ResetCollectionAsync(collectionName);
    }
}
