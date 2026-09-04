using System.Globalization;
using System.Runtime.CompilerServices;

using Google.Protobuf.Collections;

using Milvus.Client.V2.Responses.Collection;

using Milvus.Client.V2.Requests.Dql;
using Milvus.Client.V2.Responses.Dql;
using Milvus.Client.V2.Types;
using Milvus.Client.V2.Utils;

namespace Milvus.Client.V2;

public sealed partial class MilvusClientV2
{
    /// <summary>
    /// Performs a vector similarity search.
    /// </summary>
    /// <param name="request">The search request.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    public async Task<SearchResp> SearchAsync(
        SearchReq request,
        CancellationToken cancellationToken = default)
    {
        Verify.NotNull(request);

        await EnsureConnectedAsync(cancellationToken).ConfigureAwait(false);

        Grpc.SearchRequest grpcRequest = BuildSearchRequest(request);
        Grpc.SearchResults response = await InvokeAsync(
                GrpcClient.SearchAsync, grpcRequest, static r => r.Status, cancellationToken)
            .ConfigureAwait(false);

        return SearchResp.FromGrpc(response);
    }

    /// <summary>
    /// Performs a hybrid search, combining the results of multiple ANN searches with a reranker.
    /// </summary>
    /// <param name="request">The hybrid search request.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    public async Task<SearchResp> HybridSearchAsync(
        HybridSearchReq request,
        CancellationToken cancellationToken = default)
    {
        Verify.NotNull(request);
        request.Validate();

        await EnsureConnectedAsync(cancellationToken).ConfigureAwait(false);

        var grpcRequest = new Grpc.HybridSearchRequest { CollectionName = request.CollectionName };

        foreach (SearchReq subRequest in request.SearchRequests)
        {
            grpcRequest.Requests.Add(BuildHybridSearchSubRequest(subRequest));
        }

        grpcRequest.RankParams.AddRange(ToGrpcKeyValuePairs(request.Reranker.ToRankParams()));
        grpcRequest.RankParams.Add(new Grpc.KeyValuePair
        {
            Key = "limit",
            Value = request.Limit.ToString(System.Globalization.CultureInfo.InvariantCulture)
        });

        SearchParameters? parameters = request.Parameters;
        if (parameters is not null)
        {
            if (parameters.PartitionNamesInternal?.Count > 0)
            {
                grpcRequest.PartitionNames.AddRange(parameters.PartitionNamesInternal);
            }
            if (parameters.OutputFieldsInternal?.Count > 0)
            {
                grpcRequest.OutputFields.AddRange(parameters.OutputFieldsInternal);
            }
            if (parameters.TimeTravelTimestamp is not null)
            {
                grpcRequest.TravelTimestamp = parameters.TimeTravelTimestamp.Value;
            }
            if (parameters.Offset is not null)
            {
                grpcRequest.RankParams.Add(new Grpc.KeyValuePair { Key = "offset", Value = parameters.Offset.Value.ToString(System.Globalization.CultureInfo.InvariantCulture) });
            }
            if (parameters.RoundDecimal is not null)
            {
                grpcRequest.RankParams.Add(new Grpc.KeyValuePair { Key = "round_decimal", Value = parameters.RoundDecimal.Value.ToString(System.Globalization.CultureInfo.InvariantCulture) });
            }
            if (parameters.GroupByField is not null)
            {
                grpcRequest.RankParams.Add(new Grpc.KeyValuePair { Key = "group_by_field", Value = parameters.GroupByField });
            }
            if (parameters.GroupSize is not null)
            {
                grpcRequest.RankParams.Add(new Grpc.KeyValuePair { Key = "group_size", Value = parameters.GroupSize.Value.ToString(System.Globalization.CultureInfo.InvariantCulture) });
            }
            if (parameters.StrictGroupSize is not null)
            {
                grpcRequest.RankParams.Add(new Grpc.KeyValuePair { Key = "strict_group_size", Value = parameters.StrictGroupSize.Value.ToString() });
            }
            if (parameters.IgnoreGrowing is not null)
            {
                grpcRequest.RankParams.Add(new Grpc.KeyValuePair
                {
                    Key = "ignore_growing",
                    Value = parameters.IgnoreGrowing.Value ? "true" : "false"
                });
            }
            if (parameters.Timezone is not null)
            {
                grpcRequest.RankParams.Add(new Grpc.KeyValuePair { Key = "timezone", Value = parameters.Timezone });
            }
            if (parameters.GracefulTime is not null)
            {
                grpcRequest.RankParams.Add(new Grpc.KeyValuePair { Key = "graceful_time", Value = parameters.GracefulTime.Value.ToString(System.Globalization.CultureInfo.InvariantCulture) });
            }
            if (parameters.Rerank is not null)
            {
                grpcRequest.RankParams.Add(new Grpc.KeyValuePair { Key = "rerank", Value = parameters.Rerank });
            }
            foreach (KeyValuePair<string, string> parameter in parameters.ExtraParameters)
            {
                grpcRequest.RankParams.Add(new Grpc.KeyValuePair { Key = parameter.Key, Value = parameter.Value });
            }

            if (parameters.ConsistencyLevel is { } cl)
            {
                grpcRequest.ConsistencyLevel = (Grpc.ConsistencyLevel)(int)cl;
                grpcRequest.GuaranteeTimestamp = CalculateGuaranteeTimestamp(
                    _endpoint, CurrentDatabase, request.CollectionName, cl, parameters.GuaranteeTimestamp);
            }
            else
            {
                // Same Session-style guarantee as plain Search when consistency is unset (see BuildSearchRequest).
                grpcRequest.UseDefaultConsistency = true;
                grpcRequest.GuaranteeTimestamp = CalculateGuaranteeTimestamp(
                    _endpoint, CurrentDatabase, request.CollectionName, ConsistencyLevel.Session, null);
            }
        }
        else
        {
            grpcRequest.UseDefaultConsistency = true;
            grpcRequest.GuaranteeTimestamp = CalculateGuaranteeTimestamp(
                _endpoint, CurrentDatabase, request.CollectionName, ConsistencyLevel.Session, null);
        }

        Grpc.SearchResults response = await InvokeAsync(
                GrpcClient.HybridSearchAsync, grpcRequest, static r => r.Status, cancellationToken)
            .ConfigureAwait(false);

        return SearchResp.FromGrpc(response);
    }

    /// <summary>
    /// Queries rows from a collection by expression.
    /// </summary>
    /// <param name="request">The query request.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    public async Task<QueryResp> QueryAsync(
        QueryReq request,
        CancellationToken cancellationToken = default)
    {
        Verify.NotNull(request);

        await EnsureConnectedAsync(cancellationToken).ConfigureAwait(false);

        string? primaryKeyField = null;
        if (request.Parameters?.Ids is { Count: > 0 })
        {
            DescribeCollectionResp description = await DescribeCollectionAsync(
                new Requests.Collection.DescribeCollectionReq { CollectionName = request.CollectionName },
                cancellationToken).ConfigureAwait(false);

            primaryKeyField = description.Schema.Fields.SingleOrDefault(f => f.IsPrimaryKey)?.Name
                ?? throw new MilvusException(MilvusErrorCode.UnexpectedError,
                    $"Collection '{request.CollectionName}' has no primary key field.");
        }

        Grpc.QueryRequest grpcRequest = request.ToGrpcQueryRequest(primaryKeyField);
        grpcRequest.GuaranteeTimestamp = CalculateGuaranteeTimestamp(
            _endpoint, CurrentDatabase, request.CollectionName,
            request.Parameters?.ConsistencyLevel ?? ConsistencyLevel.Session,
            request.Parameters?.GuaranteeTimestamp);
        Grpc.QueryResults response = await InvokeAsync(
                GrpcClient.QueryAsync, grpcRequest, static r => r.Status, cancellationToken)
            .ConfigureAwait(false);

        return QueryResp.FromGrpc(response);
    }

    /// <summary>
    /// Fetches rows by primary key.
    /// </summary>
    /// <param name="request">The get request.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    public async Task<GetResp> GetAsync(
        GetReq request,
        CancellationToken cancellationToken = default)
    {
        Verify.NotNull(request);

        await EnsureConnectedAsync(cancellationToken).ConfigureAwait(false);

        DescribeCollectionResp description = await DescribeCollectionAsync(
            new Requests.Collection.DescribeCollectionReq { CollectionName = request.CollectionName },
            cancellationToken).ConfigureAwait(false);

        FieldSchema? primaryKey = description.Schema.Fields.SingleOrDefault(f => f.IsPrimaryKey)
            ?? throw new MilvusException(MilvusErrorCode.UnexpectedError,
                $"Collection '{request.CollectionName}' has no primary key field.");

        Grpc.QueryRequest grpcRequest = request.ToGrpcQueryRequest(primaryKey.Name);
        grpcRequest.GuaranteeTimestamp = CalculateGuaranteeTimestamp(
            _endpoint, CurrentDatabase, request.CollectionName,
            ConsistencyLevel.Session, null);
        Grpc.QueryResults response = await InvokeAsync(
                GrpcClient.QueryAsync, grpcRequest, static r => r.Status, cancellationToken)
            .ConfigureAwait(false);

        return GetResp.FromGrpc(response);
    }

    /// <summary>
    /// Creates a server-side iterator that pages over query results in batches.
    /// </summary>
    /// <remarks>
    /// The iterator lazily pages over the server in <see cref="QueryIteratorReq.BatchSize" />-sized batches
    /// (default 1000, range 1–16384). <see cref="QueryParameters.Offset" /> is not supported and throws.
    /// Consume with <c>await foreach</c>, optionally with <c>.WithCancellation(token)</c>.
    /// </remarks>
    /// <param name="request">The query iterator request.</param>
    public QueryIterator QueryIteratorAsync(QueryIteratorReq request)
    {
        Verify.NotNull(request);
        request.Validate();
        return new QueryIterator(this, request);
    }

    /// <summary>
    /// Creates a server-side iterator that pages over search results in batches.
    /// </summary>
    /// <remarks>
    /// The iterator uses the <c>search_iter_v2</c> token protocol and requires a Milvus server of version 2.5.2
    /// or later. <see cref="SearchParameters.Offset" /> is not supported and throws. Consume with
    /// <c>await foreach</c>, optionally with <c>.WithCancellation(token)</c>.
    /// </remarks>
    /// <param name="request">The search iterator request.</param>
    public SearchIterator SearchIteratorAsync(SearchIteratorReq request)
    {
        Verify.NotNull(request);
        request.Validate();
        return new SearchIterator(this, request);
    }

    internal async IAsyncEnumerable<IReadOnlyList<FieldData>> QueryIteratorCoreAsync(
        QueryIteratorReq request,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        await EnsureConnectedAsync(cancellationToken).ConfigureAwait(false);

        DescribeCollectionResp description = await DescribeCollectionAsync(
                new Requests.Collection.DescribeCollectionReq { CollectionName = request.CollectionName },
                cancellationToken)
            .ConfigureAwait(false);

        FieldSchema? pkField = description.Schema.Fields.FirstOrDefault(f => f.IsPrimaryKey);
        if (pkField is null)
        {
            throw new MilvusException(MilvusErrorCode.UnexpectedError,
                $"Collection '{request.CollectionName}' has no primary key field.");
        }

        bool userRequestedOutputFields = request.Parameters?.OutputFieldsInternal is { Count: > 0 };
        bool isUserRequestPkField = userRequestedOutputFields && request.Parameters!.OutputFieldsInternal!.Contains(pkField.Name);
        string? userExpression = request.Expression;
        int userLimit = request.Parameters?.Limit ?? int.MaxValue;

        // Establish the starting expression for the first page. With no user expression an empty filter
        // returns every row; the C++/Java iterators likewise start from the full range without a min bound.
        // (Emitting a numeric lower bound like "pk >= -9223372036854775808" requires the server's parser to
        // special-case the int64-min unary-minus boundary, which older 2.6.x servers did not -- see milvus#46075.)
        string expr = userExpression ?? "";

        var grpcRequest = new Grpc.QueryRequest
        {
            CollectionName = request.CollectionName,
            Expr = expr
        };

        QueryParameters? parameters = request.Parameters;
        if (parameters is not null)
        {
            if (parameters.PartitionNamesInternal?.Count > 0)
            {
                grpcRequest.PartitionNames.AddRange(parameters.PartitionNamesInternal);
            }
            if (parameters.OutputFieldsInternal?.Count > 0)
            {
                grpcRequest.OutputFields.AddRange(parameters.OutputFieldsInternal);
            }
            if (parameters.TimeTravelTimestamp is not null)
            {
                grpcRequest.TravelTimestamp = parameters.TimeTravelTimestamp.Value;
            }
            if (parameters.IgnoreGrowing is not null)
            {
                grpcRequest.QueryParams.Add(new Grpc.KeyValuePair
                {
                    Key = "ignore_growing",
                    Value = parameters.IgnoreGrowing.Value ? "true" : "false"
                });
            }
            if (parameters.Timezone is not null)
            {
                grpcRequest.QueryParams.Add(new Grpc.KeyValuePair { Key = "timezone", Value = parameters.Timezone });
            }
            foreach (KeyValuePair<string, object> template in parameters.FilterTemplates)
            {
                grpcRequest.ExprTemplateValues[template.Key] = ToTemplateValue(template.Value);
            }
            if (parameters.Ids is { Count: > 0 })
            {
                throw new ArgumentException(
                    "Ids is not supported with a query iterator.", nameof(request));
            }

            if (parameters.ConsistencyLevel is { } cl)
            {
                grpcRequest.ConsistencyLevel = (Grpc.ConsistencyLevel)(int)cl;
                grpcRequest.GuaranteeTimestamp = CalculateGuaranteeTimestamp(
                    _endpoint, CurrentDatabase, request.CollectionName, cl, parameters.GuaranteeTimestamp);
            }
            else
            {
                // Unset consistency falls back to the collection's configured level (server default).
                grpcRequest.UseDefaultConsistency = true;
            }
        }
        else
        {
            grpcRequest.UseDefaultConsistency = true;
        }

        // Establish a snapshot on the first page: a request-level guarantee_timestamp of 0 makes the proxy
        // take a snapshot at its request time and return it as SessionTs, which we pin for all later pages so
        // the pk-cursor paging runs on a fixed MVCC snapshot.
        grpcRequest.GuaranteeTimestamp = 0;

        // Request the primary key field in any case to drive the iteration. Only add it explicitly when the
        // user selected a non-empty output-field list that does not already contain the pk; when the list is
        // empty the server returns all fields (including the pk).
        if (userRequestedOutputFields && !isUserRequestPkField)
        {
            grpcRequest.OutputFields.Add(pkField.Name);
        }

        // Replace parameters required for the iterator.
        string iterationBatchSize = Math.Min(request.BatchSize, userLimit).ToString(CultureInfo.InvariantCulture);
        ReplaceKeyValueItems(grpcRequest.QueryParams,
            new Grpc.KeyValuePair { Key = "iterator", Value = "True" },
            new Grpc.KeyValuePair { Key = "reduce_stop_for_best", Value = request.ReduceStopForBest ? "True" : "False" },
            new Grpc.KeyValuePair { Key = "batch_size", Value = iterationBatchSize },
            new Grpc.KeyValuePair { Key = "offset", Value = "0" },
            new Grpc.KeyValuePair { Key = "limit", Value = iterationBatchSize });

        int processedItemsCount = 0;
        while (true)
        {
            Grpc.QueryResults response = await InvokeAsync(
                    GrpcClient.QueryAsync, grpcRequest, static r => r.Status, cancellationToken)
                .ConfigureAwait(false);

            if (response.SessionTs > 0)
            {
                grpcRequest.GuaranteeTimestamp = response.SessionTs;
            }
            else if (grpcRequest.GuaranteeTimestamp == 0)
            {
                // Server did not return a snapshot timestamp; pin a client-side timestamp instead so
                // subsequent pages still run on a fixed MVCC snapshot.
                grpcRequest.GuaranteeTimestamp = (ulong)((DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() + 1000) << 18);
            }

            Grpc.FieldData? pkFieldData = response.FieldsData.FirstOrDefault(f => f.FieldName == pkField.Name);
            if (pkFieldData is null)
            {
                throw new MilvusException(MilvusErrorCode.UnexpectedError,
                    $"Query iterator response did not contain primary key field '{pkField.Name}'.");
            }

            int pageRowCount = pkFieldData.Scalars.DataCase switch
            {
                Grpc.ScalarField.DataOneofCase.StringData => pkFieldData.Scalars.StringData.Data.Count,
                Grpc.ScalarField.DataOneofCase.IntData => pkFieldData.Scalars.IntData.Data.Count,
                Grpc.ScalarField.DataOneofCase.LongData => pkFieldData.Scalars.LongData.Data.Count,
                _ => 0
            };

            if (pageRowCount == 0)
            {
                yield break;
            }

            // With ReduceStopForBest enabled the server may return more rows than the requested limit on a
            // page. Cap the yielded page to the remaining limit and advance the pk cursor only to the last
            // *yielded* row, so the surplus rows are re-delivered on the next page instead of being dropped
            // (matching the C++ QueryIteratorImpl's copyResults trimming).
            int yieldCount = userLimit == int.MaxValue
                ? pageRowCount
                : Math.Min(pageRowCount, userLimit - processedItemsCount);
            if (yieldCount <= 0)
            {
                yield break;
            }

            // Remove the extra primary key field only when we added it ourselves (user requested a
            // non-empty output-field list that did not include the pk); otherwise return everything.
            IReadOnlyList<FieldData> fields = DqlConversions.ProcessReturnedFieldData(response.FieldsData);
            if (userRequestedOutputFields && !isUserRequestPkField)
            {
                fields = fields.Where(f => f.FieldName != pkField.Name).ToList();
            }

            if (yieldCount < pageRowCount)
            {
                fields = DqlConversions.TakeRows(fields, 0, yieldCount);
            }

            yield return fields;

            processedItemsCount += yieldCount;
            if (processedItemsCount >= userLimit)
            {
                yield break;
            }

            object pkLastValue = GetPkValue(pkFieldData, yieldCount - 1);

            ReplaceKeyValueItems(grpcRequest.QueryParams,
                new Grpc.KeyValuePair
                {
                    Key = "limit",
                    Value = Math.Min(request.BatchSize, userLimit - processedItemsCount).ToString(CultureInfo.InvariantCulture)
                });

            string nextExpression = pkField.DataType switch
            {
                DataType.VarChar => $"{pkField.Name} > '{EscapeStringLiteral(pkLastValue as string)}'",
                DataType.Int8 or DataType.Int16 or DataType.Int32 or DataType.Int64 => $"{pkField.Name} > {pkLastValue}",
                _ => throw new MilvusException(MilvusErrorCode.UnexpectedError,
                    $"Unsupported data type '{pkField.DataType}' for primary key field '{pkField.Name}'.")
            };

            if (!string.IsNullOrWhiteSpace(userExpression))
            {
                nextExpression += $" and ({userExpression})";
            }

            grpcRequest.Expr = nextExpression;
        }
    }

    private static object GetPkValue(Grpc.FieldData pkFieldData, int index)
        => pkFieldData.Scalars.DataCase switch
        {
            Grpc.ScalarField.DataOneofCase.StringData => pkFieldData.Scalars.StringData.Data[index],
            Grpc.ScalarField.DataOneofCase.IntData => pkFieldData.Scalars.IntData.Data[index],
            Grpc.ScalarField.DataOneofCase.LongData => pkFieldData.Scalars.LongData.Data[index],
            _ => throw new MilvusException(MilvusErrorCode.UnexpectedError,
                $"Unsupported primary key scalar case '{pkFieldData.Scalars.DataCase}'.")
        };

    internal async IAsyncEnumerable<SingleResult> SearchIteratorCoreAsync(
        SearchIteratorReq request,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        await EnsureConnectedAsync(cancellationToken).ConfigureAwait(false);

        DescribeCollectionResp description = await DescribeCollectionAsync(
                new Requests.Collection.DescribeCollectionReq { CollectionName = request.CollectionName },
                cancellationToken)
            .ConfigureAwait(false);

        long collectionId = description.CollectionId;

        int remaining = request.Limit is > 0 and not int.MaxValue ? request.Limit : int.MaxValue;

        var subRequest = new SearchReq
        {
            CollectionName = request.CollectionName,
            VectorFieldName = request.VectorFieldName,
            Vectors = request.Vectors,
            SparseVectors = request.SparseVectors,
            HalfVectors = request.HalfVectors,
            MetricType = request.MetricType,
            Limit = request.BatchSize,
            Parameters = request.Parameters
        };

        Grpc.SearchRequest grpcRequest = BuildSearchRequest(subRequest);
        grpcRequest.SearchParams.Add(new Grpc.KeyValuePair { Key = "collection_id", Value = collectionId.ToString(CultureInfo.InvariantCulture) });
        grpcRequest.SearchParams.Add(new Grpc.KeyValuePair { Key = "iterator", Value = "True" });
        grpcRequest.SearchParams.Add(new Grpc.KeyValuePair { Key = "search_iter_v2", Value = "True" });
        // Establish a snapshot on the first probe: a request-level guarantee_timestamp of 0 makes the proxy
        // take a snapshot and return it as SessionTs, which is pinned for all subsequent pages.
        grpcRequest.GuaranteeTimestamp = 0;

        string? token = null;
        float? lastBound = null;

        while (remaining > 0)
        {
            int batchSize = Math.Min(request.BatchSize, remaining);
            ReplaceKeyValueItems(grpcRequest.SearchParams,
                new Grpc.KeyValuePair { Key = "topk", Value = batchSize.ToString(CultureInfo.InvariantCulture) },
                new Grpc.KeyValuePair { Key = "search_iter_batch_size", Value = batchSize.ToString(CultureInfo.InvariantCulture) });

            if (token is not null)
            {
                ReplaceKeyValueItems(grpcRequest.SearchParams,
                    new Grpc.KeyValuePair { Key = "search_iter_id", Value = token });
            }

            if (lastBound is not null)
            {
                ReplaceKeyValueItems(grpcRequest.SearchParams,
                    new Grpc.KeyValuePair { Key = "search_iter_last_bound", Value = FormatIteratorBound(lastBound.Value) });
            }

            Grpc.SearchResults response = await InvokeAsync(
                    GrpcClient.SearchAsync, grpcRequest, static r => r.Status, cancellationToken)
                .ConfigureAwait(false);

            if (response.SessionTs > 0)
            {
                grpcRequest.GuaranteeTimestamp = response.SessionTs;
            }
            else if (grpcRequest.GuaranteeTimestamp == 0)
            {
                // Server did not return a snapshot timestamp; pin a client-side timestamp instead.
                grpcRequest.GuaranteeTimestamp = (ulong)((DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() + 1000) << 18);
            }

            Grpc.SearchIteratorV2Results? iteratorInfo = response.Results?.SearchIteratorV2Results;
            if (iteratorInfo is null || string.IsNullOrEmpty(iteratorInfo.Token))
            {
                throw new MilvusException(MilvusErrorCode.UnexpectedError,
                    "The server does not support the Search Iterator V2 protocol; a Milvus server of version 2.5.2 or later is required.");
            }

            token ??= iteratorInfo.Token;
            lastBound = iteratorInfo.LastBound;

            int count = response.Results?.Topks.Count > 0 ? (int)response.Results.Topks[0] : 0;
            if (count == 0)
            {
                yield break;
            }

            // The search iterator accepts a single query vector, so each page produces exactly one
            // SingleResult carrying the page's top-K scores, primary keys and output fields.
            SearchResp page = SearchResp.FromGrpc(response);
            if (page.SingleResults is { Count: > 0 } singleResults)
            {
                yield return singleResults[0];
            }
            else
            {
                // Fall back to the flat columns when the response carries no per-query slicing info.
                IReadOnlyList<FieldData> fields = DqlConversions.ProcessReturnedFieldData(response.Results!.FieldsData);
                string pkName = string.IsNullOrEmpty(response.Results!.PrimaryFieldName) ? "id" : response.Results!.PrimaryFieldName;
                IReadOnlyList<long>? longIds = response.Results!.Ids?.IntId?.Data;
                IReadOnlyList<string>? stringIds = response.Results!.Ids?.StrId?.Data;
                yield return new SingleResult(
                    pkName,
                    MilvusIds.FromSlices(longIds, stringIds),
                    response.Results!.Scores,
                    fields);
            }

            remaining -= count;
        }
    }

    private static string FormatIteratorBound(float bound)
        => ((double)bound).ToString("0.000000000000000", CultureInfo.InvariantCulture);

    private static Grpc.IDs ToGrpcIds(IReadOnlyList<object> ids)
    {
        bool allLong = ids.All(i => i is long or int);
        var result = new Grpc.IDs();
        if (allLong)
        {
            var longData = new Grpc.LongArray();
            longData.Data.AddRange(ids.Select(i => Convert.ToInt64(i, CultureInfo.InvariantCulture)));
            result.IntId = longData;
        }
        else
        {
            // Reject a heterogeneous list up front with a clear error instead of throwing
            // InvalidCastException on the first numeric id (matching EnsureSameElementType used for
            // template arrays).
            if (ids.Any(i => i is not string))
            {
                throw new ArgumentException(
                    "IDs must be all integers or all strings; a mix of numeric and string primary keys is not supported.");
            }

            var stringData = new Grpc.StringArray();
            stringData.Data.AddRange(ids.Select(i => (string)i));
            result.StrId = stringData;
        }

        return result;
    }

    // Builds a boolean expression of the form "<primaryKeyField> in [<id>, ...]" from a list of primary key
    // values. String ids are quoted and escaped so they cannot break or inject into the expression.
    internal static string BuildPrimaryKeyExpression(string primaryKeyField, IReadOnlyList<object> ids)
        => $"{primaryKeyField} in [{string.Join(", ", ids.Select(FormatId))}]";

    private static string FormatId(object id)
        => id is string s
            ? $"\"{EscapeString(s)}\""
            : Convert.ToString(id, CultureInfo.InvariantCulture)!;

    // Escape backslashes, double quotes and line breaks so string primary keys containing them (or other
    // special characters) still produce a valid boolean expression and cannot be used for expression
    // injection. The Milvus expression grammar rejects raw CR/LF inside a quoted literal.
    private static string EscapeString(string value)
        => value
            .Replace("\\", "\\\\")
            .Replace("\"", "\\\"")
            .Replace("\r", "\\r")
            .Replace("\n", "\\n");

    internal static Grpc.TemplateValue ToTemplateValue(object value)
        => value switch
        {
            bool b => new Grpc.TemplateValue { BoolVal = b },
            int i => new Grpc.TemplateValue { Int64Val = i },
            long l => new Grpc.TemplateValue { Int64Val = l },
            float f => new Grpc.TemplateValue { FloatVal = f },
            double d => new Grpc.TemplateValue { FloatVal = d },
            string s => new Grpc.TemplateValue { StringVal = s },
            _ when value is System.Collections.IEnumerable list => ToTemplateArrayValue(list),
            _ => throw new ArgumentException(
                $"Unsupported template value type '{value.GetType()}'; expected bool, int, long, float, double, string or an enumerable of those.", nameof(value))
        };

    private static Grpc.TemplateValue ToTemplateArrayValue(System.Collections.IEnumerable values)
    {
        var items = values.Cast<object>().ToList();
        var templateArray = new Grpc.TemplateArrayValue();
        if (items.Count == 0)
        {
            templateArray.LongData = new Grpc.LongArray();
            return new Grpc.TemplateValue { ArrayVal = templateArray };
        }

        switch (items[0])
        {
            case bool:
                EnsureSameElementType(items, typeof(bool));
                templateArray.BoolData = new Grpc.BoolArray();
                templateArray.BoolData.Data.AddRange(items.Cast<bool>());
                break;
            case int:
            case long:
            case short:
            case byte:
            case uint:
                EnsureSameElementType(items, typeof(long));
                templateArray.LongData = new Grpc.LongArray();
                templateArray.LongData.Data.AddRange(items.Select(x => Convert.ToInt64(x, System.Globalization.CultureInfo.InvariantCulture)));
                break;
            case float:
            case double:
                EnsureSameElementType(items, typeof(double));
                templateArray.DoubleData = new Grpc.DoubleArray();
                templateArray.DoubleData.Data.AddRange(items.Select(x => Convert.ToDouble(x, System.Globalization.CultureInfo.InvariantCulture)));
                break;
            case string:
                EnsureSameElementType(items, typeof(string));
                templateArray.StringData = new Grpc.StringArray();
                templateArray.StringData.Data.AddRange(items.Cast<string>());
                break;
            default:
                throw new ArgumentException(
                    $"Unsupported template array element type '{items[0].GetType()}'.", nameof(values));
        }

        return new Grpc.TemplateValue { ArrayVal = templateArray };
    }

    // Rejects mixed-type template arrays with a clear message (matching the C++ SDK's INVALID_ARGUMENT on
    // heterogeneous lists) instead of letting Convert.ToInt64/ToDouble throw a raw FormatException.
    private static void EnsureSameElementType(List<object> items, Type expectedType)
    {
        for (int i = 1; i < items.Count; i++)
        {
            object item = items[i]!;
            Type actual = item.GetType();
            bool compatible = expectedType == typeof(long)
                ? actual == typeof(int) || actual == typeof(long) || actual == typeof(short) || actual == typeof(byte) || actual == typeof(uint)
                : expectedType == typeof(double)
                    ? actual == typeof(float) || actual == typeof(double)
                    : actual == expectedType;

            if (!compatible)
            {
                throw new ArgumentException(
                    $"Filter expression template array mixes types: element {i} is '{actual}' but the first element is '{items[0].GetType()}'.");
            }
        }
    }

    private static void ReplaceKeyValueItems(
        RepeatedField<Grpc.KeyValuePair> collection, params Grpc.KeyValuePair[] pairs)
    {
        string[] obsoleteParameterKeys = pairs.Select(x => x.Key).Distinct().ToArray();
        Grpc.KeyValuePair[] obsoleteParameters = collection.Where(x => obsoleteParameterKeys.Contains(x.Key)).ToArray();
        foreach (Grpc.KeyValuePair field in obsoleteParameters)
        {
            collection.Remove(field);
        }

        foreach (Grpc.KeyValuePair pair in pairs)
        {
            collection.Add(pair);
        }
    }

    private Grpc.SearchRequest BuildSearchRequest(SearchReq request)
    {
        Verify.NotNullOrWhiteSpace(request.VectorFieldName);

        // Exactly one of dense / sparse / half vectors must be provided, unless searching by IDs.
        int vectorInputs = (request.Vectors.Count > 0 ? 1 : 0)
                           + (request.SparseVectors is { Count: > 0 } ? 1 : 0)
                           + (request.HalfVectors is { Count: > 0 } ? 1 : 0);
        bool searchByIds = request.Ids is { Count: > 0 };
        if (searchByIds ? vectorInputs != 0 : vectorInputs != 1)
        {
            throw new ArgumentException(
                "Exactly one of Vectors, SparseVectors or HalfVectors must be provided (or Ids for a primary-key search).");
        }

        Verify.GreaterThan(request.Limit, 0);

        var grpcRequest = new Grpc.SearchRequest
        {
            CollectionName = request.CollectionName,
            DslType = Grpc.DslType.BoolExprV1
        };

        SearchParameters? parameters = request.Parameters;
        if (parameters is not null)
        {
            if (parameters.PartitionNamesInternal?.Count > 0)
            {
                grpcRequest.PartitionNames.AddRange(parameters.PartitionNamesInternal);
            }
            if (parameters.OutputFieldsInternal?.Count > 0)
            {
                grpcRequest.OutputFields.AddRange(parameters.OutputFieldsInternal);
            }
            if (parameters.Expression is not null)
            {
                grpcRequest.Dsl = parameters.Expression;
            }
            if (parameters.TimeTravelTimestamp is not null)
            {
                grpcRequest.TravelTimestamp = parameters.TimeTravelTimestamp.Value;
            }
            if (parameters.Offset is not null)
            {
                grpcRequest.SearchParams.Add(new Grpc.KeyValuePair { Key = "offset", Value = parameters.Offset.Value.ToString(System.Globalization.CultureInfo.InvariantCulture) });
            }
            if (parameters.RoundDecimal is not null)
            {
                grpcRequest.SearchParams.Add(new Grpc.KeyValuePair { Key = "round_decimal", Value = parameters.RoundDecimal.Value.ToString(System.Globalization.CultureInfo.InvariantCulture) });
            }
            if (parameters.GroupByField is not null)
            {
                grpcRequest.SearchParams.Add(new Grpc.KeyValuePair { Key = Constants.GroupByField, Value = parameters.GroupByField });
            }
            if (parameters.GroupSize is not null)
            {
                grpcRequest.SearchParams.Add(new Grpc.KeyValuePair { Key = Constants.GroupSize, Value = parameters.GroupSize.Value.ToString(System.Globalization.CultureInfo.InvariantCulture) });
            }
            if (parameters.StrictGroupSize is not null)
            {
                grpcRequest.SearchParams.Add(new Grpc.KeyValuePair { Key = Constants.StrictGroupSize, Value = parameters.StrictGroupSize.Value.ToString() });
            }
            if (parameters.IgnoreGrowing is not null)
            {
                grpcRequest.SearchParams.Add(new Grpc.KeyValuePair
                {
                    Key = Constants.IgnoreGrowing,
                    Value = parameters.IgnoreGrowing.Value ? "true" : "false"
                });
            }
            if (parameters.GracefulTime is not null)
            {
                grpcRequest.SearchParams.Add(new Grpc.KeyValuePair { Key = Constants.GracefulTime, Value = parameters.GracefulTime.Value.ToString(System.Globalization.CultureInfo.InvariantCulture) });
            }
            if (parameters.Timezone is not null)
            {
                grpcRequest.SearchParams.Add(new Grpc.KeyValuePair { Key = "timezone", Value = parameters.Timezone });
            }
            if (parameters.Radius is not null)
            {
                grpcRequest.SearchParams.Add(new Grpc.KeyValuePair { Key = "radius", Value = parameters.Radius });
            }
            if (parameters.RangeFilter is not null)
            {
                grpcRequest.SearchParams.Add(new Grpc.KeyValuePair { Key = "range_filter", Value = parameters.RangeFilter });
            }
            if (parameters.Rerank is not null)
            {
                grpcRequest.SearchParams.Add(new Grpc.KeyValuePair { Key = "rerank", Value = parameters.Rerank });
            }
            foreach (KeyValuePair<string, object> template in parameters.FilterTemplates)
            {
                grpcRequest.ExprTemplateValues[template.Key] = ToTemplateValue(template.Value);
            }
            if (parameters.Highlighter.Count > 0 || parameters.HighlightType is not null)
            {
                grpcRequest.Highlighter = new Grpc.Highlighter();
                foreach (KeyValuePair<string, string> highlighter in parameters.Highlighter)
                {
                    grpcRequest.Highlighter.Params.Add(new Grpc.KeyValuePair { Key = highlighter.Key, Value = highlighter.Value });
                }
                if (parameters.HighlightType is { } searchHighlightType)
                {
                    grpcRequest.Highlighter.Type = (Grpc.HighlightType)(int)searchHighlightType;
                }
            }
            foreach (KeyValuePair<string, string> parameter in parameters.ExtraParameters)
            {
                grpcRequest.SearchParams.Add(new Grpc.KeyValuePair { Key = parameter.Key, Value = parameter.Value });
            }

            if (parameters.ConsistencyLevel is { } cl)
            {
                grpcRequest.ConsistencyLevel = (Grpc.ConsistencyLevel)(int)cl;
                grpcRequest.GuaranteeTimestamp = CalculateGuaranteeTimestamp(
                    _endpoint, CurrentDatabase, request.CollectionName, cl, parameters.GuaranteeTimestamp);
            }
            else
            {
                // Unset consistency falls back to the collection's configured level (server default), but the
                // request still carries a Session-style guarantee timestamp so an insert-then-search honors the
                // read-your-writes guarantee, matching the C++ SDK (DeduceGuaranteeTimestamp on NONE) and the
                // design doc §4.6 ("reads CollectionTsCache for Session consistency").
                grpcRequest.UseDefaultConsistency = true;
                grpcRequest.GuaranteeTimestamp = CalculateGuaranteeTimestamp(
                    _endpoint, CurrentDatabase, request.CollectionName, ConsistencyLevel.Session, null);
            }
        }
        else
        {
            grpcRequest.UseDefaultConsistency = true;
            grpcRequest.GuaranteeTimestamp = CalculateGuaranteeTimestamp(
                _endpoint, CurrentDatabase, request.CollectionName, ConsistencyLevel.Session, null);
        }

        if (searchByIds)
        {
            grpcRequest.Ids = ToGrpcIds(request.Ids!);
        }
        else
        {
            grpcRequest.PlaceholderGroup = new Grpc.PlaceholderGroup { Placeholders = { request.ToPlaceholderValue() } }.ToByteString();
        }

        grpcRequest.SearchParams.AddRange(
            new[]
            {
                new Grpc.KeyValuePair { Key = "anns_field", Value = request.VectorFieldName },
                new Grpc.KeyValuePair { Key = "topk", Value = request.Limit.ToString(System.Globalization.CultureInfo.InvariantCulture) },
                new Grpc.KeyValuePair
                {
                    Key = "params",
                    Value = Combine(parameters)
                }
            });

        // metric_type defaults to SimilarityMetricType.Invalid (enum 0), which maps to the literal string
        // "INVALID"; the proxy fails metric resolution on that value instead of inferring it from the index.
        // Match C++/Java and omit the metric_type search param when it is unset.
        if (request.MetricType != SimilarityMetricType.Invalid)
        {
            grpcRequest.SearchParams.Add(new Grpc.KeyValuePair { Key = "metric_type", Value = request.MetricType.ToWireString() });
        }

        return grpcRequest;
    }

    private static string Combine(SearchParameters? parameters)
    {
        var json = new Dictionary<string, object?>();
        if (parameters is not null)
        {
            foreach (KeyValuePair<string, string> parameter in parameters.ExtraParameters)
            {
                json[parameter.Key] = parameter.Value;
            }

            // The Milvus proxy detects a range search by parsing the "params" JSON string (search_util.go
            // parseSearchInfo), not the top-level search_params keys, so radius/range_filter must be embedded
            // in the JSON -- as numbers, since the server rejects string-typed radius/range_filter values
            // (matching the C++ SDK which serializes them with std::stod).
            if (parameters.Radius is not null)
            {
                json["radius"] = ParseRangeNumber(parameters.Radius, nameof(SearchParameters.Radius));
            }

            if (parameters.RangeFilter is not null)
            {
                json["range_filter"] = ParseRangeNumber(parameters.RangeFilter, nameof(SearchParameters.RangeFilter));
            }
        }

        // Serialize the extra parameters as a proper JSON object so string values are quoted and
        // special characters are escaped, matching how the Java SDK sends the same bag.
        return System.Text.Json.JsonSerializer.Serialize(json);
    }

    private static double ParseRangeNumber(string value, string parameterName)
    {
        if (!double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out double parsed))
        {
            throw new ArgumentException(
                $"'{value}' is not a valid number for {parameterName}.", parameterName);
        }

        return parsed;
    }

    // Escapes backslashes, single quotes and line breaks inside a string literal embedded in a boolean
    // expression, so VarChar primary-key values used in the iterator cursor (e.g. {pk} > 'value') cannot
    // break or inject into the expression. The Milvus expression grammar rejects raw CR/LF inside a quoted
    // literal, so they must be escaped here too, consistent with EscapeString.
    private static string EscapeStringLiteral(string? value)
        => (value ?? "")
            .Replace("\\", "\\\\")
            .Replace("'", "\\'")
            .Replace("\r", "\\r")
            .Replace("\n", "\\n");

    private static Grpc.SearchRequest BuildHybridSearchSubRequest(SearchReq subRequest)
    {
        Verify.NotNullOrWhiteSpace(subRequest.VectorFieldName);

        int vectorInputs = (subRequest.Vectors.Count > 0 ? 1 : 0)
                           + (subRequest.SparseVectors is { Count: > 0 } ? 1 : 0)
                           + (subRequest.HalfVectors is { Count: > 0 } ? 1 : 0);
        if (vectorInputs != 1)
        {
            throw new ArgumentException(
                "Exactly one of Vectors, SparseVectors or HalfVectors must be provided for each sub-request.");
        }

        Verify.GreaterThan(subRequest.Limit, 0);

        var request = new Grpc.SearchRequest
        {
            CollectionName = subRequest.CollectionName,
            DslType = Grpc.DslType.BoolExprV1
        };

        SearchParameters? parameters = subRequest.Parameters;
        if (parameters is not null)
        {
            if (parameters.Expression is not null)
            {
                request.Dsl = parameters.Expression;
            }
            if (parameters.Offset is not null)
            {
                request.SearchParams.Add(new Grpc.KeyValuePair { Key = "offset", Value = parameters.Offset.Value.ToString(System.Globalization.CultureInfo.InvariantCulture) });
            }
            if (parameters.RoundDecimal is not null)
            {
                request.SearchParams.Add(new Grpc.KeyValuePair { Key = "round_decimal", Value = parameters.RoundDecimal.Value.ToString(System.Globalization.CultureInfo.InvariantCulture) });
            }
            if (parameters.Radius is not null)
            {
                request.SearchParams.Add(new Grpc.KeyValuePair { Key = "radius", Value = parameters.Radius });
            }
            if (parameters.RangeFilter is not null)
            {
                request.SearchParams.Add(new Grpc.KeyValuePair { Key = "range_filter", Value = parameters.RangeFilter });
            }
            if (parameters.GroupByField is not null)
            {
                request.SearchParams.Add(new Grpc.KeyValuePair { Key = Constants.GroupByField, Value = parameters.GroupByField });
            }
            if (parameters.GroupSize is not null)
            {
                request.SearchParams.Add(new Grpc.KeyValuePair { Key = Constants.GroupSize, Value = parameters.GroupSize.Value.ToString(System.Globalization.CultureInfo.InvariantCulture) });
            }
            if (parameters.StrictGroupSize is not null)
            {
                request.SearchParams.Add(new Grpc.KeyValuePair { Key = Constants.StrictGroupSize, Value = parameters.StrictGroupSize.Value.ToString() });
            }
            if (parameters.IgnoreGrowing is not null)
            {
                request.SearchParams.Add(new Grpc.KeyValuePair
                {
                    Key = Constants.IgnoreGrowing,
                    Value = parameters.IgnoreGrowing.Value ? "true" : "false"
                });
            }
            if (parameters.Timezone is not null)
            {
                request.SearchParams.Add(new Grpc.KeyValuePair { Key = "timezone", Value = parameters.Timezone });
            }
            if (parameters.GracefulTime is not null)
            {
                request.SearchParams.Add(new Grpc.KeyValuePair { Key = Constants.GracefulTime, Value = parameters.GracefulTime.Value.ToString(System.Globalization.CultureInfo.InvariantCulture) });
            }
            if (parameters.Rerank is not null)
            {
                request.SearchParams.Add(new Grpc.KeyValuePair { Key = "rerank", Value = parameters.Rerank });
            }
            foreach (KeyValuePair<string, string> parameter in parameters.ExtraParameters)
            {
                request.SearchParams.Add(new Grpc.KeyValuePair { Key = parameter.Key, Value = parameter.Value });
            }
            foreach (KeyValuePair<string, object> template in parameters.FilterTemplates)
            {
                request.ExprTemplateValues[template.Key] = ToTemplateValue(template.Value);
            }
            if (parameters.Highlighter.Count > 0 || parameters.HighlightType is not null)
            {
                request.Highlighter = new Grpc.Highlighter();
                foreach (KeyValuePair<string, string> highlighter in parameters.Highlighter)
                {
                    request.Highlighter.Params.Add(new Grpc.KeyValuePair { Key = highlighter.Key, Value = highlighter.Value });
                }
                if (parameters.HighlightType is { } subHighlightType)
                {
                    request.Highlighter.Type = (Grpc.HighlightType)(int)subHighlightType;
                }
            }
        }

        request.PlaceholderGroup = new Grpc.PlaceholderGroup { Placeholders = { subRequest.ToPlaceholderValue() } }.ToByteString();
        request.SearchParams.AddRange(
            new[]
            {
                new Grpc.KeyValuePair { Key = "anns_field", Value = subRequest.VectorFieldName },
                new Grpc.KeyValuePair { Key = "topk", Value = subRequest.Limit.ToString(System.Globalization.CultureInfo.InvariantCulture) },
                new Grpc.KeyValuePair { Key = "params", Value = Combine(parameters) }
            });

        // Same as BuildSearchRequest: omit metric_type when it is unset (Invalid) so the proxy infers it.
        if (subRequest.MetricType != SimilarityMetricType.Invalid)
        {
            request.SearchParams.Add(new Grpc.KeyValuePair { Key = "metric_type", Value = subRequest.MetricType.ToWireString() });
        }

        return request;
    }

    private static IEnumerable<Grpc.KeyValuePair> ToGrpcKeyValuePairs(
        IReadOnlyList<KeyValuePair<string, string>> pairs)
    {
        foreach (KeyValuePair<string, string> pair in pairs)
        {
            yield return new Grpc.KeyValuePair { Key = pair.Key, Value = pair.Value };
        }
    }

    internal static ulong CalculateGuaranteeTimestamp(
        string endpoint, string database, string collectionName, ConsistencyLevel consistencyLevel, ulong? userProvidedGuaranteeTimestamp)
    {
        if (userProvidedGuaranteeTimestamp is not null && consistencyLevel != ConsistencyLevel.Customized)
        {
            throw new ArgumentException(
                $"A guarantee timestamp can only be specified with consistency level {ConsistencyLevel.Customized}");
        }

        return consistencyLevel switch
        {
            ConsistencyLevel.Strong => (ulong)Constants.GuaranteeStrongTs,
            ConsistencyLevel.Session
                => (ulong)CollectionTsCache.Instance.Get(endpoint, database, collectionName) is { } ts && ts != 0
                    ? (ulong)ts
                    : (ulong)Constants.GuaranteeEventuallyTs,
            ConsistencyLevel.BoundedStaleness => (ulong)2,
            ConsistencyLevel.Eventually => (ulong)Constants.GuaranteeEventuallyTs,
            ConsistencyLevel.Customized => userProvidedGuaranteeTimestamp
                ?? throw new ArgumentException(
                    $"A guarantee timestamp is required with consistency level {ConsistencyLevel.Customized}"),
            _ => throw new ArgumentOutOfRangeException(nameof(consistencyLevel), consistencyLevel, null)
        };
    }
}
