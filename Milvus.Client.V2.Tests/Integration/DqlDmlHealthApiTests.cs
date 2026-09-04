using Xunit;

using Milvus.Client.V2;
using Milvus.Client.V2.Requests.Dql;
using Milvus.Client.V2.Requests.Dml;
using Milvus.Client.V2.Responses.Dql;
using Milvus.Client.V2.Responses.Dml;
using Milvus.Client.V2.Types;
using Milvus.Client.V2.Utils;

namespace Milvus.Client.V2.Tests.Integration;

[Trait("Category", "Integration")]
public class DqlDmlHealthApiTests
{
    [Fact]
    public async Task HybridSearch_forwards_sub_requests_and_reranker()
    {
        using var server = new MockMilvusServer();
        using MilvusClientV2 client = server.CreateClient();

        SearchResp response = await client.HybridSearchAsync(
            new HybridSearchReq
            {
                CollectionName = "coll",
                SearchRequests =
                [
                    new SearchReq
                    {
                        CollectionName = "coll",
                        VectorFieldName = "embedding",
                        Vectors = new[] { new ReadOnlyMemory<float>(new[] { 1f, 2f, 3f, 4f }) },
                        MetricType = SimilarityMetricType.L2,
                        Limit = 10
                    },
                    new SearchReq
                    {
                        CollectionName = "coll",
                        VectorFieldName = "embedding",
                        Vectors = new[] { new ReadOnlyMemory<float>(new[] { 1f, 2f, 3f, 4f }) },
                        MetricType = SimilarityMetricType.Ip,
                        Limit = 10
                    }
                ],
                Limit = 3
            },
            TestContext.Current.CancellationToken);

        var grpcRequest =
            Assert.IsType<Milvus.Client.Grpc.HybridSearchRequest>(server.Service.Requests["HybridSearch"]);
        Assert.Equal("coll", grpcRequest.CollectionName);
        Assert.Equal(2, grpcRequest.Requests.Count);
        Assert.Equal(2, grpcRequest.Requests.Count(r => r.PlaceholderGroup.Length > 0));

        var rankParams = grpcRequest.RankParams.ToDictionary(p => p.Key, p => p.Value);
        Assert.Equal("3", rankParams["limit"]);
        Assert.Equal("rrf", rankParams["strategy"]);

        Assert.Equal(3, response.Ids.LongIds!.Count);
        Assert.Equal(3, response.Scores.Count);
    }

    [Fact]
    public async Task QueryIterator_pages_over_results_with_pk_cursor()
    {
        using var server = new MockMilvusServer();
        using MilvusClientV2 client = server.CreateClient();

        SchemaCache.Instance.Clear();
        server.Service.DescribeSchema = BuildSchema();

        await client.InsertAsync(
            new InsertReq
            {
                CollectionName = "iter_coll",
                Data =
                [
                    FieldData.Create("id", new long[] { 1L }),
                    FieldData.CreateFloatVector("embedding", new[] { new ReadOnlyMemory<float>(new[] { 1f, 2f, 3f, 4f }) })
                ]
            },
            TestContext.Current.CancellationToken);

        // A Limit of 1 bounds the cursor-driven loop to a single page against the mock.
        int batches = 0;
        await foreach (IReadOnlyList<FieldData> batch in client
            .QueryIteratorAsync(new QueryIteratorReq
            {
                CollectionName = "iter_coll",
                BatchSize = 1000,
                Parameters = new QueryParameters { Limit = 1 }
            })
            .WithCancellation(TestContext.Current.CancellationToken))
        {
            batches++;
            Assert.NotNull(batch);
        }

        Assert.Equal(1, batches);
        Assert.True(server.Service.Requests.ContainsKey("DescribeCollection"));
        Assert.True(server.Service.Requests.ContainsKey("Query"));

        var grpcRequest = Assert.IsType<Milvus.Client.Grpc.QueryRequest>(server.Service.Requests["Query"]);
        Assert.Equal("iter_coll", grpcRequest.CollectionName);
        Assert.Equal("", grpcRequest.Expr);   // no user expression -> full-range first page, no min-bound literal
        // With an empty output-field list the server returns all fields (including the pk), so the
        // iterator does not add the pk to OutputFields explicitly.
        Assert.Empty(grpcRequest.OutputFields);

        var queryParams = grpcRequest.QueryParams.ToDictionary(p => p.Key, p => p.Value);
        Assert.Equal("True", queryParams["iterator"]);
        Assert.Equal("1", queryParams["batch_size"]);
        Assert.Equal("1", queryParams["limit"]);
    }

