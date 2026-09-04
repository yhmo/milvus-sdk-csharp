using Milvus.Client.V2.Utils;

namespace Milvus.Client.V2.Responses.Collection;

/// <summary>
/// Represents the result of a <c>GetCollectionStats</c> operation.
/// </summary>
public sealed class GetCollectionStatsResp
{
    private GetCollectionStatsResp(long rowCount, IReadOnlyDictionary<string, string> stats)
    {
        RowCount = rowCount;
        Stats = stats;
    }

    internal static GetCollectionStatsResp FromGrpc(Grpc.GetCollectionStatisticsResponse response)
    {
        long rowCount = 0;
        var stats = new Dictionary<string, string>();
        foreach (Grpc.KeyValuePair stat in response.Stats)
        {
            stats[stat.Key] = stat.Value;
            if (stat.Key == "row_count"
                && !long.TryParse(stat.Value, System.Globalization.NumberStyles.Integer,
                    System.Globalization.CultureInfo.InvariantCulture, out rowCount))
            {
                // A malformed row_count must not abort the whole response; leave RowCount at 0 and surface
                // the raw stat through Stats so the caller can diagnose it.
                rowCount = 0;
            }
        }

        return new GetCollectionStatsResp(rowCount, stats);
    }

    /// <summary>
    /// The number of rows in the collection.
    /// </summary>
    public long RowCount { get; }

    /// <summary>
    /// The complete collection statistics as reported by the server, mirroring the Java SDK's <c>stats</c> map.
    /// </summary>
    public IReadOnlyDictionary<string, string> Stats { get; }
}
