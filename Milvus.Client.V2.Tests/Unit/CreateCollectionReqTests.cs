using Xunit;

using Milvus.Client.V2.Requests.Collection;
using Milvus.Client.V2.Requests.Index;
using Milvus.Client.V2.Types;

namespace Milvus.Client.V2.Tests.Unit;

[Trait("Category", "Unit")]
public class CreateCollectionReqTests
{
    [Fact]
    public void ToGrpc_maps_schema_fields_and_type_params()
    {
        var request = new CreateCollectionReq
        {
            CollectionName = "book",
            ConsistencyLevel = ConsistencyLevel.Strong,
            ShardsNum = 2,
            Schema = new CollectionSchema
            {
                Name = "book",
                Description = "books",
                EnableDynamicFields = true,
                Fields =
                {
                    new FieldSchema("id", DataType.Int64, isPrimaryKey: true),
                    FieldSchema.CreateVarchar("title", maxLength: 100),
                    FieldSchema.CreateFloatVector("embedding", dimension: 4)
                }
            }
        };

        Grpc.CreateCollectionRequest grpc = request.ToGrpcCreateCollectionRequest();

        Assert.Equal("book", grpc.CollectionName);
        Assert.Equal((int)ConsistencyLevel.Strong, (int)grpc.ConsistencyLevel);
        Assert.Equal(2, grpc.ShardsNum);

        Grpc.CollectionSchema schema = Grpc.CollectionSchema.Parser.ParseFrom(grpc.Schema);
        Assert.Equal("book", schema.Name);
        Assert.Equal("books", schema.Description);
        Assert.True(schema.EnableDynamicField);
        Assert.Equal(3, schema.Fields.Count);

        Grpc.FieldSchema title = schema.Fields.Single(f => f.Name == "title");
        Assert.Equal((int)DataType.VarChar, (int)title.DataType);
        Assert.Equal("100", title.TypeParams.Single(p => p.Key == "max_length").Value);

        Grpc.FieldSchema vector = schema.Fields.Single(f => f.Name == "embedding");
        Assert.Equal((int)DataType.FloatVector, (int)vector.DataType);
        Assert.Equal("4", vector.TypeParams.Single(p => p.Key == "dim").Value);
    }

    [Fact]
    public void ToGrpc_maps_functions_properties_and_num_partitions()
    {
        var request = new CreateCollectionReq
        {
            CollectionName = "book",
            Schema = new CollectionSchema
            {
                Fields =
                {
                    new FieldSchema("id", DataType.Int64, isPrimaryKey: true),
                    FieldSchema.CreateVarchar("title", maxLength: 100),
                    FieldSchema.CreateFloatVector("embedding", dimension: 4),
                    FieldSchema.CreateSparseFloatVector("sparse")
                },
                Functions =
                {
                    FunctionSchema.CreateBm25("bm25", "title", "sparse", "full-text search")
                }
            },
            NumPartitions = 8
        };
        request.Properties["collection.ttl.seconds"] = "3600";

        Grpc.CreateCollectionRequest grpc = request.ToGrpcCreateCollectionRequest();

        Assert.Equal(8, grpc.NumPartitions);
        Grpc.KeyValuePair property = Assert.Single(grpc.Properties);
        Assert.Equal("collection.ttl.seconds", property.Key);
        Assert.Equal("3600", property.Value);

        Grpc.CollectionSchema schema = Grpc.CollectionSchema.Parser.ParseFrom(grpc.Schema);
        Grpc.FunctionSchema function = Assert.Single(schema.Functions);
        Assert.Equal("bm25", function.Name);
        Assert.Equal((int)FunctionType.Bm25, (int)function.Type);
        Assert.Equal(new[] { "title" }, function.InputFieldNames);
        Assert.Equal(new[] { "sparse" }, function.OutputFieldNames);
        Assert.Equal("full-text search", function.Description);
    }

    [Fact]
    public void ToGrpc_throws_when_collection_name_blank()
    {
        var request = new CreateCollectionReq
        {
            CollectionName = " ",
            Schema = new CollectionSchema { Fields = { new FieldSchema("id", DataType.Int64) } }
        };

        Assert.Throws<ArgumentException>(() => request.ToGrpcCreateCollectionRequest());
    }

    [Fact]
    public void ToGrpc_throws_when_schema_missing()
    {
        var request = new CreateCollectionReq { CollectionName = "book" };

        Assert.Throws<ArgumentNullException>(() => request.ToGrpcCreateCollectionRequest());
    }

    [Fact]
    public void ToGrpc_rejects_json_default_values()
    {
        var request = new CreateCollectionReq
        {
            CollectionName = "book",
            Schema = new CollectionSchema
            {
                Fields =
                {
                    new FieldSchema("id", DataType.Int64, isPrimaryKey: true)
                    {
                        DefaultValue = 1
                    },
                    new FieldSchema("meta", DataType.Json)
                    {
                        DefaultValue = "{\"a\":1}"
                    }
                }
            }
        };

        // The server only accepts JSON defaults on dynamic fields (which cannot be created), so the SDK must
        // fail fast instead of sending a request the server always rejects.
        Assert.Throws<NotSupportedException>(() => request.ToGrpcCreateCollectionRequest());
    }

    [Fact]
    public void IndexDesc_to_create_index_req_carries_fields()
    {
        var index = new IndexDesc("embedding", "idx1", IndexType.Hnsw, SimilarityMetricType.Cosine);
        index.ExtraParams["M"] = "16";

        CreateIndexReq createIndexReq = index.ToCreateIndexReq("book");

        Assert.Equal("book", createIndexReq.CollectionName);
        Assert.Equal("embedding", createIndexReq.FieldName);
        Assert.Equal("idx1", createIndexReq.IndexName);
        Assert.Equal(IndexType.Hnsw, createIndexReq.IndexType);
        Assert.Equal(SimilarityMetricType.Cosine, createIndexReq.MetricType);
        Assert.Equal("16", createIndexReq.ExtraParams["M"]);
    }
}
