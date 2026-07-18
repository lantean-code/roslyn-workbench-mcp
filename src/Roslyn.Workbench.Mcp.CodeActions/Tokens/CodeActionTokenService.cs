using System.Security.Cryptography;
using System.Text.Json;

namespace Roslyn.Workbench.Mcp.CodeActions.Tokens;

internal sealed class CodeActionTokenService : ICodeActionTokenService
{
    private static readonly JsonSerializerOptions _serializerOptions = new(JsonSerializerDefaults.Web);

    private readonly byte[] _secret;

    public CodeActionTokenService()
    {
        _secret = RandomNumberGenerator.GetBytes(32);
    }

    public string Encode(CodeActionTokenPayload payload)
    {
        var payloadBytes = JsonSerializer.SerializeToUtf8Bytes(payload, _serializerOptions);
        var signatureBytes = HMACSHA256.HashData(_secret, payloadBytes);
        return $"{Base64UrlEncode(payloadBytes)}.{Base64UrlEncode(signatureBytes)}";
    }

    public bool TryDecode(string token, out CodeActionTokenPayload payload)
    {
        payload = new CodeActionTokenPayload();
        var separatorIndex = token.IndexOf('.', StringComparison.Ordinal);
        if (separatorIndex <= 0 || separatorIndex == token.Length - 1)
        {
            return false;
        }

        try
        {
            var payloadBytes = Base64UrlDecode(token[..separatorIndex]);
            var signatureBytes = Base64UrlDecode(token[(separatorIndex + 1)..]);
            var expectedSignature = HMACSHA256.HashData(_secret, payloadBytes);
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

    private static string Base64UrlEncode(byte[] data)
    {
        return Convert.ToBase64String(data).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    }

    private static byte[] Base64UrlDecode(string value)
    {
        var padded = value.Replace('-', '+').Replace('_', '/');
        var remainder = padded.Length % 4;
        if (remainder > 0)
        {
            padded = padded.PadRight(padded.Length + (4 - remainder), '=');
        }

        return Convert.FromBase64String(padded);
    }
}
