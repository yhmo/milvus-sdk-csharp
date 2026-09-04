using Milvus.Client.V2.Responses.Collection;

using Grpc.Core;
using Milvus.Client.V2.Requests.Index;
using Milvus.Client.V2.Requests.Utility;
using Milvus.Client.V2.Responses.Index;
using Milvus.Client.V2.Responses.Utility;
using Milvus.Client.V2.Types;
using Milvus.Client.V2.Utils;

namespace Milvus.Client.V2;

public sealed partial class MilvusClientV2
{
    /// <summary>
    /// Compacts a collection (merging segments), returning the compaction id.
    /// </summary>
    public async Task<CompactResp> CompactAsync(CompactReq request, CancellationToken cancellationToken = default)
    {
        Verify.NotNull(request);
        await EnsureConnectedAsync(cancellationToken).ConfigureAwait(false);

        DescribeCollectionResp description = await DescribeCollectionAsync(
            new Requests.Collection.DescribeCollectionReq { CollectionName = request.CollectionName },
            cancellationToken).ConfigureAwait(false);

        Grpc.ManualCompactionRequest grpcRequest = request.ToGrpcManualCompactionRequest(description.CollectionId);
        Grpc.ManualCompactionResponse response = await InvokeAsync(
            GrpcClient.ManualCompactionAsync, grpcRequest, static r => r.Status, cancellationToken).ConfigureAwait(false);
        return CompactResp.FromGrpc(response);
    }

    /// <summary>
    /// Gets the state of a compaction.
    /// </summary>
    public async Task<GetCompactionStateResp> GetCompactionStateAsync(
        GetCompactionStateReq request, CancellationToken cancellationToken = default)
    {
        Verify.NotNull(request);
        await EnsureConnectedAsync(cancellationToken).ConfigureAwait(false);
        Grpc.GetCompactionStateRequest grpcRequest = request.ToGrpcGetCompactionStateRequest();
        Grpc.GetCompactionStateResponse response = await InvokeAsync(
            GrpcClient.GetCompactionStateAsync, grpcRequest, static r => r.Status, cancellationToken).ConfigureAwait(false);
        return GetCompactionStateResp.FromGrpc(response);
    }

    /// <summary>
    /// Gets the compaction plans for a compaction.
    /// </summary>
    public async Task<GetCompactionPlansResp> GetCompactionPlansAsync(
        GetCompactionPlansReq request, CancellationToken cancellationToken = default)
    {
        Verify.NotNull(request);
        await EnsureConnectedAsync(cancellationToken).ConfigureAwait(false);
        Grpc.GetCompactionPlansRequest grpcRequest = request.ToGrpcGetCompactionPlansRequest();
        Grpc.GetCompactionPlansResponse response = await InvokeAsync(
            GrpcClient.GetCompactionStateWithPlansAsync, grpcRequest, static r => r.Status, cancellationToken).ConfigureAwait(false);
        return GetCompactionPlansResp.FromGrpc(response);
    }

    /// <summary>
    /// Flushes the given collections, optionally waiting for the flush to complete.
    /// </summary>
    public async Task<FlushResp> FlushAsync(FlushReq request, CancellationToken cancellationToken = default)
    {
        Verify.NotNull(request);
        await EnsureConnectedAsync(cancellationToken).ConfigureAwait(false);
        Grpc.FlushRequest grpcRequest = request.ToGrpcFlushRequest();
        Grpc.FlushResponse response = await InvokeAsync(
            GrpcClient.FlushAsync, grpcRequest, static r => r.Status, cancellationToken).ConfigureAwait(false);

        FlushResp flushResp = FlushResp.FromGrpc(response);
        if (flushResp.CollSegIDs.Count == 0)
        {
            return flushResp;
        }

        // Wait for the segments to be flushed, mirroring the C++ SDK: poll GetFlushState every 500 ms until all
        // segments are flushed or the WaitFlushedMs timeout elapses (0 means wait forever).
        DateTime? deadline = request.WaitFlushedMs > 0
            ? DateTime.UtcNow + TimeSpan.FromMilliseconds(request.WaitFlushedMs)
            : null;

        var remaining = new Dictionary<string, IReadOnlyList<long>>();
        foreach (KeyValuePair<string, IReadOnlyList<long>> pair in flushResp.CollSegIDs)
        {
            remaining[pair.Key] = pair.Value;
        }

        while (remaining.Count > 0)
        {
            foreach (KeyValuePair<string, IReadOnlyList<long>> pair in remaining.ToList())
            {
                GetFlushStateResp state = await GetFlushStateAsync(
                    new GetFlushStateReq { CollectionName = pair.Key, SegmentIds = pair.Value },
                    cancellationToken).ConfigureAwait(false);

                if (state.Flushed)
                {
                    remaining.Remove(pair.Key);
                }
            }

            if (remaining.Count == 0)
            {
                break;
            }

            if (deadline is not null && DateTime.UtcNow >= deadline)
            {
                throw new TimeoutException($"Timed out waiting for the flush to complete.");
            }

            await Task.Delay(500, cancellationToken).ConfigureAwait(false);
        }

        return flushResp;
    }