    [Fact]
    public async Task QueryIterator_pages_with_pk_cursor_advancement()
    {
        using var server = new MockMilvusServer();
        server.Service.DescribeSchema = BuildSchema();
        server.Service.QueryIteratorTotalRows = 5;
        using MilvusClientV2 client = server.CreateClient();

        SchemaCache.Instance.Clear();

        var expressions = new List<string>();
        await foreach (IReadOnlyList<FieldData> batch in client
            .QueryIteratorAsync(new QueryIteratorReq
            {
                CollectionName = "iter_coll",
                BatchSize = 2
            })
            .WithCancellation(TestContext.Current.CancellationToken))
        {
            Assert.Single(batch);
            expressions.Add(server.Service.LastQueryExpression!);
        }

        // 5 rows at batch size 2: three non-empty pages plus a final empty page that ends the iteration.
        Assert.Equal(4, server.Service.LastQueryCount);
        Assert.Equal("", expressions[0]);
        Assert.StartsWith("id > 1", expressions[1]);
        Assert.StartsWith("id > 3", expressions[2]);
        Assert.Equal(new[] { "2", "2", "2", "2" }, server.Service.LastQueryLimit);
    }

    [Fact]
    public async Task QueryIterator_forwards_ignore_growing_timezone_and_filter_templates()
    {
        using var server = new MockMilvusServer();
        using MilvusClientV2 client = server.CreateClient();

        SchemaCache.Instance.Clear();
        server.Service.DescribeSchema = BuildSchema();

        var parameters = new QueryParameters
        {
            Limit = 1,
            IgnoreGrowing = true,
            Timezone = "+08:00"
        };
        parameters.FilterTemplates["status"] = "active";

        await foreach (IReadOnlyList<FieldData> batch in client
            .QueryIteratorAsync(new QueryIteratorReq
            {
                CollectionName = "iter_coll",
                Expression = "status == @status",
                BatchSize = 1000,
                Parameters = parameters
            })
            .WithCancellation(TestContext.Current.CancellationToken))
        {
            Assert.NotNull(batch);
        }

        var grpcRequest = Assert.IsType<Milvus.Client.Grpc.QueryRequest>(server.Service.Requests["Query"]);
        var queryParams = grpcRequest.QueryParams.ToDictionary(p => p.Key, p => p.Value);
        Assert.Equal("true", queryParams["ignore_growing"]);
        Assert.Equal("+08:00", queryParams["timezone"]);
        Assert.True(grpcRequest.ExprTemplateValues.ContainsKey("status"));
        Assert.Equal("active", grpcRequest.ExprTemplateValues["status"].StringVal);
    }

    [Fact]
    public async Task QueryIterator_rejects_ids_parameter()
    {
        using var server = new MockMilvusServer();
        using MilvusClientV2 client = server.CreateClient();

        SchemaCache.Instance.Clear();
        server.Service.DescribeSchema = BuildSchema();

        await Assert.ThrowsAsync<ArgumentException>(async () =>
        {
            await foreach (IReadOnlyList<FieldData> batch in client
                .QueryIteratorAsync(new QueryIteratorReq
                {
                    CollectionName = "iter_coll",
                    BatchSize = 1000,
                    Parameters = new QueryParameters { Ids = new object[] { 1L } }
                })
                .WithCancellation(TestContext.Current.CancellationToken))
            {
                Assert.NotNull(batch);
            }
        });
    }

