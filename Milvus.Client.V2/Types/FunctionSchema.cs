using Milvus.Client.V2.Utils;

namespace Milvus.Client.V2.Types;

/// <summary>
/// Describes a function in a collection schema, such as a BM25 full-text-search or text-embedding function.
/// </summary>
public sealed class FunctionSchema
{
    /// <summary>
    /// Initializes a new instance of the <see cref="FunctionSchema"/> class.
    /// </summary>
    /// <param name="name">The name of the function.</param>
    /// <param name="type">The type of the function.</param>
    /// <param name="inputFieldNames">The names of the input fields.</param>
    /// <param name="outputFieldNames">The names of the output fields.</param>
    /// <param name="description">An optional description of the function.</param>
    /// <param name="id">An optional ID for the function.</param>
    public FunctionSchema(string name, FunctionType type, IEnumerable<string> inputFieldNames, IEnumerable<string> outputFieldNames, string description = "", long id = 0)
    {
        Verify.NotNullOrWhiteSpace(name);
        Verify.NotNull(inputFieldNames);
        Verify.NotNull(outputFieldNames);
        Name = name;
        Id = id;
        Type = type;
        _inputFieldNames.AddRange(inputFieldNames);
        _outputFieldNames.AddRange(outputFieldNames);
        Description = description;
    }

    /// <summary>
    /// Creates a BM25 full-text-search function.
    /// </summary>
    /// <param name="name">The name of the function.</param>
    /// <param name="inputFieldName">The name of the input field.</param>
    /// <param name="outputFieldName">The name of the output field.</param>
    /// <param name="description">An optional description of the function.</param>
    /// <returns>The newly created <see cref="FunctionSchema"/>.</returns>
    public static FunctionSchema CreateBm25(string name, string inputFieldName, string outputFieldName, string description = "")
        => new(name, FunctionType.Bm25, new[] { inputFieldName }, new[] { outputFieldName }, description);

    private readonly List<string> _inputFieldNames = new();
    private readonly List<string> _outputFieldNames = new();

    /// <summary>
    /// The ID of the function.
    /// </summary>
    public long Id { get; }

    /// <summary>
    /// The name of the function.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// The type of the function.
    /// </summary>
    public FunctionType Type { get; }

    /// <summary>
    /// A description of the function.
    /// </summary>
    public string Description { get; }

    /// <summary>
    /// The names of the input fields.
    /// </summary>
    public IList<string> InputFieldNames => _inputFieldNames;

    /// <summary>
    /// The names of the output fields.
    /// </summary>
    public IList<string> OutputFieldNames => _outputFieldNames;

    internal static FunctionSchema FromGrpc(Grpc.FunctionSchema grpc)
        => new(
            grpc.Name,
            (FunctionType)grpc.Type,
            grpc.InputFieldNames,
            grpc.OutputFieldNames,
            grpc.Description,
            grpc.Id);

    internal Grpc.FunctionSchema ToGrpcFunctionSchema()
    {
        var result = new Grpc.FunctionSchema
        {
            Name = Name,
            Id = Id,
            Type = (Grpc.FunctionType)(int)Type,
            Description = Description
        };
        result.InputFieldNames.AddRange(_inputFieldNames);
        result.OutputFieldNames.AddRange(_outputFieldNames);
        return result;
    }
}
