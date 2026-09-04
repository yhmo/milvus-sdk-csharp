using Xunit;

using Milvus.Client.V2;
using Milvus.Client.V2.Requests.Dml;
using Milvus.Client.V2.Responses.Dml;
using Milvus.Client.V2.Types;
using Milvus.Client.V2.Utils;

namespace Milvus.Client.V2.Tests.Integration;

[Trait("Category", "Integration")]
public class DmlTests
{
    [Fact]
    public async Task Insert_forwards_data_and_updates_ts_cache()
    {
        using var server = new MockMilvusServer { Service = { NextMutationTimestamp = 100 } };
        using MilvusClientV2 client = server.CreateClient();

        CollectionTsCache.Instance.Clear();
        MutationResp response = await client.InsertAsync(
            new InsertReq
            {
                CollectionName = "dml_coll",
                Data =
                [
                    FieldData.Create("id", new long[] { 1, 2, 3 }),
                    FieldData.CreateVarChar("name", new[] { "a", "b", "c" })
                ]
            },
            TestContext.Current.CancellationToken);

        Assert.Equal("dml_coll", server.Service.LastInsertedCollection);
        Assert.Equal(3, server.Service.LastInsertedRows);
        Assert.Equal(3, response.InsertCount);
        Assert.Equal(100UL, response.Timestamp);

        // The ts cache should be populated for Session consistency.
        Assert.Equal(100L, CollectionTsCache.Instance.Get(server.Uri, "default", "dml_coll"));
    }

    [Fact]
    public async Task Delete_forwards_expression()
    {
        using var server = new MockMilvusServer();
        using MilvusClientV2 client = server.CreateClient();

        await client.DeleteAsync(
            new DeleteReq { CollectionName = "coll", Expression = "id in [1, 2]" },
            TestContext.Current.CancellationToken);

        Assert.Equal("coll", server.Service.LastDeletedCollection);
        Assert.Equal("id in [1, 2]", server.Service.LastDeleteExpression);
    }

    [Fact]
    public async Task Insert_retries_once_on_schema_mismatch_and_invalidates_schema_cache()
    {
        using var server = new MockMilvusServer { Service = { NextMutationTimestamp = 100 } };
        using MilvusClientV2 client = server.CreateClient();
        CancellationToken ct = TestContext.Current.CancellationToken;

        CollectionTsCache.Instance.Clear();
        SchemaCache.Instance.Clear();
        server.Service.DescribeSchema = BuildSchema();

        // Connect explicitly so the lazy connect does not consume the schema-mismatch flag below.
        await client.ConnectAsync(ct);

        // The first Insert RPC fails with SchemaMismatch; the retry succeeds.
        server.Service.FailNextMutationsWithSchemaMismatch = 1;

        MutationResp response = await client.InsertAsync(
            new InsertReq
            {
                CollectionName = "dml_coll",
                Data = [FieldData.Create("id", new long[] { 1, 2, 3 })]
            },
            ct);
        // Connect + Describe (cache miss) + Insert fail + Describe (cache re-populated after invalidate) + Insert success.
        Assert.Equal(5, server.Service.TotalCalls);
        Assert.Equal(3, response.InsertCount);
        Assert.Equal(100L, CollectionTsCache.Instance.Get(server.Uri, "default", "dml_coll"));
        // The schema cache was invalidated on the mismatch and re-populated by the retry's describe.
        Assert.Equal(1, SchemaCache.Instance.Count);
    }

    [Fact]
    public async Task Insert_propagates_schema_mismatch_when_retry_also_fails()
    {
        using var server = new MockMilvusServer();
        using MilvusClientV2 client = server.CreateClient();

        SchemaCache.Instance.Clear();
        CollectionTsCache.Instance.Clear();

        // Connect explicitly so the lazy connect does not consume the schema-mismatch flag below.
        await client.ConnectAsync(TestContext.Current.CancellationToken);

        server.Service.DescribeSchema = BuildSchema();

        // Both the initial call and the single retry fail with SchemaMismatch.
        server.Service.FailNextMutationsWithSchemaMismatch = 2;

        MilvusException exception = await Assert.ThrowsAsync<MilvusException>(() =>
            client.InsertAsync(
                new InsertReq { CollectionName = "dml_coll", Data = [FieldData.Create("id", new long[] { 1 })] },
                TestContext.Current.CancellationToken));

        Assert.Equal(MilvusErrorCode.SchemaMismatch, exception.ErrorCode);
        // Connect + Describe + Insert + Describe + Insert = exactly one retry, no infinite loop.
        Assert.Equal(5, server.Service.TotalCalls);
    }

    [Fact]
    public async Task Upsert_retries_once_on_schema_mismatch()
    {
        using var server = new MockMilvusServer();
        using MilvusClientV2 client = server.CreateClient();

        SchemaCache.Instance.Clear();
        CollectionTsCache.Instance.Clear();

        // Connect explicitly so the lazy connect does not consume the schema-mismatch flag below.
        await client.ConnectAsync(TestContext.Current.CancellationToken);

        server.Service.DescribeSchema = BuildSchema();

        server.Service.FailNextMutationsWithSchemaMismatch = 1;

        MutationResp response = await client.UpsertAsync(
            new UpsertReq
            {
                CollectionName = "dml_coll",
                Data = [FieldData.Create("id", new long[] { 1, 2, 3 })]
            },
            TestContext.Current.CancellationToken);

        Assert.Equal(5, server.Service.TotalCalls);
        Assert.Equal(3, response.UpsertCount);
    }

    private static CollectionSchema BuildSchema()
    {
        var schema = new CollectionSchema { Name = "dml_coll" };
        schema.Fields.Add(new FieldSchema("id", DataType.Int64, isPrimaryKey: true));
        return schema;
    }
}
