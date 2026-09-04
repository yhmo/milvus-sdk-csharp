using Xunit;

using Milvus.Client.V2;
using Milvus.Client.V2.Requests.Collection;
using Milvus.Client.V2.Responses.Collection;
using Milvus.Client.V2.Types;
using Milvus.Client.V2.Utils;

namespace Milvus.Client.V2.Tests.Integration;

[Trait("Category", "Integration")]
public class CollectionApiTests
{
    [Fact]
    public async Task CreateCollection_forwards_request_and_succeeds()
    {
        using var server = new MockMilvusServer();
        using MilvusClientV2 client = server.CreateClient();

        await client.CreateCollectionAsync(new CreateCollectionReq
        {
            CollectionName = "coll",
            Schema = new CollectionSchema
            {
                Fields =
                {
                    new FieldSchema("id", DataType.Int64, isPrimaryKey: true),
                    FieldSchema.CreateFloatVector("embedding", dimension: 4)
                }
            }
        }, TestContext.Current.CancellationToken);

        Assert.Equal("coll", server.Service.LastCreatedCollectionName);
    }

    [Fact]
    public async Task HasCollection_maps_response()
    {
        using var server = new MockMilvusServer { Service = { HasCollectionResult = true } };
        using MilvusClientV2 client = server.CreateClient();

        HasCollectionResp response =
            await client.HasCollectionAsync(new HasCollectionReq { CollectionName = "coll" }, TestContext.Current.CancellationToken);

        Assert.True(response.Has);
        Assert.Equal("coll", server.Service.LastCheckedCollectionName);
    }

    [Fact]
    public async Task ListCollections_maps_response()
    {
        using var server = new MockMilvusServer();
        server.Service.CollectionNames.Add("a");
        server.Service.CollectionNames.Add("b");
        using MilvusClientV2 client = server.CreateClient();

        ListCollectionsResp response = await client.ListCollectionsAsync(new ListCollectionsReq(), TestContext.Current.CancellationToken);

        Assert.Equal(new[] { "a", "b" }, response.CollectionNames);
    }

    [Fact]
    public async Task Server_error_maps_to_MilvusException()
    {
        using var server = new MockMilvusServer();
        using MilvusClientV2 client = server.CreateClient();

        // Connect successfully first (lazy), then make the operation itself fail.
        await client.ConnectAsync(TestContext.Current.CancellationToken);
        server.Service.FailureStatus = new Milvus.Client.Grpc.Status
        {
            Code = (int)MilvusErrorCode.CollectionNotFound,
            Reason = "collection not found"
        };

        MilvusException exception = await Assert.ThrowsAsync<MilvusException>(() =>
            client.HasCollectionAsync(new HasCollectionReq { CollectionName = "missing" }, TestContext.Current.CancellationToken));

        Assert.Equal(MilvusErrorCode.CollectionNotFound, exception.ErrorCode);
        Assert.Contains("collection not found", exception.Message);
    }
}

/// <summary>
/// Verifies that the MilvusClientV2 collection-domain APIs forward the expected gRPC requests.
/// </summary>
[Trait("Category", "Integration")]
public class CollectionApiForwardingTests
{
    private static CollectionSchema PrimaryKeySchema()
        => new() { Fields = { new FieldSchema("id", DataType.Int64, isPrimaryKey: true) } };

    [Fact]
    public async Task AddCollectionField_forwards_request()
    {
        using var server = new MockMilvusServer();
        using MilvusClientV2 client = server.CreateClient();

        await client.AddCollectionFieldAsync(
            new AddCollectionFieldReq
            {
                CollectionName = "add_field_coll",
                Field = FieldSchema.CreateFloatVector("embedding", dimension: 8)
            },
            TestContext.Current.CancellationToken);

        Grpc.AddCollectionFieldRequest request =
            Assert.IsType<Grpc.AddCollectionFieldRequest>(server.Service.Requests["AddCollectionField"]);
        Assert.Equal("add_field_coll", request.CollectionName);

        Grpc.FieldSchema field = Grpc.FieldSchema.Parser.ParseFrom(request.Schema);
        Assert.Equal("embedding", field.Name);
        Assert.Equal(Grpc.DataType.FloatVector, field.DataType);
    }

