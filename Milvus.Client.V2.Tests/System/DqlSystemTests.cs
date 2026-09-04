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
            FieldName = "embedding",
            IndexType = IndexType.Flat,
            MetricType = SimilarityMetricType.L2
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
            FieldName = "dummy_vec",
            IndexType = IndexType.Flat,
            MetricType = SimilarityMetricType.L2
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
}
