using Milvus.Client.V2.Utils;

namespace Milvus.Client.V2.Types;

/// <summary>
/// Highlight fragments for one field of a full-text/hybrid search result, mapped from the proto
/// <c>common.HighlightResult</c>. One entry exists per query (in the same order as the query vectors).
/// </summary>
public sealed class MilvusHighlightResult
{
    internal MilvusHighlightResult(string fieldName, IReadOnlyList<MilvusHighlightData> datas)
    {
        FieldName = fieldName;
        Datas = datas;
    }

    /// <summary>
    /// The name of the highlighted field.
    /// </summary>
    public string FieldName { get; }

    /// <summary>
    /// The per-query highlight data, in the same order as the search queries.
    /// </summary>
    public IReadOnlyList<MilvusHighlightData> Datas { get; }

    internal static MilvusHighlightResult FromGrpc(Grpc.HighlightResult grpc)
        => new(
            grpc.FieldName,
            grpc.Datas.Select(d => new MilvusHighlightData(
                d.Fragments.ToList(),
                d.Scores.ToList())).ToList());
}

/// <summary>
/// The highlight fragments and scores for a single query of a highlighted field.
/// </summary>
public sealed class MilvusHighlightData
{
    internal MilvusHighlightData(IReadOnlyList<string> fragments, IReadOnlyList<float> scores)
    {
        Fragments = fragments;
        Scores = scores;
    }

    /// <summary>
    /// The matched fragments.
    /// </summary>
    public IReadOnlyList<string> Fragments { get; }

    /// <summary>
    /// The per-fragment relevance scores.
    /// </summary>
    public IReadOnlyList<float> Scores { get; }
}