    [Fact]
    public async Task AddCollectionFunction_forwards_request()
    {
        using var server = new MockMilvusServer();
        server.Service.DescribeSchema = PrimaryKeySchema();
        using MilvusClientV2 client = server.CreateClient();

        await client.AddCollectionFunctionAsync(
            new AddCollectionFunctionReq
            {
                CollectionName = "add_func_coll",
                Function = FunctionSchema.CreateBm25("bm25_func", "text", "sparse")
            },
            TestContext.Current.CancellationToken);

        Grpc.AddCollectionFunctionRequest request =
            Assert.IsType<Grpc.AddCollectionFunctionRequest>(server.Service.Requests["AddCollectionFunction"]);
        Assert.Equal("add_func_coll", request.CollectionName);
        Assert.Equal("bm25_func", request.FunctionSchema.Name);
    }

    [Fact]
    public async Task AlterCollectionField_forwards_request()
    {
        using var server = new MockMilvusServer();
        using MilvusClientV2 client = server.CreateClient();

        await client.AlterCollectionFieldAsync(
            new AlterCollectionFieldReq
            {
                CollectionName = "alter_field_coll",
                FieldName = "embedding",
                Properties = { ["index_type"] = "IVF_FLAT" }
            },
            TestContext.Current.CancellationToken);

        Grpc.AlterCollectionFieldRequest request =
            Assert.IsType<Grpc.AlterCollectionFieldRequest>(server.Service.Requests["AlterCollectionField"]);
        Assert.Equal("alter_field_coll", request.CollectionName);
        Assert.Equal("embedding", request.FieldName);
        Assert.Equal("index_type", request.Properties.Single().Key);
        Assert.Equal("IVF_FLAT", request.Properties.Single().Value);
    }

    [Fact]
    public async Task AlterCollectionFunction_forwards_request()
    {
        using var server = new MockMilvusServer();
        server.Service.DescribeSchema = PrimaryKeySchema();
        using MilvusClientV2 client = server.CreateClient();

        await client.AlterCollectionFunctionAsync(
            new AlterCollectionFunctionReq
            {
                CollectionName = "alter_func_coll",
                FunctionName = "bm25_func",
                Function = FunctionSchema.CreateBm25("bm25_func", "text", "sparse")
            },
            TestContext.Current.CancellationToken);

        Grpc.AlterCollectionFunctionRequest request =
            Assert.IsType<Grpc.AlterCollectionFunctionRequest>(server.Service.Requests["AlterCollectionFunction"]);
        Assert.Equal("alter_func_coll", request.CollectionName);
        Assert.Equal("bm25_func", request.FunctionName);
        Assert.Equal("bm25_func", request.FunctionSchema.Name);
    }

    [Fact]
    public async Task AlterCollectionProperties_forwards_request()
    {
        using var server = new MockMilvusServer();
        using MilvusClientV2 client = server.CreateClient();

        await client.AlterCollectionPropertiesAsync(
            new AlterCollectionPropertiesReq
            {
                CollectionName = "alter_props_coll",
                Properties = { ["collection.ttl.seconds"] = "86400" }
            },
            TestContext.Current.CancellationToken);

        Grpc.AlterCollectionRequest request =
            Assert.IsType<Grpc.AlterCollectionRequest>(server.Service.Requests["AlterCollection"]);
        Assert.Equal("alter_props_coll", request.CollectionName);
        Assert.Equal("collection.ttl.seconds", request.Properties.Single().Key);
        Assert.Equal("86400", request.Properties.Single().Value);
    }

