using Xunit;

using Milvus.Client.V2;
using Milvus.Client.V2.Requests.Aliases;
using Milvus.Client.V2.Requests.Database;
using Milvus.Client.V2.Requests.Index;
using Milvus.Client.V2.Responses.Aliases;
using Milvus.Client.V2.Responses.Database;
using Milvus.Client.V2.Responses.Index;
using Milvus.Client.V2.Types;
using Milvus.Client.Grpc;

namespace Milvus.Client.V2.Tests.Integration;

[Trait("Category", "Integration")]
public class AliasDatabaseIndexApiTests
{
    // ---- Alias ----

    [Fact]
    public async Task CreateAlias_forwards_request()
    {
        using var server = new MockMilvusServer();
        using MilvusClientV2 client = server.CreateClient();

        await client.CreateAliasAsync(
            new CreateAliasReq { CollectionName = "coll", Alias = "my_alias" },
            TestContext.Current.CancellationToken);

        CreateAliasRequest request = Assert.IsType<CreateAliasRequest>(server.Service.Requests["CreateAlias"]);
        Assert.Equal("coll", request.CollectionName);
        Assert.Equal("my_alias", request.Alias);
    }

    [Fact]
    public async Task DropAlias_forwards_request()
    {
        using var server = new MockMilvusServer();
        using MilvusClientV2 client = server.CreateClient();

        await client.DropAliasAsync(
            new DropAliasReq { Alias = "my_alias" },
            TestContext.Current.CancellationToken);

        DropAliasRequest request = Assert.IsType<DropAliasRequest>(server.Service.Requests["DropAlias"]);
        Assert.Equal("my_alias", request.Alias);
    }

    [Fact]
    public async Task AlterAlias_forwards_request()
    {
        using var server = new MockMilvusServer();
        using MilvusClientV2 client = server.CreateClient();

        await client.AlterAliasAsync(
            new AlterAliasReq { CollectionName = "other_coll", Alias = "my_alias" },
            TestContext.Current.CancellationToken);

        AlterAliasRequest request = Assert.IsType<AlterAliasRequest>(server.Service.Requests["AlterAlias"]);
        Assert.Equal("other_coll", request.CollectionName);
        Assert.Equal("my_alias", request.Alias);
    }

    [Fact]
    public async Task DescribeAlias_forwards_request()
    {
        using var server = new MockMilvusServer();
        using MilvusClientV2 client = server.CreateClient();

        DescribeAliasResp response = await client.DescribeAliasAsync(
            new DescribeAliasReq { Alias = "my_alias" },
            TestContext.Current.CancellationToken);

        DescribeAliasRequest request = Assert.IsType<DescribeAliasRequest>(server.Service.Requests["DescribeAlias"]);
        Assert.Equal("my_alias", request.Alias);
        Assert.Equal("my_alias", response.Alias);
    }

    [Fact]
    public async Task ListAliases_forwards_request()
    {
        using var server = new MockMilvusServer();
        using MilvusClientV2 client = server.CreateClient();

        await client.ListAliasesAsync(
            new ListAliasesReq { CollectionName = "coll" },
            TestContext.Current.CancellationToken);

        ListAliasesRequest request = Assert.IsType<ListAliasesRequest>(server.Service.Requests["ListAliases"]);
        Assert.Equal("coll", request.CollectionName);
    }

    // ---- Database ----

    [Fact]
    public async Task CreateDatabase_forwards_request()
    {
        using var server = new MockMilvusServer();
        using MilvusClientV2 client = server.CreateClient();

        await client.CreateDatabaseAsync(
            new CreateDatabaseReq { DatabaseName = "db1" },
            TestContext.Current.CancellationToken);

        CreateDatabaseRequest request = Assert.IsType<CreateDatabaseRequest>(server.Service.Requests["CreateDatabase"]);
        Assert.Equal("db1", request.DbName);
    }

    [Fact]
    public async Task DropDatabase_forwards_request()
    {
        using var server = new MockMilvusServer();
        using MilvusClientV2 client = server.CreateClient();

        await client.DropDatabaseAsync(
            new DropDatabaseReq { DatabaseName = "db1" },
            TestContext.Current.CancellationToken);

        DropDatabaseRequest request = Assert.IsType<DropDatabaseRequest>(server.Service.Requests["DropDatabase"]);
        Assert.Equal("db1", request.DbName);
    }

