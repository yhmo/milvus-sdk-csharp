using Milvus.Client.V2.Requests.Collection;
using Milvus.Client.V2.Requests.Dml;
using Milvus.Client.V2.Responses.Collection;
using Milvus.Client.V2.Responses.Dml;
using Milvus.Client.V2.Utils;

namespace Milvus.Client.V2;

public sealed partial class MilvusClientV2
{
    /// <summary>
    /// Inserts rows into a collection, recording the mutation timestamp for Session consistency
    /// (see design doc §5.1.2).
    /// </summary>
    /// <param name="request">The request containing the collection name and field data.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    public Task<MutationResp> InsertAsync(
        InsertReq request,
        CancellationToken cancellationToken = default)
        => InsertAsync(request, allowRetry: true, cancellationToken);

    private async Task<MutationResp> InsertAsync(
        InsertReq request,
        bool allowRetry,
        CancellationToken cancellationToken)
    {
        Verify.NotNull(request);

        await EnsureConnectedAsync(cancellationToken).ConfigureAwait(false);

        // Describe the collection (cache-served) to stamp the request with the schema timestamp, so the
        // server can detect a stale schema and return SchemaMismatch instead of mis-encoding the data.
        DescribeCollectionResp description = await DescribeCollectionAsync(
            new Requests.Collection.DescribeCollectionReq { CollectionName = request.CollectionName },
            cancellationToken).ConfigureAwait(false);

        request.SchemaTimestamp = description.UpdateTimestamp;
        Grpc.InsertRequest grpcRequest = request.ToGrpcInsertRequest();
        try
        {
            Grpc.MutationResult response = await InvokeAsync(
                    GrpcClient.InsertAsync, grpcRequest, static r => r.Status, cancellationToken)
                .ConfigureAwait(false);

            CollectionTsCache.Instance.Set(_endpoint, CurrentDatabase, request.CollectionName, unchecked((long)response.Timestamp));

            return MutationResp.FromGrpc(response);
        }
        catch (MilvusException ex) when (ex.ErrorCode == MilvusErrorCode.SchemaMismatch && allowRetry)
        {
            // The collection was recreated or its schema changed by another client between our last describe
            // and this insert. Drop the stale cached schema and retry once, mirroring the Java/C++ SDKs.
            SchemaCache.Instance.Invalidate(_endpoint, CurrentDatabase, request.CollectionName);
            return await InsertAsync(request, allowRetry: false, cancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Upserts (inserts or updates) rows into a collection, recording the mutation timestamp for Session
    /// consistency.
    /// </summary>
    /// <param name="request">The request containing the collection name and field data.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    public Task<MutationResp> UpsertAsync(
        UpsertReq request,
        CancellationToken cancellationToken = default)
        => UpsertAsync(request, allowRetry: true, cancellationToken);

    private async Task<MutationResp> UpsertAsync(
        UpsertReq request,
        bool allowRetry,
        CancellationToken cancellationToken)
    {
        Verify.NotNull(request);

        await EnsureConnectedAsync(cancellationToken).ConfigureAwait(false);

        // Same schema-timestamp stamping as InsertAsync (see above).
        DescribeCollectionResp description = await DescribeCollectionAsync(
            new Requests.Collection.DescribeCollectionReq { CollectionName = request.CollectionName },
            cancellationToken).ConfigureAwait(false);

        request.SchemaTimestamp = description.UpdateTimestamp;
        Grpc.UpsertRequest grpcRequest = request.ToGrpcUpsertRequest();
        try
        {
            Grpc.MutationResult response = await InvokeAsync(
                    GrpcClient.UpsertAsync, grpcRequest, static r => r.Status, cancellationToken)
                .ConfigureAwait(false);

            CollectionTsCache.Instance.Set(_endpoint, CurrentDatabase, request.CollectionName, unchecked((long)response.Timestamp));

            return MutationResp.FromGrpc(response);
        }
        catch (MilvusException ex) when (ex.ErrorCode == MilvusErrorCode.SchemaMismatch && allowRetry)
        {
            // Same recreate/schema-change recovery as InsertAsync.
            SchemaCache.Instance.Invalidate(_endpoint, CurrentDatabase, request.CollectionName);
            return await UpsertAsync(request, allowRetry: false, cancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Deletes rows from a collection by expression, recording the mutation timestamp for Session consistency.
    /// </summary>
    /// <param name="request">The request containing the collection name and delete expression.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    public async Task<MutationResp> DeleteAsync(
        DeleteReq request,
        CancellationToken cancellationToken = default)
    {
        Verify.NotNull(request);

        await EnsureConnectedAsync(cancellationToken).ConfigureAwait(false);

        string? primaryKeyField = null;
        if (request.Ids is { Count: > 0 })
        {
            DescribeCollectionResp description = await DescribeCollectionAsync(
                new Requests.Collection.DescribeCollectionReq { CollectionName = request.CollectionName },
                cancellationToken).ConfigureAwait(false);

            primaryKeyField = description.Schema.Fields.SingleOrDefault(f => f.IsPrimaryKey)?.Name
                ?? throw new MilvusException(MilvusErrorCode.UnexpectedError,
                    $"Collection '{request.CollectionName}' has no primary key field.");
        }

        Grpc.DeleteRequest grpcRequest = request.ToGrpcDeleteRequest(primaryKeyField);
        Grpc.MutationResult response = await InvokeAsync(
                GrpcClient.DeleteAsync, grpcRequest, static r => r.Status, cancellationToken)
            .ConfigureAwait(false);

        CollectionTsCache.Instance.Set(_endpoint, CurrentDatabase, request.CollectionName, unchecked((long)response.Timestamp));

        return MutationResp.FromGrpc(response);
    }
}
