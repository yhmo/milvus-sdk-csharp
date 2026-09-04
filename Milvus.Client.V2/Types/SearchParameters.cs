using Milvus.Client.V2.Utils;

namespace Milvus.Client.V2.Types;

/// <summary>
/// A set of optional parameters for performing a search.
/// </summary>
public sealed class SearchParameters
{
    internal List<string>? OutputFieldsInternal { get; private set; }
    internal List<string>? PartitionNamesInternal { get; private set; }

    /// <summary>
    /// Number of entities to skip during the search. If set, the sum of this parameter and of the search
    /// <c>limit</c> must be between 1 and 16384.
    /// </summary>
    public int? Offset { get; set; }

    /// <summary>
    /// The consistency level to be used in the search. Defaults to the consistency level configured for the
    /// collection.
    /// </summary>
    public ConsistencyLevel? ConsistencyLevel { get; set; }

    /// <summary>
    /// If set, guarantee that the search is performed after any updates up to the provided timestamp.
    /// </summary>
    public ulong? GuaranteeTimestamp { get; set; }

    /// <summary>
    /// The duration of graceful time (in milliseconds) that the search tolerates for eventual consistency.
    /// </summary>
    public ulong? GracefulTime { get; set; }

    /// <summary>
    /// Specifies an optional time travel timestamp; the search will get results based on the data at that point
    /// in time.
    /// </summary>
    public ulong? TimeTravelTimestamp { get; set; }

    /// <summary>
    /// A boolean expression to filter the search results.
    /// </summary>
    public string? Expression { get; set; }

    /// <summary>
    /// The number of decimal places to round the scores to.
    /// </summary>
    public long? RoundDecimal { get; set; }

    /// <summary>
    /// Additional search parameters passed through to the server.
    /// </summary>
    public IDictionary<string, string> ExtraParameters { get; } = new Dictionary<string, string>();

    /// <summary>
    /// Whether to ignore the growing segments during the search.
    /// </summary>
    public bool? IgnoreGrowing { get; private set; }

    /// <summary>
    /// The field to group the search results by.
    /// </summary>
    public string? GroupByField { get; set; }

    /// <summary>
    /// The maximum number of results per group when grouping is used.
    /// </summary>
    public int? GroupSize { get; set; }

    /// <summary>
    /// Whether the group size is strict.
    /// </summary>
    public bool? StrictGroupSize { get; set; }

    /// <summary>
    /// The names of fields to be returned from the search.
    /// </summary>
    public IList<string> OutputFields => OutputFieldsInternal ??= new();

    /// <summary>
    /// An optional list of partitions to be searched in the collection.
    /// </summary>
    public IList<string> PartitionNames => PartitionNamesInternal ??= new();

    /// <summary>
    /// Whether to ignore the growing segments during the search.
    /// </summary>
    public void SetIgnoreGrowing(bool value) => IgnoreGrowing = value;

    /// <summary>
    /// The timezone used for the search, e.g. <c>"+08:00"</c> or <c>"America/New_York"</c>.
    /// </summary>
    public string? Timezone { get; set; }

    /// <summary>
    /// The search radius for range searches on binary/float vectors.
    /// </summary>
    public string? Radius { get; set; }

    /// <summary>
    /// The range filter for range searches on binary/float vectors.
    /// </summary>
    public string? RangeFilter { get; set; }

    /// <summary>
    /// The reranker to apply to the search results (e.g. <c>"rrf"</c>).
    /// </summary>
    public string? Rerank { get; set; }

    /// <summary>
    /// Named expression-template values used by the <see cref="Expression" /> filter, for parameterized filters.
    /// </summary>
    public IDictionary<string, object> FilterTemplates { get; } = new Dictionary<string, object>();

    /// <summary>
    /// The highlighter settings (key-value pairs) used to highlight matched terms in the results.
    /// </summary>
    public IDictionary<string, string> Highlighter { get; } = new Dictionary<string, string>();

    /// <summary>
    /// The type of highlighter to apply (lexical or semantic). When set, it is sent as the highlighter's
    /// <c>type</c>, matching the Java SDK's <c>Highlighter.highlightType()</c>. When <c>null</c>, the server
    /// applies its default (lexical).
    /// </summary>
    public HighlightType? HighlightType { get; set; }
}

/// <summary>
/// The type of highlighter used to produce matched-term highlights in search results.
/// </summary>
public enum HighlightType
{
    /// <summary>
    /// Lexical (BM25/keyword-based) highlighting. The server default.
    /// </summary>
    Lexical = 0,

    /// <summary>
    /// Semantic (embedding-based) highlighting.
    /// </summary>
    Semantic = 1
}
