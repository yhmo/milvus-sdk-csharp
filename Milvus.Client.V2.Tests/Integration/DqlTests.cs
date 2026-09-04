using Xunit;

using Milvus.Client.V2;
using Milvus.Client.V2.Requests.Dql;
using Milvus.Client.V2.Responses.Dql;
using Milvus.Client.V2.Types;
using Milvus.Client.V2.Utils;

namespace Milvus.Client.V2.Tests.Integration;

[Trait("Category", "Integration")]
public class DqlTests
{
    [Fact]
    public async Task Search_forwards_request()
    {
        using var server = new MockMilvusServer();
        using MilvusClientV2 client = server.CreateClient();

        SearchResp response = await client.SearchAsync(
            new SearchReq
            {
                CollectionName = "coll",
                VectorFieldName = "embedding",
                Vectors = new[] { new ReadOnlyMemory<float>(new[] { 1f, 2f, 3f, 4f }) },
                MetricType = SimilarityMetricType.L2,
                Limit = 2
            },
            TestContext.Current.CancellationToken);

        Assert.Equal("coll", server.Service.LastSearchedCollection);
        Assert.Equal(2, server.Service.LastSearchTopK);
        Assert.Equal(2, response.Ids.LongIds!.Count);
        Assert.Equal(2, response.Scores.Count);
        Assert.Equal(0.5f, response.Scores[0]);
    }

    [Fact]
    public async Task Search_uses_ts_cache_for_session_consistency()
    {
        using var server = new MockMilvusServer { Service = { NextMutationTimestamp = 100 } };
        using MilvusClientV2 client = server.CreateClient();

        // Simulate a prior DML so the ts cache is populated.
        CollectionTsCache.Instance.Clear();
        CollectionTsCache.Instance.Set(server.Uri, "default", "session_coll", 100);

        await client.SearchAsync(
            new SearchReq
            {
                CollectionName = "session_coll",
                VectorFieldName = "embedding",
                Vectors = new[] { new ReadOnlyMemory<float>(new[] { 1f, 2f }) },
                MetricType = SimilarityMetricType.L2,
                Limit = 1,
                Parameters = new SearchParameters { ConsistencyLevel = ConsistencyLevel.Session }
            },
            TestContext.Current.CancellationToken);

        Assert.Equal(100UL, server.Service.LastSearchGuaranteeTimestamp);
    }

    [Fact]
    public async Task Search_unset_consistency_still_sends_session_guarantee_timestamp()
    {
        using var server = new MockMilvusServer();
        using MilvusClientV2 client = server.CreateClient();

        CollectionTsCache.Instance.Clear();
        CollectionTsCache.Instance.Set(server.Uri, "default", "session_coll", 200);

        await client.SearchAsync(
            new SearchReq
            {
                CollectionName = "session_coll",
                VectorFieldName = "embedding",
                Vectors = new[] { new ReadOnlyMemory<float>(new[] { 1f, 2f }) },
                MetricType = SimilarityMetricType.L2,
                Limit = 1
            },
            TestContext.Current.CancellationToken);

        // Unset consistency still carries the Session-style cached timestamp (matching C++ DeduceGuaranteeTimestamp
        // on NONE and the design doc §4.6), so insert-then-search honors read-your-writes.
        Assert.Equal(200UL, server.Service.LastSearchGuaranteeTimestamp);
        Assert.True(server.Service.LastSearchUseDefaultConsistency);
    }

    [Fact]
    public async Task Search_serializes_ignore_growing_and_graceful_time()
    {
        using var server = new MockMilvusServer();
        using MilvusClientV2 client = server.CreateClient();

        var parameters = new SearchParameters { GracefulTime = 5000 };
        parameters.SetIgnoreGrowing(true);

        await client.SearchAsync(
            new SearchReq
            {
                CollectionName = "coll",
                VectorFieldName = "embedding",
                Vectors = new[] { new ReadOnlyMemory<float>(new[] { 1f, 2f }) },
                MetricType = SimilarityMetricType.L2,
                Limit = 1,
                Parameters = parameters
            },
            TestContext.Current.CancellationToken);

        var byKey = server.Service.LastSearchParams.ToDictionary(p => p.Key, p => p.Value);
        Assert.Equal("true", byKey["ignore_growing"]);
        Assert.Equal("5000", byKey["graceful_time"]);
    }

    [Fact]
    public async Task Search_serializes_timezone_radius_range_filter_and_rerank()
    {
        using var server = new MockMilvusServer();
        using MilvusClientV2 client = server.CreateClient();

        await client.SearchAsync(
            new SearchReq
            {
                CollectionName = "coll",
                VectorFieldName = "embedding",
                Vectors = new[] { new ReadOnlyMemory<float>(new[] { 1f, 2f }) },
                MetricType = SimilarityMetricType.L2,
                Limit = 1,
                Parameters = new SearchParameters
                {
                    Timezone = "+08:00",
                    Radius = "0.5",
                    RangeFilter = "1.0",
                    Rerank = "rrf"
                }
            },
            TestContext.Current.CancellationToken);

        var byKey = server.Service.LastSearchParams.ToDictionary(p => p.Key, p => p.Value);
        Assert.Equal("+08:00", byKey["timezone"]);
        Assert.Equal("0.5", byKey["radius"]);
        Assert.Equal("1.0", byKey["range_filter"]);
        Assert.Equal("rrf", byKey["rerank"]);

        // The proxy detects range search from the "params" JSON string, so radius/range_filter must also be
        // embedded there (as numbers, since string-typed radius/range_filter is rejected server-side).
        using var paramsJson = System.Text.Json.JsonDocument.Parse(byKey["params"]);
        Assert.Equal(0.5, paramsJson.RootElement.GetProperty("radius").GetDouble());
        Assert.Equal(1.0, paramsJson.RootElement.GetProperty("range_filter").GetDouble());
    }

