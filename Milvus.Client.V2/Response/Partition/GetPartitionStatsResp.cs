namespace Milvus.Client.V2.Responses.Partition;

/// <summary>
/// The result of a <c>GetPartitionStats</c> operation.
/// </summary>
public sealed class GetPartitionStatsResp
{
    private GetPartitionStatsResp(long rowCount, IReadOnlyDictionary<string, string> stats)
    {
        RowCount = rowCount;
        Stats = stats;
    }

    internal static GetPartitionStatsResp FromGrpc(Grpc.GetPartitionStatisticsResponse response)
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

        return new GetPartitionStatsResp(rowCount, stats);
    }

    /// <summary>
    /// The number of rows in the partition.
    /// </summary>
    public long RowCount { get; }

    /// <summary>
    /// The complete partition statistics as reported by the server, mirroring the Java SDK's <c>stats</c> map.
    /// </summary>
    public IReadOnlyDictionary<string, string> Stats { get; }
}
