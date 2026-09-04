using Milvus.Client.V2.Responses.Dql;
using Milvus.Client.V2.Types;

namespace Milvus.Examples;

/// <summary>
/// Shared helpers for the examples.
/// </summary>
internal static class ExampleHelpers
{
    /// <summary>
    /// Creates a <see cref="Milvus.Client.V2.MilvusClientV2" /> from a "host:port" URI, using the token
    /// (username:password) from <paramref name="defaultToken" /> when <c>MILVUS_TOKEN</c> is not set.
    /// </summary>
    public static Milvus.Client.V2.MilvusClientV2 CreateClient(string uri, string? defaultToken = null)
    {
        string? token = Environment.GetEnvironmentVariable("MILVUS_TOKEN") ?? defaultToken;

        var config = new Milvus.Client.V2.Types.ConnectConfig { Uri = uri };

        if (token is not null)
        {
            int colon = token.IndexOf(':');
            if (colon > 0)
            {
                config.Username = token[..colon];
                config.Password = token[(colon + 1)..];
            }
            else
            {
                config.ApiKey = token;
            }
        }

        var client = new Milvus.Client.V2.MilvusClientV2(config);
        return client;
    }

    /// <summary>
    /// Drops a collection if it exists, so examples are idempotent.
    /// </summary>
    public static async Task ResetCollectionAsync(
        Milvus.Client.V2.MilvusClientV2 client, string collectionName, CancellationToken ct = default)
    {
        if ((await client.HasCollectionAsync(new Milvus.Client.V2.Requests.Collection.HasCollectionReq
        {
            CollectionName = collectionName
        }, ct)).Has)
        {
            await client.DropCollectionAsync(new Milvus.Client.V2.Requests.Collection.DropCollectionReq
            {
                CollectionName = collectionName
            }, ct);
        }
    }

    /// <summary>
    /// Prints each row of a query result, one <c>field=value</c> pair per output field.
    /// </summary>
    public static void PrintQueryRows(QueryResp results)
    {
        for (int i = 0; i < results.RowCount; i++)
        {
            IReadOnlyDictionary<string, object?> row = results.GetRow(i);
            Console.WriteLine($"  {string.Join(", ", row.Select(pair => $"{pair.Key}={FormatValue(pair.Value)}"))}");
        }
    }

    /// <summary>
    /// Prints each hit of a search result, with its id, score and output fields.
    /// </summary>
    public static void PrintSearchHits(SearchResp results)
    {
        SingleResult? single = results.SingleResults?.FirstOrDefault();
        int hitCount = results.Ids.LongIds?.Count ?? 0;
        if (single is null)
        {
            return;
        }

        for (int i = 0; i < hitCount; i++)
        {
            IReadOnlyDictionary<string, object?> row = single.GetRow(i);
            Console.WriteLine($"  {string.Join(", ", row.Select(pair => $"{pair.Key}={FormatValue(pair.Value)}"))}");
        }
    }

    private static string FormatValue(object? value)
    {
        if (value is null)
        {
            return "null";
        }

        Type type = value.GetType();

        // ReadOnlyMemory<T> (vector rows) is not IEnumerable; materialize it via ToArray().
        if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(ReadOnlyMemory<>))
        {
            object? array = type.GetMethod(nameof(ReadOnlyMemory<byte>.ToArray))?.Invoke(value, null);
            return FormatValue(array);
        }

        if (value is SparseVector<float> sparse)
        {
            var parts = new List<string>();
            for (int i = 0; i < sparse.Indices.Length; i++)
            {
                parts.Add($"{sparse.Indices.Span[i]}: {sparse.Values.Span[i]}");
            }

            return $"{{{string.Join(", ", parts)}}}";
        }

        if (value is System.Collections.IEnumerable enumerable and not string)
        {
            return $"[{string.Join(", ", enumerable.Cast<object?>().Select(FormatValue))}]";
        }

        return value.ToString() ?? "null";
    }
}