    [Fact]
    public async Task SearchIterator_issues_search_with_iterator_params()
    {
        using var server = new MockMilvusServer();
        using MilvusClientV2 client = server.CreateClient();

        SchemaCache.Instance.Clear();
        server.Service.DescribeSchema = BuildSchema();

        // The mock cannot produce a search_iter_v2 token, so enumeration throws after issuing the
        // DescribeCollection + Search RPCs; assert those RPCs and their iterator parameters instead.
        await Assert.ThrowsAsync<MilvusException>(async () =>
        {
            await foreach (SingleResult page in client
                .SearchIteratorAsync(new SearchIteratorReq
                {
                    CollectionName = "iter_coll",
                    VectorFieldName = "embedding",
                    Vectors = new[] { new ReadOnlyMemory<float>(new[] { 1f, 2f, 3f, 4f }) },
                    MetricType = SimilarityMetricType.L2,
                    Limit = 1,
                    BatchSize = 10
                })
                .WithCancellation(TestContext.Current.CancellationToken))
            {
                Assert.NotNull(page);
            }
        });

        Assert.True(server.Service.Requests.ContainsKey("DescribeCollection"));
        Assert.True(server.Service.Requests.ContainsKey("Search"));

        var grpcRequest = Assert.IsType<Milvus.Client.Grpc.SearchRequest>(server.Service.Requests["Search"]);
        Assert.Equal("iter_coll", grpcRequest.CollectionName);
        Assert.Equal("embedding", grpcRequest.SearchParams.Single(p => p.Key == "anns_field").Value);

        var searchParams = grpcRequest.SearchParams.ToDictionary(p => p.Key, p => p.Value);
        Assert.Equal("True", searchParams["iterator"]);
        Assert.Equal("True", searchParams["search_iter_v2"]);
        Assert.Equal("1", searchParams["topk"]);
        Assert.Equal("1", searchParams["search_iter_batch_size"]);
    }

    [Fact]
    public async Task QueryIterator_caps_over_delivered_pages_to_the_limit()
    {
        using var server = new MockMilvusServer();
        server.Service.DescribeSchema = BuildSchema();
        server.Service.QueryIteratorTotalRows = 10;
        // Simulate reduce_stop_for_best over-delivery: the mock returns 5 extra rows beyond the requested limit.
        server.Service.QueryOverDeliver = 5;
        using MilvusClientV2 client = server.CreateClient();

        SchemaCache.Instance.Clear();

        var rows = new List<long>();
        await foreach (IReadOnlyList<FieldData> batch in client
            .QueryIteratorAsync(new QueryIteratorReq
            {
                CollectionName = "iter_coll",
                BatchSize = 2,
                Parameters = new QueryParameters { Limit = 3 }
            })
            .WithCancellation(TestContext.Current.CancellationToken))
        {
            var idColumn = (FieldData<long>)batch.Single(f => f.FieldName == "id");
            rows.AddRange(idColumn.Data);
        }

        // Even though page 1 over-delivers 7 rows, the iterator yields only the 3 the user asked for.
        Assert.Equal(3, rows.Count);
    }