    [Fact]
    public async Task DescribeDatabase_forwards_request()
    {
        using var server = new MockMilvusServer();
        using MilvusClientV2 client = server.CreateClient();

        DescribeDatabaseResp response = await client.DescribeDatabaseAsync(
            new DescribeDatabaseReq { DatabaseName = "db1" },
            TestContext.Current.CancellationToken);

        DescribeDatabaseRequest request = Assert.IsType<DescribeDatabaseRequest>(server.Service.Requests["DescribeDatabase"]);
        Assert.Equal("db1", request.DbName);
        Assert.Equal("db1", response.DatabaseName);
    }

    [Fact]
    public async Task ListDatabases_forwards_request()
    {
        using var server = new MockMilvusServer();
        using MilvusClientV2 client = server.CreateClient();

        await client.ListDatabasesAsync(
            new ListDatabasesReq(),
            TestContext.Current.CancellationToken);

        Assert.IsType<ListDatabasesRequest>(server.Service.Requests["ListDatabases"]);
    }

    [Fact]
    public async Task AlterDatabaseProperties_forwards_request()
    {
        using var server = new MockMilvusServer();
        using MilvusClientV2 client = server.CreateClient();

        await client.AlterDatabasePropertiesAsync(
            new AlterDatabasePropertiesReq
            {
                DatabaseName = "db1",
                Properties = { { "replica.number", "2" } },
                DeleteKeys = new[] { "old_key" }
            },
            TestContext.Current.CancellationToken);

        AlterDatabaseRequest request = Assert.IsType<AlterDatabaseRequest>(server.Service.Requests["AlterDatabase"]);
        Assert.Equal("db1", request.DbName);
        Assert.Contains(request.Properties, p => p.Key == "replica.number" && p.Value == "2");
        Assert.Contains("old_key", request.DeleteKeys);
    }

    [Fact]
    public async Task DropDatabaseProperties_forwards_request()
    {
        using var server = new MockMilvusServer();
        using MilvusClientV2 client = server.CreateClient();

        await client.DropDatabasePropertiesAsync(
            new DropDatabasePropertiesReq
            {
                DatabaseName = "db1",
                DeleteKeys = new[] { "key1", "key2" }
            },
            TestContext.Current.CancellationToken);

        AlterDatabaseRequest request = Assert.IsType<AlterDatabaseRequest>(server.Service.Requests["AlterDatabase"]);
        Assert.Equal("db1", request.DbName);
        Assert.Equal(new[] { "key1", "key2" }, request.DeleteKeys);
        Assert.Empty(request.Properties);
    }

    // ---- Index ----

    [Fact]
    public async Task CreateIndex_forwards_request()
    {
        using var server = new MockMilvusServer();
        using MilvusClientV2 client = server.CreateClient();

        await client.CreateIndexAsync(
            new CreateIndexReq
            {
                CollectionName = "coll",
                FieldName = "embedding",
                IndexType = IndexType.Hnsw,
                MetricType = SimilarityMetricType.L2,
                IndexName = "idx1",
                ExtraParams = { { "nlist", "128" } }
            },
            TestContext.Current.CancellationToken);

        CreateIndexRequest request = Assert.IsType<CreateIndexRequest>(server.Service.Requests["CreateIndex"]);
        Assert.Equal("coll", request.CollectionName);
        Assert.Equal("embedding", request.FieldName);
        Assert.Equal("idx1", request.IndexName);
        Assert.Contains(request.ExtraParams, p => p.Key == "index_type" && p.Value == "HNSW");
        Assert.Contains(request.ExtraParams, p => p.Key == "metric_type" && p.Value == "L2");
        Assert.Contains(request.ExtraParams, p => p.Key == "nlist" && p.Value == "128");
    }

    [Fact]
    public async Task CreateIndex_sync_waits_for_index_to_finish()
    {
        using var server = new MockMilvusServer();
        using MilvusClientV2 client = server.CreateClient();

        await client.CreateIndexAsync(
            new CreateIndexReq { CollectionName = "coll", FieldName = "embedding" },
            TestContext.Current.CancellationToken);

        Assert.True(server.Service.Requests.ContainsKey("CreateIndex"));
        Assert.True(server.Service.Requests.ContainsKey("DescribeIndex"));
    }

