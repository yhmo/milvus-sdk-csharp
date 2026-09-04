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
public class DmlSystemTests : SystemTestBase
{
    public DmlSystemTests(MilvusV2Fixture fixture)
        : base(fixture)
    {
    }

    [Fact]
    public async Task Insert_rows_round_trips_dynamic_and_nullable_data()
    {
        const string collectionName = nameof(Insert_rows_round_trips_dynamic_and_nullable_data);

        await ResetCollectionAsync(collectionName);

        await Client.CreateCollectionAsync(new CreateCollectionReq
        {
            CollectionName = collectionName,
            Schema = new CollectionSchema
            {
                EnableDynamicFields = true,
                Fields =
                {
                    new FieldSchema("id", DataType.Int64, isPrimaryKey: true, autoId: true),
                    FieldSchema.CreateVarchar("title", maxLength: 200),
                    new FieldSchema("embedding", DataType.FloatVector) { Dimension = 4, Nullable = true },
                    new FieldSchema("score", DataType.Float) { Nullable = true }
                }
            }
        }, TestContext.Current.CancellationToken);

        await Client.CreateIndexAsync(new CreateIndexReq
        {
            CollectionName = collectionName,
            FieldName = "embedding",
            IndexType = IndexType.Flat,
            MetricType = SimilarityMetricType.L2
        }, TestContext.Current.CancellationToken);

        // Row-based insert with a nullable vector (row 0 has a null embedding) and a dynamic key ("color").
        MutationResp mutation = await Client.InsertAsync(new InsertReq
        {
            CollectionName = collectionName,
            RowsData =
            [
                new Dictionary<string, object?>
                {
                    ["title"] = "first",
                    ["embedding"] = null,
                    ["score"] = 1.0f,
                    ["color"] = "red"
                },
                new Dictionary<string, object?>
                {
                    ["title"] = "second",
                    ["embedding"] = new[] { 0.5f, 0.1f, 0.2f, 0.3f },
                    ["score"] = 2.0,
                    ["color"] = "blue"
                }
            ]
        }, TestContext.Current.CancellationToken);

        Assert.Equal(2, mutation.InsertCount);

        await Client.LoadCollectionAsync(new LoadCollectionReq { CollectionName = collectionName },
            TestContext.Current.CancellationToken);

        await WaitForLoadAsync(collectionName);

        // The row with a vector is queryable; the auto-id pk is generated and the dynamic $meta key stored.
        QueryResp query = await Client.QueryAsync(new QueryReq
        {
            CollectionName = collectionName,
            Expression = "title == \"second\"",
            Parameters = new QueryParameters { OutputFields = { "id", "title" } }
        }, TestContext.Current.CancellationToken);

        Assert.NotNull(query.FieldsData);
        FieldData title = query.FieldsData.First(f => f.FieldName == "title");
        Assert.Equal(1, title.RowCount);
        Assert.Equal("second", title.GetValueAsObject(0));
        Assert.True(query.FieldsData.First(f => f.FieldName == "id").GetValueAsObject(0) is long);

        await ResetCollectionAsync(collectionName);
    }

    [Fact]
    public async Task Upsert_round_trips_row_data()
    {
        const string collectionName = nameof(Upsert_round_trips_row_data);

        await ResetCollectionAsync(collectionName);

        await Client.CreateCollectionAsync(new CreateCollectionReq
        {
            CollectionName = collectionName,
            Schema = new CollectionSchema
            {
                Fields =
                {
                    new FieldSchema("id", DataType.Int64, isPrimaryKey: true),
                    FieldSchema.CreateVarchar("name", maxLength: 64),
                    FieldSchema.CreateFloatVector("dummy_vec", dimension: 2)
                }
            }
        }, TestContext.Current.CancellationToken);

        await Client.CreateIndexAsync(new CreateIndexReq
        {
            CollectionName = collectionName,
            FieldName = "dummy_vec",
            IndexType = IndexType.Flat,
            MetricType = SimilarityMetricType.L2
        }, TestContext.Current.CancellationToken);

        await Client.InsertAsync(new InsertReq
        {
            CollectionName = collectionName,
            RowsData =
            [
                new Dictionary<string, object?> { ["id"] = 1L, ["name"] = "a", ["dummy_vec"] = new[] { 0.1f, 0.2f } },
                new Dictionary<string, object?> { ["id"] = 2L, ["name"] = "b", ["dummy_vec"] = new[] { 0.3f, 0.4f } }
            ]
        }, TestContext.Current.CancellationToken);

        // Upsert overwrites row 1 and inserts a new row 3.
        MutationResp upsert = await Client.UpsertAsync(new UpsertReq
        {
            CollectionName = collectionName,
            RowsData =
            [
                new Dictionary<string, object?> { ["id"] = 1L, ["name"] = "a-updated", ["dummy_vec"] = new[] { 0.1f, 0.2f } },
                new Dictionary<string, object?> { ["id"] = 3L, ["name"] = "c", ["dummy_vec"] = new[] { 0.5f, 0.6f } }
            ]
        }, TestContext.Current.CancellationToken);

        Assert.Equal(2, upsert.UpsertCount);

        await Client.LoadCollectionAsync(new LoadCollectionReq { CollectionName = collectionName },
            TestContext.Current.CancellationToken);
        await WaitForLoadAsync(collectionName);

        QueryResp query = await Client.QueryAsync(new QueryReq
        {
            CollectionName = collectionName,
            Expression = "id == 1",
            Parameters = new QueryParameters { OutputFields = { "name" } }
        }, TestContext.Current.CancellationToken);

        FieldData name = query.FieldsData.Single(f => f.FieldName == "name");
        Assert.Equal("a-updated", name.GetValueAsObject(0));

        await ResetCollectionAsync(collectionName);
    }
}
