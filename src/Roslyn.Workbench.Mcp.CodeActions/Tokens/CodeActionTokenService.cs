using System.Buffers.Text;
using System.Security.Cryptography;
using System.Text.Json;

namespace Roslyn.Workbench.Mcp.CodeActions.Tokens;

internal sealed class CodeActionTokenService : ICodeActionTokenService
{
    internal const int MaximumTokenLength = 256 * 1024;

    private const int _signatureLength = 32;
    private const char _tokenSeparator = '.';

    private static readonly JsonSerializerOptions _serializerOptions = new(JsonSerializerDefaults.Web);

    private readonly byte[] _secret;

    public CodeActionTokenService()
    {
        _secret = RandomNumberGenerator.GetBytes(32);
    }

    public bool TryEncode(CodeActionTokenPayload payload, out string token)
    {
        var payloadBytes = JsonSerializer.SerializeToUtf8Bytes(payload, _serializerOptions);
        var payloadEncodedLength = Base64Url.GetEncodedLength(payloadBytes.Length);
        var signatureEncodedLength = Base64Url.GetEncodedLength(_signatureLength);
        var tokenLength = payloadEncodedLength + 1 + signatureEncodedLength;
        if (tokenLength > MaximumTokenLength)
        {
            token = string.Empty;
            return false;
        }

        var signatureBytes = HMACSHA256.HashData(_secret, payloadBytes);
        token = string.Create(
            tokenLength,
            (payloadBytes, signatureBytes, payloadEncodedLength),
            static (destination, state) =>
            {
                var (payload, signature, separatorIndex) = state;
                Base64Url.EncodeToChars(payload, destination[..separatorIndex]);
                destination[separatorIndex] = _tokenSeparator;
                Base64Url.EncodeToChars(signature, destination[(separatorIndex + 1)..]);
            });

        return true;
    }

    public bool TryDecode(string token, out CodeActionTokenPayload payload)
    {
        payload = new CodeActionTokenPayload();
        if (token.Length > MaximumTokenLength)
        {
            return false;
        }

        var tokenSpan = token.AsSpan();
        var separatorIndex = tokenSpan.IndexOf(_tokenSeparator);
        if (separatorIndex <= 0 || separatorIndex == token.Length - 1)
        {
            return false;
        }

        var encodedPayload = tokenSpan[..separatorIndex];
        var encodedSignature = tokenSpan[(separatorIndex + 1)..];
        if (!Base64Url.IsValid(encodedPayload, out var payloadLength)
            || !Base64Url.IsValid(encodedSignature, out var signatureLength)
            || signatureLength != _signatureLength)
        {
            return false;
        }

        try
        {
            var payloadBytes = new byte[payloadLength];
            Base64Url.DecodeFromChars(encodedPayload, payloadBytes);

            Span<byte> signatureBytes = stackalloc byte[_signatureLength];
            Base64Url.DecodeFromChars(encodedSignature, signatureBytes);

            Span<byte> expectedSignature = stackalloc byte[_signatureLength];
            HMACSHA256.HashData(_secret, payloadBytes, expectedSignature);
            if (!CryptographicOperations.FixedTimeEquals(signatureBytes, expectedSignature))
            {
                return false;
            }

            var parsed = JsonSerializer.Deserialize<CodeActionTokenPayload>(payloadBytes, _serializerOptions);
            if (parsed is null)
            {
                return false;
            }

            payload = parsed;
            return true;
        }
        catch (FormatException)
        {
            return false;
        }
        catch (JsonException)
        {
            return false;
        }
        catch (NotSupportedException)
        {
            return false;
        }
    }
}
