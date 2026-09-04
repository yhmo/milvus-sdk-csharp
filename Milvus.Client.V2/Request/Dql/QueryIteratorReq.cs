using Milvus.Client.V2.Types;
using Milvus.Client.V2.Utils;

namespace Milvus.Client.V2.Requests.Dql;

/// <summary>
/// Represents a request to iterate over query results in batches using a server-side iterator.
/// </summary>
public sealed class QueryIteratorReq
{
    /// <summary>
    /// The name of the collection to query.
    /// </summary>
    public string CollectionName { get; set; } = "";

    /// <summary>
    /// The boolean expression identifying the rows to return. When omitted, the iterator returns all rows
    /// ordered by primary key.
    /// </summary>
    public string? Expression { get; set; }

    /// <summary>
    /// The optional query parameters.
    /// </summary>
    public QueryParameters? Parameters { get; set; }

    /// <summary>
    /// The number of rows to fetch per batch. Defaults to 1000.
    /// </summary>
    public int BatchSize { get; set; } = 1000;

    /// <summary>
    /// When true (default), the server stops reducing results once the top-K best results are found, which can
    /// improve performance at the cost of exactness. When false, the server performs a full reduction.
    /// </summary>
    public bool ReduceStopForBest { get; set; } = true;

    internal void Validate()
    {
        Verify.NotNullOrWhiteSpace(CollectionName);

        if (BatchSize < 1 || BatchSize > 16384)
        {
            throw new ArgumentOutOfRangeException(nameof(BatchSize), BatchSize, "Batch size must be between 1 and 16384");
        }

        if (Parameters?.Offset is not null and not 0)
        {
            throw new ArgumentException("Offset is not supported with a query iterator.", nameof(Parameters));
        }

        if (Parameters?.Limit is < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(Parameters), Parameters.Limit,
                "Limit must be at least 1.");
        }
    }
}
