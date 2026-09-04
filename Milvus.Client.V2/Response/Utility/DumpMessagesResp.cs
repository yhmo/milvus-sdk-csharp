using System.Runtime.CompilerServices;

using Grpc.Core;

using Milvus.Client.V2.Utils;

namespace Milvus.Client.V2.Responses.Utility;

/// <summary>
/// The result of a dump-messages operation, consumed as an <see cref="IAsyncEnumerable{T}" /> of
/// <see cref="DumpMessageInfo" />.
/// </summary>
public sealed class DumpMessagesResp : IAsyncEnumerable<DumpMessageInfo>
{
    private readonly Func<CancellationToken, IAsyncEnumerable<DumpMessageInfo>> _messages;

    internal DumpMessagesResp(Func<CancellationToken, IAsyncEnumerable<DumpMessageInfo>> messages)
    {
        _messages = messages;
    }

    /// <inheritdoc />
    public IAsyncEnumerator<DumpMessageInfo> GetAsyncEnumerator(
        CancellationToken cancellationToken = default)
        => _messages(cancellationToken).GetAsyncEnumerator(cancellationToken);
}

/// <summary>
/// Executes the server-streaming dump-messages RPC, converting the raw responses into
/// <see cref="DumpMessageInfo" /> values.
/// </summary>
internal static class DumpMessagesReader
{
    public static async IAsyncEnumerable<DumpMessageInfo> ReadAsync(
        MilvusClientV2 client,
        Grpc.DumpMessagesRequest request,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        await client.EnsureConnectedAsync(cancellationToken).ConfigureAwait(false);

        using AsyncServerStreamingCall<Grpc.DumpMessagesResponse> call =
            client.GrpcClient.DumpMessages(request, client.CallOptionsForStreaming(cancellationToken));

        var enumerator = call.ResponseStream.ReadAllAsync(cancellationToken).GetAsyncEnumerator(cancellationToken);
        while (true)
        {
            bool hasNext;
            try
            {
                hasNext = await enumerator.MoveNextAsync().ConfigureAwait(false);
            }
            catch (RpcException ex)
            {
                // A mid-stream transport failure surfaces as a raw RpcException; wrap it into MilvusException
                // so await-foreach consumers get the same uniform error surface as every other facade method.
                throw new MilvusException(
                    MilvusErrorCode.UnexpectedError, $"RPC failed: {ex.StatusCode} {ex.Status.Detail}",
                    ex.StatusCode, ex);
            }

            if (!hasNext)
            {
                yield break;
            }

            Grpc.DumpMessagesResponse response = enumerator.Current;
            if (response.ResponseCase == Grpc.DumpMessagesResponse.ResponseOneofCase.Status)
            {
                var code = (MilvusErrorCode)response.Status.Code;
                if (code != MilvusErrorCode.Success)
                {
                    throw new MilvusException(code, response.Status.Reason);
                }

                continue;
            }

            if (response.Message is not null)
            {
                yield return DumpMessageInfo.FromGrpc(response.Message);
            }
        }
    }
}
