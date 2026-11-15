using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using System.Threading;

namespace EniadCsharpDotnet.Enterprise;

/// <summary>
/// Protobuf wire-format serializer and gRPC bidirectional streaming service (#112).
/// Implements standard Proto3 varint + zigzag encoding matching <c>Protos/comparison.proto</c>.
/// </summary>
public sealed class ComparisonGrpcStreamingService
{
    private readonly IComparisonDomainService _domainService;

    /// <summary>
    /// Initializes gRPC streaming endpoint with injected domain service.
    /// </summary>
    /// <param name="domainService">Comparison domain service.</param>
    public ComparisonGrpcStreamingService(IComparisonDomainService domainService)
    {
        ArgumentNullException.ThrowIfNull(domainService);
        _domainService = domainService;
    }

    /// <summary>
    /// Bidirectional gRPC stream transforming an incoming <see cref="IAsyncEnumerable{T}"/>
    /// of comparison requests into streamed results (#112).
    /// </summary>
    /// <param name="requests">Client request stream.</param>
    /// <param name="cancellationToken">Stream cancellation token.</param>
    /// <returns>Server response stream.</returns>
    public async IAsyncEnumerable<ComparisonResultDto> CompareStreamAsync(
        IAsyncEnumerable<ComparisonRequestDto> requests,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(requests);
        await foreach (ComparisonRequestDto req in requests.WithCancellation(cancellationToken).ConfigureAwait(false))
        {
            yield return await _domainService.CompareAsync(req, cancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Encodes a <see cref="ComparisonRequestDto"/> in standard Proto3 binary wire format
    /// matching <c>CompareRequestMessage</c> (<c>Protos/comparison.proto</c>).
    /// </summary>
    /// <param name="request">Comparison request message.</param>
    /// <returns>Proto3 wire bytes.</returns>
    public static byte[] SerializeRequestProto3(ComparisonRequestDto request)
    {
        ArgumentNullException.ThrowIfNull(request);
        using MemoryStream ms = new();
        // Field 1 (int32 first_value): wire type 0 (varint)
        WriteVarint(ms, (1UL << 3) | 0);
        WriteVarint(ms, unchecked((ulong)(long)request.FirstValue));
        // Field 2 (int32 second_value): wire type 0 (varint)
        WriteVarint(ms, (2UL << 3) | 0);
        WriteVarint(ms, unchecked((ulong)(long)request.SecondValue));
        // Field 3 (string correlation_id): wire type 2 (length-delimited)
        if (!string.IsNullOrEmpty(request.CorrelationId))
        {
            byte[] strBytes = System.Text.Encoding.UTF8.GetBytes(request.CorrelationId);
            WriteVarint(ms, (3UL << 3) | 2);
            WriteVarint(ms, (ulong)strBytes.Length);
            ms.Write(strBytes, 0, strBytes.Length);
        }

        return ms.ToArray();
    }

    /// <summary>
    /// Decodes a Proto3 wire buffer back into a <see cref="ComparisonRequestDto"/>.
    /// </summary>
    /// <param name="wireBytes">Serialized Proto3 payload.</param>
    /// <returns>Decoded request DTO.</returns>
    public static ComparisonRequestDto DeserializeRequestProto3(ReadOnlySpan<byte> wireBytes)
    {
        int offset = 0;
        int first = 0;
        int second = 0;
        string? corrId = null;

        while (offset < wireBytes.Length)
        {
            ulong tag = ReadVarint(wireBytes, ref offset);
            uint fieldNumber = (uint)(tag >> 3);
            uint wireType = (uint)(tag & 0x07);

            switch (fieldNumber, wireType)
            {
                case (1, 0):
                    first = unchecked((int)ReadVarint(wireBytes, ref offset));
                    break;
                case (2, 0):
                    second = unchecked((int)ReadVarint(wireBytes, ref offset));
                    break;
                case (3, 2):
                    int len = (int)ReadVarint(wireBytes, ref offset);
                    corrId = System.Text.Encoding.UTF8.GetString(wireBytes.Slice(offset, len));
                    offset += len;
                    break;
                default:
                    throw new InvalidDataException($"Unsupported protobuf field {fieldNumber} wireType {wireType}.");
            }
        }

        return new ComparisonRequestDto(first, second, corrId);
    }

    private static void WriteVarint(Stream stream, ulong value)
    {
        while (value >= 0x80)
        {
            stream.WriteByte((byte)((value & 0x7F) | 0x80));
            value >>= 7;
        }

        stream.WriteByte((byte)value);
    }

    private static ulong ReadVarint(ReadOnlySpan<byte> data, ref int offset)
    {
        ulong result = 0;
        int shift = 0;
        while (offset < data.Length)
        {
            byte b = data[offset++];
            result |= (ulong)(b & 0x7F) << shift;
            if ((b & 0x80) == 0)
            {
                return result;
            }

            shift += 7;
        }

        throw new InvalidDataException("Truncated varint in protobuf stream.");
    }
}