    /// <summary>
    /// Checks whether the given segments have been flushed.
    /// </summary>
    public async Task<GetFlushStateResp> GetFlushStateAsync(
        GetFlushStateReq request, CancellationToken cancellationToken = default)
    {
        Verify.NotNull(request);
        await EnsureConnectedAsync(cancellationToken).ConfigureAwait(false);
        Grpc.GetFlushStateRequest grpcRequest = request.ToGrpcGetFlushStateRequest();
        Grpc.GetFlushStateResponse response = await InvokeAsync(
            GrpcClient.GetFlushStateAsync, grpcRequest, static r => r.Status, cancellationToken).ConfigureAwait(false);
        return GetFlushStateResp.FromGrpc(response);
    }

    /// <summary>
    /// Gets the persistent segment info of a collection.
    /// </summary>
    public async Task<GetPersistentSegmentInfoResp> GetPersistentSegmentInfoAsync(
        GetPersistentSegmentInfoReq request, CancellationToken cancellationToken = default)
    {
        Verify.NotNull(request);
        await EnsureConnectedAsync(cancellationToken).ConfigureAwait(false);
        Grpc.GetPersistentSegmentInfoRequest grpcRequest = request.ToGrpcGetPersistentSegmentInfoRequest();
        Grpc.GetPersistentSegmentInfoResponse response = await InvokeAsync(
            GrpcClient.GetPersistentSegmentInfoAsync, grpcRequest, static r => r.Status, cancellationToken).ConfigureAwait(false);
        return GetPersistentSegmentInfoResp.FromGrpc(response);
    }

    /// <summary>
    /// Flushes all collections.
    /// </summary>
    public async Task<FlushAllResp> FlushAllAsync(FlushAllReq request, CancellationToken cancellationToken = default)
    {
        Verify.NotNull(request);
        await EnsureConnectedAsync(cancellationToken).ConfigureAwait(false);
        Grpc.FlushAllRequest grpcRequest = FlushAllReq.ToGrpcFlushAllRequest();
        Grpc.FlushAllResponse response = await InvokeAsync(
            GrpcClient.FlushAllAsync, grpcRequest, static r => r.Status, cancellationToken).ConfigureAwait(false);
        return FlushAllResp.FromGrpc(response);
    }

    /// <summary>
    /// Gets the state of a flush-all operation.
    /// </summary>
    public async Task<GetFlushAllStateResp> GetFlushAllStateAsync(
        GetFlushAllStateReq request, CancellationToken cancellationToken = default)
    {
        Verify.NotNull(request);
        await EnsureConnectedAsync(cancellationToken).ConfigureAwait(false);
        Grpc.GetFlushAllStateRequest grpcRequest = request.ToGrpcGetFlushAllStateRequest();
        Grpc.GetFlushAllStateResponse response = await InvokeAsync(
            GrpcClient.GetFlushAllStateAsync, grpcRequest, static r => r.Status, cancellationToken).ConfigureAwait(false);
        return GetFlushAllStateResp.FromGrpc(response);
    }

    /// <summary>
    /// Gets the loaded query-segment info of a collection.
    /// </summary>
    public async Task<GetQuerySegmentInfoResp> GetQuerySegmentInfoAsync(
        GetQuerySegmentInfoReq request, CancellationToken cancellationToken = default)
    {
        Verify.NotNull(request);
        await EnsureConnectedAsync(cancellationToken).ConfigureAwait(false);
        Grpc.GetQuerySegmentInfoRequest grpcRequest = request.ToGrpcGetQuerySegmentInfoRequest();
        Grpc.GetQuerySegmentInfoResponse response = await InvokeAsync(
            GrpcClient.GetQuerySegmentInfoAsync, grpcRequest, static r => r.Status, cancellationToken).ConfigureAwait(false);
        return GetQuerySegmentInfoResp.FromGrpc(response);
    }

    /// <summary>
    /// Runs the text analyzer on the given strings, returning the analyzed tokens.
    /// </summary>
    public async Task<RunAnalyzerResp> RunAnalyzerAsync(
        RunAnalyzerReq request, CancellationToken cancellationToken = default)
    {
        Verify.NotNull(request);
        await EnsureConnectedAsync(cancellationToken).ConfigureAwait(false);
        Grpc.RunAnalyzerRequest grpcRequest = request.ToGrpcRunAnalyzerRequest();
        Grpc.RunAnalyzerResponse response = await InvokeAsync(
            GrpcClient.RunAnalyzerAsync, grpcRequest, static r => r.Status, cancellationToken).ConfigureAwait(false);
        return RunAnalyzerResp.FromGrpc(response);
    }