    [Fact]
    public async Task Search_by_ids_sends_ids_instead_of_placeholder()
    {
        using var server = new MockMilvusServer();
        using MilvusClientV2 client = server.CreateClient();

        await client.SearchAsync(
            new SearchReq
            {
                CollectionName = "coll",
                VectorFieldName = "embedding",
                Ids = new object[] { 1L, 2L, 3L },
                MetricType = SimilarityMetricType.L2,
                Limit = 3
            },
            TestContext.Current.CancellationToken);

        var request = Assert.IsType<Milvus.Client.Grpc.SearchRequest>(server.Service.Requests["Search"]);
        Assert.NotNull(request.Ids);
        Assert.Equal(new long[] { 1L, 2L, 3L }, request.Ids.IntId.Data);
        Assert.True(string.IsNullOrEmpty(request.PlaceholderGroup.ToStringUtf8()) || request.PlaceholderGroup.Length == 0);
    }

    [Fact]
    public async Task Query_forwards_expression()
    {
        using var server = new MockMilvusServer();
        using MilvusClientV2 client = server.CreateClient();

        QueryResp response = await client.QueryAsync(
            new QueryReq
            {
                CollectionName = "coll",
                Expression = "id in [1, 2]",
                Parameters = new QueryParameters { OutputFields = { "id" } }
            },
            TestContext.Current.CancellationToken);

        Assert.Equal("coll", server.Service.LastQueriedCollection);
        Assert.Equal("id in [1, 2]", server.Service.LastQueryExpression);
        Assert.Single(response.FieldsData);
    }

    [Fact]
    public async Task Get_builds_query_by_primary_key()
    {
        using var server = new MockMilvusServer();
        using MilvusClientV2 client = server.CreateClient();

        // The mock DescribeCollection has no schema, so Get needs one via the (mock) describe path.
        // Give the mock a describable schema by adding a primary-key field through DescribeCollectionResp.
        SchemaCache.Instance.Clear();
        server.Service.DescribeSchema = BuildSchema();

        GetResp response = await client.GetAsync(
            new GetReq { CollectionName = "coll", Ids = new object[] { 1L, 2L } },
            TestContext.Current.CancellationToken);

        Assert.Single(response.FieldsData);
        Assert.Equal("id in [1, 2]", server.Service.LastQueryExpression);
    }

    [Fact]
    public async Task Search_forwards_highlighter_type()
    {
        using var server = new MockMilvusServer();
        using MilvusClientV2 client = server.CreateClient();

        var parameters = new SearchParameters
        {
            HighlightType = HighlightType.Semantic
        };
        parameters.Highlighter["max_length"] = "20";

        await client.SearchAsync(
            new SearchReq
            {
                CollectionName = "coll",
                VectorFieldName = "embedding",
                Vectors = new[] { new ReadOnlyMemory<float>(new[] { 1f, 2f, 3f, 4f }) },
                MetricType = SimilarityMetricType.L2,
                Limit = 1,
                Parameters = parameters
            },
            TestContext.Current.CancellationToken);

        var grpcRequest = Assert.IsType<Milvus.Client.Grpc.SearchRequest>(server.Service.Requests["Search"]);
        Assert.Equal(Grpc.HighlightType.Semantic, grpcRequest.Highlighter.Type);
        Assert.Equal("20", grpcRequest.Highlighter.Params.Single(p => p.Key == "max_length").Value);
    }

    [Fact]
    public async Task Search_parses_metrics_from_status_extra_info()
    {
        using var server = new MockMilvusServer();
        using MilvusClientV2 client = server.CreateClient();

        server.Service.SearchMetricsStatus = new Milvus.Client.Grpc.Status
        {
            ExtraInfo =
            {
                { "report_value", "7" },
                { "scanned_remote_bytes", "30" },
                { "scanned_total_bytes", "60" },
                { "cache_hit_ratio", "0.9" }
            }
        };

        SearchResp response = await client.SearchAsync(
            new SearchReq
            {
                CollectionName = "coll",
                VectorFieldName = "embedding",
                Vectors = new[] { new ReadOnlyMemory<float>(new[] { 1f, 2f, 3f, 4f }) },
                MetricType = SimilarityMetricType.L2,
                Limit = 1
            },
            TestContext.Current.CancellationToken);

        Assert.Equal(7, response.Cost);
        Assert.Equal(30, response.ScannedRemoteBytes);
        Assert.Equal(60, response.ScannedTotalBytes);
        Assert.Equal(0.9f, response.CacheHitRatio);
    }

    private static CollectionSchema BuildSchema()
    {
        var schema = new CollectionSchema { Name = "coll" };
        schema.Fields.Add(new FieldSchema("id", DataType.Int64, isPrimaryKey: true));
        return schema;
    }
}
