using Milvus.Client.V2.Requests.Index;
using Milvus.Client.V2.Responses.Index;
using Milvus.Client.V2.Types;
using Milvus.Client.V2.Utils;

namespace Milvus.Client.V2;

public sealed partial class MilvusClientV2
{
    /// <summary>
    /// Creates an index on a field.
    /// </summary>
    /// <param name="request">The request containing the collection/field names and index parameters.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    public async Task CreateIndexAsync(
        CreateIndexReq request,
        CancellationToken cancellationToken = default)
    {
        Verify.NotNull(request);

        await EnsureConnectedAsync(cancellationToken).ConfigureAwait(false);

        Grpc.CreateIndexRequest grpcRequest = request.ToGrpcCreateIndexRequest();
        await InvokeAsync(GrpcClient.CreateIndexAsync, grpcRequest, cancellationToken).ConfigureAwait(false);

        if (!request.Sync)
        {
            return;
        }

        // Wait for the index to be fully built, mirroring the C++ and Java SDKs: poll DescribeIndex every
        // 500 ms until the index reaches Finished (or None), fail on Failed, and stop once the timeout elapses.
        DateTime? deadline = request.TimeoutMs > 0
            ? DateTime.UtcNow + TimeSpan.FromMilliseconds(request.TimeoutMs)
            : null;

        while (true)
        {
            DescribeIndexResp description = await DescribeIndexAsync(
                new DescribeIndexReq
                {
                    CollectionName = request.CollectionName,
                    FieldName = request.FieldName,
                    IndexName = request.IndexName
                },
                cancellationToken).ConfigureAwait(false);

            IndexDescription? index = description.Indexes.Count > 0 ? description.Indexes[0] : null;
            if (index is null)
            {
                throw new MilvusException(
                    MilvusErrorCode.UnexpectedError,
                    $"Index on field '{request.FieldName}' of collection '{request.CollectionName}' could not be described.");
            }

            if (index.State is IndexState.Finished or IndexState.None)
            {
                return;
            }

            if (index.State == IndexState.Failed)
            {
                throw new MilvusException(
                    MilvusErrorCode.UnexpectedError,
                    $"Index on field '{request.FieldName}' of collection '{request.CollectionName}' failed to build"
                    + (string.IsNullOrEmpty(index.IndexStateFailReason) ? "" : $": {index.IndexStateFailReason}"));
            }

            if (deadline is not null && DateTime.UtcNow >= deadline)
            {
                throw new TimeoutException($"Timed out waiting for the index on field '{request.FieldName}' to build.");
            }

            await Task.Delay(500, cancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Drops an index.
    /// </summary>
    /// <param name="request">The request containing the collection/field names and the index name.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    public async Task DropIndexAsync(
        DropIndexReq request,
        CancellationToken cancellationToken = default)
    {
        Verify.NotNull(request);

        await EnsureConnectedAsync(cancellationToken).ConfigureAwait(false);

        Grpc.DropIndexRequest grpcRequest = request.ToGrpcDropIndexRequest();
        await InvokeAsync(GrpcClient.DropIndexAsync, grpcRequest, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Describes the indexes on a field of a collection.
    /// </summary>
    /// <param name="request">The request containing the collection/field names and the index name.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    public async Task<DescribeIndexResp> DescribeIndexAsync(
        DescribeIndexReq request,
        CancellationToken cancellationToken = default)
    {
        Verify.NotNull(request);

        await EnsureConnectedAsync(cancellationToken).ConfigureAwait(false);

        Grpc.DescribeIndexRequest grpcRequest = request.ToGrpcDescribeIndexRequest();
        Grpc.DescribeIndexResponse response = await InvokeAsync(
                GrpcClient.DescribeIndexAsync, grpcRequest, static r => r.Status, cancellationToken)
            .ConfigureAwait(false);

        return DescribeIndexResp.FromGrpc(response);
    }

    /// <summary>
    /// Lists the indexes of a collection.
    /// </summary>
    /// <param name="request">The request containing the collection name.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    public async Task<ListIndexesResp> ListIndexesAsync(
        ListIndexesReq request,
        CancellationToken cancellationToken = default)
    {
        Verify.NotNull(request);

        await EnsureConnectedAsync(cancellationToken).ConfigureAwait(false);

        Grpc.DescribeIndexRequest grpcRequest = request.ToGrpcDescribeIndexRequest();
        try
        {
            Grpc.DescribeIndexResponse response = await InvokeAsync(
                    GrpcClient.DescribeIndexAsync, grpcRequest, static r => r.Status, cancellationToken)
                .ConfigureAwait(false);

            return ListIndexesResp.FromGrpc(response);
        }
        catch (MilvusException ex) when (ex.ErrorCode == MilvusErrorCode.IndexNotFound)
        {
            // A collection with no index reports IndexNotFound (code 700); return an empty list instead of
            // surfacing the error, matching the C++ and Java SDKs.
            return new ListIndexesResp(Array.Empty<IndexDescription>());
        }
    }

    /// <summary>
    /// Alters the properties of an index.
    /// </summary>
    public async Task AlterIndexPropertiesAsync(AlterIndexPropertiesReq request, CancellationToken cancellationToken = default)
    {
        Verify.NotNull(request);
        await EnsureConnectedAsync(cancellationToken).ConfigureAwait(false);
        Grpc.AlterIndexRequest grpcRequest = request.ToGrpcRequest();
        await InvokeAsync(GrpcClient.AlterIndexAsync, grpcRequest, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Drops the properties of an index.
    /// </summary>
    public async Task DropIndexPropertiesAsync(DropIndexPropertiesReq request, CancellationToken cancellationToken = default)
    {
        Verify.NotNull(request);
        await EnsureConnectedAsync(cancellationToken).ConfigureAwait(false);
        Grpc.AlterIndexRequest grpcRequest = request.ToGrpcRequest();
        await InvokeAsync(GrpcClient.AlterIndexAsync, grpcRequest, cancellationToken).ConfigureAwait(false);
    }
}
