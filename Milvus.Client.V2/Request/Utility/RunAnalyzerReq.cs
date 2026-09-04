using System.Text.Json;
using Milvus.Client.V2.Utils;
namespace Milvus.Client.V2.Requests.Utility;

/// <summary>
/// Represents a request to run a text analyzer on the given strings, returning the analyzed tokens.
/// </summary>
public sealed class RunAnalyzerReq
{
    /// <summary>
    /// The analyzer configuration as a JSON object, e.g. <c>new Dictionary&lt;string, object&gt; { ["type"] = "english" }</c>.
    /// Serialized to JSON before sending.
    /// </summary>
    public IReadOnlyDictionary<string, object> AnalyzerParams { get; set; } =
        new Dictionary<string, object>();

    /// <summary>
    /// The texts to analyze.
    /// </summary>
    public IReadOnlyList<string> Texts { get; set; } = Array.Empty<string>();

    /// <summary>
    /// Whether to return the detailed token offsets and positions.
    /// </summary>
    public bool WithDetail { get; set; }

    /// <summary>
    /// Whether to return the token hashes.
    /// </summary>
    public bool WithHash { get; set; }

    /// <summary>
    /// The collection whose analyzer configuration is used. Required when <see cref="FieldName" /> is set.
    /// </summary>
    public string? CollectionName { get; set; }

    /// <summary>
    /// The field whose analyzer configuration is used.
    /// </summary>
    public string? FieldName { get; set; }

    /// <summary>
    /// The names of predefined analyzers to apply, when not using <see cref="AnalyzerParams" />.
    /// </summary>
    public IReadOnlyList<string>? AnalyzerNames { get; set; }
    internal Grpc.RunAnalyzerRequest ToGrpcRunAnalyzerRequest()
    {
        Verify.NotNull(AnalyzerParams);
        Verify.NotNullOrEmpty(Texts);
        var request = new Grpc.RunAnalyzerRequest
        {
            AnalyzerParams = JsonSerializer.Serialize(AnalyzerParams),
            WithDetail = WithDetail,
            WithHash = WithHash,
            CollectionName = CollectionName ?? "",
            FieldName = FieldName ?? ""
        };
        foreach (string text in Texts)
        {
            request.Placeholder.Add(ByteString.CopyFromUtf8(text));
        }
        if (AnalyzerNames is not null)
        {
            request.AnalyzerNames.AddRange(AnalyzerNames);
        }
        return request;
    }
}