    /// <summary>
    /// Gets the metrics of the Milvus server.
    /// </summary>
    public async Task<GetMetricsResp> GetMetricsAsync(GetMetricsReq request, CancellationToken cancellationToken = default)
    {
        Verify.NotNull(request);
        await EnsureConnectedAsync(cancellationToken).ConfigureAwait(false);
        Grpc.GetMetricsRequest grpcRequest = request.ToGrpcGetMetricsRequest();
        Grpc.GetMetricsResponse response = await InvokeAsync(
            GrpcClient.GetMetricsAsync, grpcRequest, static r => r.Status, cancellationToken).ConfigureAwait(false);
        return GetMetricsResp.FromGrpc(response);
    }

    /// <summary>
    /// Gets replication info about a physical channel.
    /// </summary>
    public async Task<GetReplicateInfoResp> GetReplicateInfoAsync(
        GetReplicateInfoReq request, CancellationToken cancellationToken = default)
    {
        Verify.NotNull(request);
        await EnsureConnectedAsync(cancellationToken).ConfigureAwait(false);
        Grpc.GetReplicateInfoRequest grpcRequest = request.ToGrpcRequest();
        try
        {
            Grpc.GetReplicateInfoResponse response = await RetryPolicy.ExecuteAsync(
                async innerCt => await GrpcClient.GetReplicateInfoAsync(
                        grpcRequest, CreateCallOptions(innerCt)).ConfigureAwait(false),
                _retryConfig,
                cancellationToken).ConfigureAwait(false);
            return GetReplicateInfoResp.FromGrpc(response);
        }
        catch (RpcException ex)
        {
            // GetReplicateInfoResponse carries no status field, so it cannot go through InvokeAsync; apply the
            // same CreateCallOptions deadline and RpcException->MilvusException wrapping for a consistent surface,
            // preserving the original gRPC status code.
            throw new MilvusException(
                MilvusErrorCode.UnexpectedError, $"RPC failed: {ex.StatusCode} {ex.Status.Detail}",
                ex.StatusCode, ex);
        }
    }

    /// <summary>
    /// Updates the replication configuration of a cluster.
    /// </summary>
    public async Task<UpdateReplicateConfigurationResp> UpdateReplicateConfigurationAsync(
        UpdateReplicateConfigurationReq request, CancellationToken cancellationToken = default)
    {
        Verify.NotNull(request);
        await EnsureConnectedAsync(cancellationToken).ConfigureAwait(false);
        Grpc.UpdateReplicateConfigurationRequest grpcRequest = request.ToGrpcRequest();
        await InvokeAsync(GrpcClient.UpdateReplicateConfigurationAsync, grpcRequest, cancellationToken).ConfigureAwait(false);
        return UpdateReplicateConfigurationResp.FromGrpc();
    }