    [Fact]
    public async Task DescribeCollection_forwards_request()
    {
        using var server = new MockMilvusServer();
        server.Service.DescribeSchema = new CollectionSchema
        {
            Name = "describe_coll",
            Fields = { new FieldSchema("id", DataType.Int64, isPrimaryKey: true) }
        };
        using MilvusClientV2 client = server.CreateClient();

        DescribeCollectionResp response = await client.DescribeCollectionAsync(
            new DescribeCollectionReq { CollectionName = "describe_coll" },
            TestContext.Current.CancellationToken);

        Grpc.DescribeCollectionRequest request =
            Assert.IsType<Grpc.DescribeCollectionRequest>(server.Service.Requests["DescribeCollection"]);
        Assert.Equal("describe_coll", request.CollectionName);
        Assert.Equal("describe_coll", response.CollectionName);
        Assert.Single(response.Schema.Fields);
    }

    [Fact]
    public async Task BatchDescribeCollections_forwards_names_and_maps_descriptions()
    {
        using var server = new MockMilvusServer();
        using MilvusClientV2 client = server.CreateClient();

        IReadOnlyList<DescribeCollectionResp> response = await client.BatchDescribeCollectionsAsync(
            new BatchDescribeCollectionsReq { CollectionNames = new[] { "a", "b" } },
            TestContext.Current.CancellationToken);

        Grpc.BatchDescribeCollectionRequest request =
            Assert.IsType<Grpc.BatchDescribeCollectionRequest>(server.Service.Requests["BatchDescribeCollection"]);
        Assert.Equal(new[] { "a", "b" }, request.CollectionName);
        Assert.Equal(2, response.Count);
        Assert.Equal("a", response[0].CollectionName);
        Assert.Equal("b", response[1].CollectionName);
    }

    [Fact]
    public async Task BatchDescribeCollections_throws_on_missing_collection_response()
    {
        using var server = new MockMilvusServer();
        server.Service.BatchDescribeMissingCollections = new HashSet<string> { "missing" };
        using MilvusClientV2 client = server.CreateClient();

        MilvusException ex = await Assert.ThrowsAsync<MilvusException>(() =>
            client.BatchDescribeCollectionsAsync(
                new BatchDescribeCollectionsReq { CollectionNames = new[] { "ok", "missing" } },
                TestContext.Current.CancellationToken));

        Assert.Contains("missing", ex.Message);
    }