    [Fact]
    public async Task CreateIndex_sync_false_skips_describe_index()
    {
        using var server = new MockMilvusServer();
        using MilvusClientV2 client = server.CreateClient();

        await client.CreateIndexAsync(
            new CreateIndexReq { CollectionName = "coll", FieldName = "embedding", Sync = false },
            TestContext.Current.CancellationToken);

        Assert.True(server.Service.Requests.ContainsKey("CreateIndex"));
        Assert.False(server.Service.Requests.ContainsKey("DescribeIndex"));
    }

    [Fact]
    public async Task DropIndex_forwards_request()
    {
        using var server = new MockMilvusServer();
        using MilvusClientV2 client = server.CreateClient();

        await client.DropIndexAsync(
            new DropIndexReq { CollectionName = "coll", FieldName = "embedding" },
            TestContext.Current.CancellationToken);

        DropIndexRequest request = Assert.IsType<DropIndexRequest>(server.Service.Requests["DropIndex"]);
        Assert.Equal("coll", request.CollectionName);
        Assert.Equal("embedding", request.FieldName);
        Assert.Equal("_default_idx", request.IndexName);
    }

    [Fact]
    public async Task DescribeIndex_forwards_request()
    {
        using var server = new MockMilvusServer();
        using MilvusClientV2 client = server.CreateClient();

        await client.DescribeIndexAsync(
            new DescribeIndexReq { CollectionName = "coll", FieldName = "embedding", IndexName = "idx1" },
            TestContext.Current.CancellationToken);

        DescribeIndexRequest request = Assert.IsType<DescribeIndexRequest>(server.Service.Requests["DescribeIndex"]);
        Assert.Equal("coll", request.CollectionName);
        Assert.Equal("embedding", request.FieldName);
        Assert.Equal("idx1", request.IndexName);
    }

    [Fact]
    public async Task ListIndexes_forwards_request()
    {
        using var server = new MockMilvusServer();
        using MilvusClientV2 client = server.CreateClient();

        await client.ListIndexesAsync(
            new ListIndexesReq { CollectionName = "coll" },
            TestContext.Current.CancellationToken);

        DescribeIndexRequest request = Assert.IsType<DescribeIndexRequest>(server.Service.Requests["DescribeIndex"]);
        Assert.Equal("coll", request.CollectionName);
        Assert.Equal("", request.FieldName);
        Assert.Equal("", request.IndexName);
    }

    [Fact]
    public async Task ListIndexes_returns_empty_when_no_index_exists()
    {
        using var server = new MockMilvusServer();
        server.Service.DescribeIndexNotFound = true;
        using MilvusClientV2 client = server.CreateClient();

        ListIndexesResp response = await client.ListIndexesAsync(
            new ListIndexesReq { CollectionName = "coll" },
            TestContext.Current.CancellationToken);

        Assert.Empty(response.Indexes);
    }

    [Fact]
    public async Task AlterIndexProperties_forwards_request()
    {
        using var server = new MockMilvusServer();
        using MilvusClientV2 client = server.CreateClient();

        await client.AlterIndexPropertiesAsync(
            new AlterIndexPropertiesReq
            {
                CollectionName = "coll",
                IndexName = "idx1",
                Properties = { { "nlist", "256" } },
                DeleteKeys = new[] { "old_key" }
            },
            TestContext.Current.CancellationToken);

        AlterIndexRequest request = Assert.IsType<AlterIndexRequest>(server.Service.Requests["AlterIndex"]);
        Assert.Equal("coll", request.CollectionName);
        Assert.Equal("idx1", request.IndexName);
        Assert.Contains(request.ExtraParams, p => p.Key == "nlist" && p.Value == "256");
        Assert.Contains("old_key", request.DeleteKeys);
    }

    [Fact]
    public async Task DropIndexProperties_forwards_request()
    {
        using var server = new MockMilvusServer();
        using MilvusClientV2 client = server.CreateClient();

        await client.DropIndexPropertiesAsync(
            new DropIndexPropertiesReq
            {
                CollectionName = "coll",
                IndexName = "idx1",
                DeleteKeys = new[] { "key1", "key2" }
            },
            TestContext.Current.CancellationToken);

        AlterIndexRequest request = Assert.IsType<AlterIndexRequest>(server.Service.Requests["AlterIndex"]);
        Assert.Equal("coll", request.CollectionName);
        Assert.Equal("idx1", request.IndexName);
        Assert.Equal(new[] { "key1", "key2" }, request.DeleteKeys);
        Assert.Empty(request.ExtraParams);
    }
}