    /// <summary>
    /// Switches the default database for this client.
    /// </summary>
    /// <remarks>
    /// This is a synchronous client-side state switch: no RPC is issued, the switch applies immediately, and
    /// the returned task is already completed. The <c>Async</c> suffix is kept for a uniform public surface
    /// (the Java and C++ SDKs expose the same synchronous <c>useDatabase</c>/<c>UseDatabase</c>).
    /// </remarks>
    public Task UseDatabaseAsync(UseDatabaseReq request, CancellationToken cancellationToken = default)
    {
        Verify.NotNull(request);
        request.Validate();

        // Guard the header/database swap so a concurrent UseDatabaseAsync cannot interleave the dbname
        // metadata header with the CurrentDatabase cache key (which would make RPCs hit one database while
        // session-consistency reads consult another).
        lock (_connectLock)
        {
            string? authorization = _authorizationHeader;

            var metadata = new Metadata();
            if (authorization is not null)
            {
                metadata.Add("authorization", authorization);
            }

            metadata.Add("dbname", request.DatabaseName);
            _callOptions = _callOptions.WithHeaders(metadata);
            _database = request.DatabaseName;
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// Gets the replication configuration of a cluster.
    /// </summary>
    public async Task<GetReplicateConfigurationResp> GetReplicateConfigurationAsync(
        GetReplicateConfigurationReq request, CancellationToken cancellationToken = default)
    {
        Verify.NotNull(request);
        await EnsureConnectedAsync(cancellationToken).ConfigureAwait(false);
        Grpc.GetReplicateConfigurationRequest grpcRequest = GetReplicateConfigurationReq.ToGrpcRequest();
        Grpc.GetReplicateConfigurationResponse response = await InvokeAsync(
            GrpcClient.GetReplicateConfigurationAsync, grpcRequest, static r => r.Status, cancellationToken).ConfigureAwait(false);
        return GetReplicateConfigurationResp.FromGrpc(response);
    }

    /// <summary>
    /// Dumps CDC messages from a physical channel, as a stream of <see cref="DumpMessageInfo" />.
    /// </summary>
    /// <remarks>
    /// Consume with <c>await foreach</c>, optionally with <c>.WithCancellation(token)</c>.
    /// </remarks>
    public DumpMessagesResp DumpMessagesAsync(DumpMessagesReq request)
    {
        Verify.NotNull(request);
        Grpc.DumpMessagesRequest grpcRequest = request.ToGrpcRequest();
        return new DumpMessagesResp(ct => DumpMessagesReader.ReadAsync(this, grpcRequest, ct));
    }

    /// <summary>
    /// Optimizes a collection (rebuilding indexes and compacting segments).
    /// </summary>
    /// <param name="request">The optimize request.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    public async Task<OptimizeResp> OptimizeAsync(OptimizeReq request, CancellationToken cancellationToken = default)
    {
        Verify.NotNull(request);
        request.Validate();
        await EnsureConnectedAsync(cancellationToken).ConfigureAwait(false);

        DateTime? deadline = request.TimeoutMilliseconds is { } timeout
            ? DateTime.UtcNow + TimeSpan.FromMilliseconds(timeout)
            : null;

        // Wait for all indexes to be built.
        await WaitForIndexesAsync(request.CollectionName, deadline, cancellationToken).ConfigureAwait(false);

        // Trigger a major compaction with the optional target size.
        CompactResp compact = await CompactAsync(new CompactReq
        {
            CollectionName = request.CollectionName,
            IsMajorCompaction = true,
            TargetSize = request.TargetSizeInMB
        }, cancellationToken).ConfigureAwait(false);

        if (request.WaitForCompletion)
        {
            // Wait for the compaction to complete.
            while (true)
            {
                GetCompactionStateResp state = await GetCompactionStateAsync(
                    new GetCompactionStateReq { CompactionId = compact.CompactionId },
                    cancellationToken).ConfigureAwait(false);
                if (state.State == CompactionState.Completed)
                {
                    break;
                }

                if (deadline is not null && DateTime.UtcNow >= deadline)
                {
                    throw new TimeoutException($"Timed out waiting for compaction '{compact.CompactionId}' of collection '{request.CollectionName}' to complete.");
                }

                await Task.Delay(500, cancellationToken).ConfigureAwait(false);
            }

            // Wait for the indexes to be rebuilt after compaction.
            await WaitForIndexesAsync(request.CollectionName, deadline, cancellationToken).ConfigureAwait(false);

            // Refresh the loaded data if the collection is loaded.
            GetLoadStateResp loadState = await GetLoadStateAsync(
                new Requests.Collection.GetLoadStateReq { CollectionName = request.CollectionName },
                cancellationToken).ConfigureAwait(false);
            if (loadState.State == LoadState.Loaded)
            {
                await RefreshLoadAsync(new Requests.Collection.RefreshLoadReq
                {
                    CollectionName = request.CollectionName,
                    Sync = true,
                    TimeoutMilliseconds = request.TimeoutMilliseconds
                }, cancellationToken).ConfigureAwait(false);
            }
        }

        return new OptimizeResp(
            compact.CompactionId,
            request.TargetSizeInMB is { } size ? size + "MB" : null);
    }

    private async Task WaitForIndexesAsync(
        string collectionName, DateTime? deadline, CancellationToken cancellationToken)
    {
        while (true)
        {
            ListIndexesResp indexes = await ListIndexesAsync(
                new ListIndexesReq { CollectionName = collectionName },
                cancellationToken).ConfigureAwait(false);

            bool allFinished = true;
            foreach (IndexDescription index in indexes.Indexes)
            {
                if (index.State == IndexState.Failed)
                {
                    throw new MilvusException(MilvusErrorCode.UnexpectedError,
                        $"Index build failed for '{index.IndexName}' on collection '{collectionName}': {index.IndexStateFailReason}");
                }

                if (index.State != IndexState.Finished)
                {
                    allFinished = false;
                }
            }

            if (allFinished)
            {
                return;
            }

            if (deadline is not null && DateTime.UtcNow >= deadline)
            {
                throw new TimeoutException($"Timed out waiting for indexes of collection '{collectionName}' to finish.");
            }

            await Task.Delay(500, cancellationToken).ConfigureAwait(false);
        }
    }
}
