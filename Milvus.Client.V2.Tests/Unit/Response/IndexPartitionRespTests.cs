using Xunit;

using Milvus.Client.V2.Responses.Index;
using Milvus.Client.V2.Responses.Partition;
using Milvus.Client.V2.Types;

namespace Milvus.Client.V2.Tests.Unit.Response;

[Trait("Category", "Unit")]
public class IndexPartitionRespTests
{
    [Fact]
    public void IndexDescription_maps_proto_fields_and_params()
    {
        var grpc = new Grpc.IndexDescription
        {
            IndexName = "my_idx",
            IndexID = 42,
            FieldName = "embedding",
            State = Grpc.IndexState.Finished,
            IndexedRows = 100,
            TotalRows = 120,
            PendingIndexRows = 20,
            IndexStateFailReason = "",
            Params =
            {
                new Grpc.KeyValuePair { Key = "index_type", Value = "HNSW" },
                new Grpc.KeyValuePair { Key = "metric_type", Value = "COSINE" }
            }
        };

        IndexDescription description = IndexDescription.FromGrpc(grpc);

        Assert.Equal("my_idx", description.IndexName);
        Assert.Equal(42, description.IndexId);
        Assert.Equal("embedding", description.FieldName);
        Assert.Equal(IndexState.Finished, description.State);
        Assert.Equal(100, description.IndexedRows);
        Assert.Equal(120, description.TotalRows);
        Assert.Equal(20, description.PendingIndexRows);
        Assert.Null(description.IndexStateFailReason);
        Assert.Equal(2, description.Parameters.Count);
        Assert.Equal("HNSW", description.Parameters["index_type"]);
        Assert.Equal("COSINE", description.Parameters["metric_type"]);
    }

    [Fact]
    public void IndexDescription_preserves_fail_reason_and_maps_failed_state()
    {
        var grpc = new Grpc.IndexDescription
        {
            IndexName = "my_idx",
            State = Grpc.IndexState.Failed,
            IndexStateFailReason = "disk full"
        };

        IndexDescription description = IndexDescription.FromGrpc(grpc);

        Assert.Equal(IndexState.Failed, description.State);
        Assert.Equal("disk full", description.IndexStateFailReason);
        Assert.Empty(description.Parameters);
    }

    [Fact]
    public void DescribeIndexResp_maps_index_descriptions()
    {
        var grpc = new Grpc.DescribeIndexResponse();
        grpc.IndexDescriptions.Add(new Grpc.IndexDescription { IndexName = "idx_a", IndexID = 1 });
        grpc.IndexDescriptions.Add(new Grpc.IndexDescription { IndexName = "idx_b", IndexID = 2 });

        DescribeIndexResp response = DescribeIndexResp.FromGrpc(grpc);

        Assert.Equal(2, response.Indexes.Count);
        Assert.Equal("idx_a", response.Indexes[0].IndexName);
        Assert.Equal(1, response.Indexes[0].IndexId);
        Assert.Equal("idx_b", response.Indexes[1].IndexName);
        Assert.Equal(2, response.Indexes[1].IndexId);
    }

    [Fact]
    public void ListIndexesResp_maps_index_descriptions()
    {
        var grpc = new Grpc.DescribeIndexResponse();
        grpc.IndexDescriptions.Add(new Grpc.IndexDescription { IndexName = "idx_a" });
        grpc.IndexDescriptions.Add(new Grpc.IndexDescription { IndexName = "idx_b" });

        ListIndexesResp response = ListIndexesResp.FromGrpc(grpc);

        Assert.Equal(2, response.Indexes.Count);
        Assert.Equal(new[] { "idx_a", "idx_b" }, response.Indexes.Select(i => i.IndexName));
    }

    [Fact]
    public void GetPartitionStatsResp_parses_row_count_stat()
    {
        var grpc = new Grpc.GetPartitionStatisticsResponse();
        grpc.Stats.Add(new Grpc.KeyValuePair { Key = "row_count", Value = "12345" });
        grpc.Stats.Add(new Grpc.KeyValuePair { Key = "custom", Value = "xyz" });

        GetPartitionStatsResp response = GetPartitionStatsResp.FromGrpc(grpc);

        Assert.Equal(12345, response.RowCount);
        Assert.Equal(2, response.Stats.Count);
        Assert.Equal("xyz", response.Stats["custom"]);
    }

    [Fact]
    public void GetPartitionStatsResp_defaults_row_count_when_stat_missing()
    {
        var grpc = new Grpc.GetPartitionStatisticsResponse();

        GetPartitionStatsResp response = GetPartitionStatsResp.FromGrpc(grpc);

        Assert.Equal(0, response.RowCount);
    }

    [Fact]
    public void HasPartitionResp_maps_true()
    {
        HasPartitionResp response = HasPartitionResp.FromGrpc(new Grpc.BoolResponse { Value = true });
        Assert.True(response.Has);
    }

    [Fact]
    public void HasPartitionResp_maps_false()
    {
        HasPartitionResp response = HasPartitionResp.FromGrpc(new Grpc.BoolResponse { Value = false });
        Assert.False(response.Has);
    }

    [Fact]
    public void ListPartitionsResp_maps_names_and_ids()
    {
        var grpc = new Grpc.ShowPartitionsResponse();
        grpc.PartitionNames.Add("p1");
        grpc.PartitionNames.Add("p2");
        grpc.PartitionIDs.Add(101);
        grpc.PartitionIDs.Add(202);

        ListPartitionsResp response = ListPartitionsResp.FromGrpc(grpc);

        Assert.Equal(new[] { "p1", "p2" }, response.PartitionNames);
        Assert.Equal(new long[] { 101, 202 }, response.PartitionIds);
    }
}