    [Fact]
    public async Task BatchDescribeCollections_throws_when_empty()
    {
        using var server = new MockMilvusServer();
        using MilvusClientV2 client = server.CreateClient();

        await Assert.ThrowsAsync<ArgumentException>(() =>
            client.BatchDescribeCollectionsAsync(new BatchDescribeCollectionsReq(), TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task DescribeReplicas_forwards_request()
    {
        using var server = new MockMilvusServer();
        using MilvusClientV2 client = server.CreateClient();

        DescribeReplicasResp response = await client.DescribeReplicasAsync(
            new DescribeReplicasReq { CollectionName = "describe_replicas_coll", WithShardNodes = true },
            TestContext.Current.CancellationToken);

        Grpc.GetReplicasRequest request =
            Assert.IsType<Grpc.GetReplicasRequest>(server.Service.Requests["GetReplicas"]);
        Assert.Equal("describe_replicas_coll", request.CollectionName);
        Assert.True(request.WithShardNodes);
        Assert.Empty(response.Replicas);
    }

    [Fact]
    public async Task DropCollection_forwards_request()
    {
        using var server = new MockMilvusServer();
        using MilvusClientV2 client = server.CreateClient();

        await client.DropCollectionAsync(
            new DropCollectionReq { CollectionName = "drop_coll" },
            TestContext.Current.CancellationToken);

        Grpc.DropCollectionRequest request =
            Assert.IsType<Grpc.DropCollectionRequest>(server.Service.Requests["DropCollection"]);
        Assert.Equal("drop_coll", request.CollectionName);
    }

    [Fact]
    public async Task DropCollectionFieldProperties_forwards_request()
    {
        using var server = new MockMilvusServer();
        using MilvusClientV2 client = server.CreateClient();

        await client.DropCollectionFieldPropertiesAsync(
            new DropCollectionFieldPropertiesReq
            {
                CollectionName = "drop_field_props_coll",
                FieldName = "embedding",
                DeleteKeys = new[] { "index_type" }
            },
            TestContext.Current.CancellationToken);

        Grpc.AlterCollectionFieldRequest request =
            Assert.IsType<Grpc.AlterCollectionFieldRequest>(server.Service.Requests["AlterCollectionField"]);
        Assert.Equal("drop_field_props_coll", request.CollectionName);
        Assert.Equal("embedding", request.FieldName);
        Assert.Equal(new[] { "index_type" }, request.DeleteKeys);
    }

    [Fact]
    public async Task DropCollectionFunction_forwards_request()
    {
        using var server = new MockMilvusServer();
        server.Service.DescribeSchema = PrimaryKeySchema();
        using MilvusClientV2 client = server.CreateClient();

        await client.DropCollectionFunctionAsync(
            new DropCollectionFunctionReq
            {
                CollectionName = "drop_func_coll",
                FunctionName = "bm25_func"
            },
            TestContext.Current.CancellationToken);

        Grpc.DropCollectionFunctionRequest request =
            Assert.IsType<Grpc.DropCollectionFunctionRequest>(server.Service.Requests["DropCollectionFunction"]);
        Assert.Equal("drop_func_coll", request.CollectionName);
        Assert.Equal("bm25_func", request.FunctionName);
    }

    [Fact]
    public async Task DropCollectionProperties_forwards_request()
    {
        using var server = new MockMilvusServer();
        using MilvusClientV2 client = server.CreateClient();

        await client.DropCollectionPropertiesAsync(
            new DropCollectionPropertiesReq
            {
                CollectionName = "drop_props_coll",
                DeleteKeys = new[] { "collection.ttl.seconds" }
            },
            TestContext.Current.CancellationToken);

        Grpc.AlterCollectionRequest request =
            Assert.IsType<Grpc.AlterCollectionRequest>(server.Service.Requests["AlterCollection"]);
        Assert.Equal("drop_props_coll", request.CollectionName);
        Assert.Equal(new[] { "collection.ttl.seconds" }, request.DeleteKeys);
    }

    [Fact]
    public async Task GetCollectionStats_forwards_request()
    {
        using var server = new MockMilvusServer();
        using MilvusClientV2 client = server.CreateClient();

        GetCollectionStatsResp response = await client.GetCollectionStatsAsync(
            new GetCollectionStatsReq { CollectionName = "get_stats_coll" },
            TestContext.Current.CancellationToken);

        Grpc.GetCollectionStatisticsRequest request =
            Assert.IsType<Grpc.GetCollectionStatisticsRequest>(server.Service.Requests["GetCollectionStatistics"]);
        Assert.Equal("get_stats_coll", request.CollectionName);
        Assert.Equal(0L, response.RowCount);
    }

    [Fact]
    public async Task GetLoadState_forwards_request()
    {
        using var server = new MockMilvusServer();
        using MilvusClientV2 client = server.CreateClient();

        GetLoadStateResp response = await client.GetLoadStateAsync(
            new GetLoadStateReq { CollectionName = "get_load_state_coll" },
            TestContext.Current.CancellationToken);

        Grpc.GetLoadStateRequest request =
            Assert.IsType<Grpc.GetLoadStateRequest>(server.Service.Requests["GetLoadState"]);
        Assert.Equal("get_load_state_coll", request.CollectionName);
        Assert.Equal(LoadState.Loaded, response.State);
        Assert.Equal(100, response.Progress);   // Loaded => 100%, matching C++/Java
    }

    [Fact]
    public async Task GetLoadState_reports_loading_progress()
    {
        using var server = new MockMilvusServer { Service = { GetLoadStateResult = Grpc.LoadState.Loading, LoadingProgress = 37 } };
        using MilvusClientV2 client = server.CreateClient();

        GetLoadStateResp response = await client.GetLoadStateAsync(
            new GetLoadStateReq { CollectionName = "get_load_state_coll" },
            TestContext.Current.CancellationToken);

        Assert.Equal(LoadState.Loading, response.State);
        Assert.Equal(37, response.Progress);
        Assert.Contains("GetLoadingProgress", server.Service.Requests.Keys);
    }

    [Fact]
    public async Task LoadCollection_forwards_request()
    {
        using var server = new MockMilvusServer();
        using MilvusClientV2 client = server.CreateClient();

        await client.LoadCollectionAsync(
            new LoadCollectionReq { CollectionName = "load_coll", ReplicaNumber = 2 },
            TestContext.Current.CancellationToken);

        Grpc.LoadCollectionRequest request =
            Assert.IsType<Grpc.LoadCollectionRequest>(server.Service.Requests["LoadCollection"]);
        Assert.Equal("load_coll", request.CollectionName);
        Assert.Equal(2, request.ReplicaNumber);
        Assert.False(request.Refresh);
    }

    [Fact]
    public async Task RefreshLoad_forwards_refresh_flag()
    {
        using var server = new MockMilvusServer();
        using MilvusClientV2 client = server.CreateClient();

        await client.RefreshLoadAsync(
            new RefreshLoadReq { CollectionName = "refresh_load_coll", Sync = false },
            TestContext.Current.CancellationToken);

        Grpc.LoadCollectionRequest request =
            Assert.IsType<Grpc.LoadCollectionRequest>(server.Service.Requests["LoadCollection"]);
        Assert.Equal("refresh_load_coll", request.CollectionName);
        Assert.True(request.Refresh);
    }

    [Fact]
    public async Task ReleaseCollection_forwards_request()
    {
        using var server = new MockMilvusServer();
        using MilvusClientV2 client = server.CreateClient();

        await client.ReleaseCollectionAsync(
            new ReleaseCollectionReq { CollectionName = "release_coll" },
            TestContext.Current.CancellationToken);

        Grpc.ReleaseCollectionRequest request =
            Assert.IsType<Grpc.ReleaseCollectionRequest>(server.Service.Requests["ReleaseCollection"]);
        Assert.Equal("release_coll", request.CollectionName);
    }

    [Fact]
    public async Task RenameCollection_forwards_request()
    {
        using var server = new MockMilvusServer();
        using MilvusClientV2 client = server.CreateClient();

        await client.RenameCollectionAsync(
            new RenameCollectionReq { CollectionName = "rename_coll", NewCollectionName = "rename_coll_new" },
            TestContext.Current.CancellationToken);

        Grpc.RenameCollectionRequest request =
            Assert.IsType<Grpc.RenameCollectionRequest>(server.Service.Requests["RenameCollection"]);
        Assert.Equal("rename_coll", request.OldName);
        Assert.Equal("rename_coll_new", request.NewName);
    }

    [Fact]
    public async Task RenameCollection_moves_ts_cache_to_target_database()
    {
        CollectionTsCache.Instance.Clear();
        SchemaCache.Instance.Clear();
        try
        {
            using var server = new MockMilvusServer();
            using MilvusClientV2 client = server.CreateClient();

            // Simulate a prior DML so the ts cache is populated for the source collection.
            CollectionTsCache.Instance.Set(server.Uri, "default", "rename_coll", 100);

            await client.RenameCollectionAsync(
                new RenameCollectionReq
                {
                    CollectionName = "rename_coll",
                    NewCollectionName = "rename_coll_new",
                    TargetDatabaseName = "db2"
                },
                TestContext.Current.CancellationToken);

            Assert.Equal(100L, CollectionTsCache.Instance.Get(server.Uri, "db2", "rename_coll_new"));
            Assert.Equal(0L, CollectionTsCache.Instance.Get(server.Uri, "default", "rename_coll"));
        }
        finally
        {
            CollectionTsCache.Instance.Clear();
            SchemaCache.Instance.Clear();
        }
    }

    [Fact]
    public async Task TruncateCollection_forwards_request()
    {
        using var server = new MockMilvusServer();
        using MilvusClientV2 client = server.CreateClient();

        await client.TruncateCollectionAsync(
            new TruncateCollectionReq { CollectionName = "truncate_coll" },
            TestContext.Current.CancellationToken);

        Grpc.TruncateCollectionRequest request =
            Assert.IsType<Grpc.TruncateCollectionRequest>(server.Service.Requests["TruncateCollection"]);
        Assert.Equal("truncate_coll", request.CollectionName);
    }
}