    [Fact]
    public async Task SearchIterator_success_path_pins_token_and_iterates()
    {
        using var server = new MockMilvusServer();
        using MilvusClientV2 client = server.CreateClient();

        SchemaCache.Instance.Clear();
        server.Service.DescribeSchema = BuildSchema();
        // The mock returns a search-iterator token on every page; the loop ends via the remaining count.
        server.Service.SearchIteratorToken = "tok-1";
        // The server pins a snapshot timestamp for the iterator, which the client must forward on page 2.
        server.Service.NextSearchSessionTs = 999;

        var pages = new List<SingleResult>();
        await foreach (SingleResult page in client
            .SearchIteratorAsync(new SearchIteratorReq
            {
                CollectionName = "iter_coll_success",
                VectorFieldName = "embedding",
                Vectors = new[] { new ReadOnlyMemory<float>(new[] { 1f, 2f, 3f, 4f }) },
                MetricType = SimilarityMetricType.L2,
                Limit = 2,
                BatchSize = 1
            })
            .WithCancellation(TestContext.Current.CancellationToken))
        {
            pages.Add(page);
        }

        // The mock returns 1 hit per call; Limit=2 + BatchSize=1 drives two pages, then the loop ends.
        Assert.Equal(2, pages.Count);
        // Each page exposes the hit's score and primary key (the mock returns one hit with score 0.5 and id 1).
        Assert.Equal(new[] { 0.5f }, pages[0].Scores);
        Assert.Equal(1L, pages[0].Ids.LongIds![0]);
        Assert.True(server.Service.Requests.ContainsKey("DescribeCollection"));
        Assert.True(server.Service.Requests.ContainsKey("Search"));
        Assert.Equal(2, server.Service.SearchRequests.Count);

        // Page 1 carries no cursor; page 2 must forward the token and last bound returned by page 1.
        var page1 = server.Service.SearchRequests[0];
        var page1Params = page1.SearchParams.ToDictionary(p => p.Key, p => p.Value);
        Assert.Equal("True", page1Params["iterator"]);
        Assert.Equal("True", page1Params["search_iter_v2"]);
        Assert.Equal("1", page1Params["topk"]);
        Assert.Equal("1", page1Params["search_iter_batch_size"]);
        Assert.DoesNotContain("search_iter_id", page1Params);

        var page2 = server.Service.SearchRequests[1];
        var page2Params = page2.SearchParams.ToDictionary(p => p.Key, p => p.Value);
        Assert.Equal("tok-1", page2Params["search_iter_id"]);
        Assert.Equal("1.500000000000000", page2Params["search_iter_last_bound"]);
        // The snapshot timestamp returned by page 1 is pinned for page 2.
        Assert.Equal(999UL, page2.GuaranteeTimestamp);
    }

    [Fact]
    public async Task Upsert_forwards_data_and_updates_ts_cache()
    {
        using var server = new MockMilvusServer { Service = { NextMutationTimestamp = 200 } };
        using MilvusClientV2 client = server.CreateClient();

        CollectionTsCache.Instance.Clear();
        MutationResp response = await client.UpsertAsync(
            new UpsertReq
            {
                CollectionName = "dml_coll",
                Data =
                [
                    FieldData.Create("id", new long[] { 1L, 2L }),
                    FieldData.CreateVarChar("name", new[] { "a", "b" })
                ]
            },
            TestContext.Current.CancellationToken);

        var grpcRequest = Assert.IsType<Milvus.Client.Grpc.UpsertRequest>(server.Service.Requests["Upsert"]);
        Assert.Equal("dml_coll", grpcRequest.CollectionName);
        Assert.Equal(2U, grpcRequest.NumRows);
        Assert.Equal(2, grpcRequest.FieldsData.Count);

        Assert.Equal(2, response.UpsertCount);
        Assert.Equal(200UL, response.Timestamp);
        Assert.Equal(200L, CollectionTsCache.Instance.Get(server.Uri, "default", "dml_coll"));
    }

    [Fact]
    public async Task HealthAsync_checks_server_health()
    {
        using var server = new MockMilvusServer();
        using MilvusClientV2 client = server.CreateClient();

        MilvusHealthState health = await client.HealthAsync(TestContext.Current.CancellationToken);

        Assert.True(server.Service.Requests.ContainsKey("CheckHealth"));
        Assert.IsType<Milvus.Client.Grpc.CheckHealthRequest>(server.Service.Requests["CheckHealth"]);
        Assert.True(health.IsHealthy);
        Assert.Equal(MilvusErrorCode.Success, health.ErrorCode);
        Assert.Contains("healthy", health.Reasons!);
        Assert.Contains(Milvus.Client.V2.Types.QuotaState.ReadLimited, health.QuotaStates!);
    }

    [Fact]
    public async Task CheckHealthAsync_aliases_health_check()
    {
        using var server = new MockMilvusServer();
        using MilvusClientV2 client = server.CreateClient();

        MilvusHealthState health = await client.CheckHealthAsync(TestContext.Current.CancellationToken);

        Assert.True(server.Service.Requests.ContainsKey("CheckHealth"));
        Assert.True(health.IsHealthy);
    }

    private static CollectionSchema BuildSchema()
    {
        var schema = new CollectionSchema { Name = "iter_coll" };
        schema.Fields.Add(new FieldSchema("id", DataType.Int64, isPrimaryKey: true));
        schema.Fields.Add(FieldSchema.CreateFloatVector("embedding", 4));
        return schema;
    }
}
