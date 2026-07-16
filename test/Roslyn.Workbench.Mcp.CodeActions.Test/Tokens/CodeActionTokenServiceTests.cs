namespace Roslyn.Workbench.Mcp.CodeActions.Test.Tokens;

public sealed class CodeActionTokenServiceTests
{
    private readonly CodeActionTokenService _target;

    public CodeActionTokenServiceTests()
    {
        _target = new CodeActionTokenService();
    }

    [Fact]
    public void GIVEN_EncodedPayload_WHEN_DecodingToken_THEN_ShouldPreserveEveryValue()
    {
        var payload = CreatePayload();
        var token = _target.Encode(payload);

        var result = _target.TryDecode(token, out var decodedPayload);

        result.Should().BeTrue();
        decodedPayload.Should().BeEquivalentTo(payload);
    }

    [Fact]
    public void GIVEN_TokenFromAnotherServiceInstance_WHEN_Decoding_THEN_ShouldRejectToken()
    {
        var otherService = new CodeActionTokenService();
        var token = otherService.Encode(CreatePayload());

        var result = _target.TryDecode(token, out var payload);

        result.Should().BeFalse();
        payload.Should().BeEquivalentTo(new CodeActionTokenPayload());
    }

    [Theory]
    [InlineData("")]
    [InlineData("Payload")]
    [InlineData(".Signature")]
    [InlineData("Payload.")]
    public void GIVEN_MissingTokenPart_WHEN_Decoding_THEN_ShouldRejectToken(string token)
    {
        var result = _target.TryDecode(token, out var payload);

        result.Should().BeFalse();
        payload.Should().BeEquivalentTo(new CodeActionTokenPayload());
    }

    [Theory]
    [InlineData("invalid!.signature")]
    [InlineData("payload.invalid!")]
    [InlineData("payload.signature.extra")]
    public void GIVEN_MalformedTokenEncoding_WHEN_Decoding_THEN_ShouldRejectToken(string token)
    {
        var result = _target.TryDecode(token, out var payload);

        result.Should().BeFalse();
        payload.Should().BeEquivalentTo(new CodeActionTokenPayload());
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void GIVEN_TamperedTokenPart_WHEN_Decoding_THEN_ShouldRejectToken(bool tamperPayload)
    {
        var token = _target.Encode(CreatePayload());
        var parts = token.Split('.');
        var partIndex = tamperPayload ? 0 : 1;
        parts[partIndex] = ReplaceFirstCharacter(parts[partIndex]);

        var result = _target.TryDecode(string.Join(".", parts), out var payload);

        result.Should().BeFalse();
        payload.Should().BeEquivalentTo(new CodeActionTokenPayload());
    }

    [Fact]
    public void GIVEN_EncodedPayload_WHEN_InspectingTokenAlphabet_THEN_ShouldUseUnpaddedBase64Url()
    {
        var token = _target.Encode(CreatePayload());

        var parts = token.Split('.');

        parts.Should().HaveCount(2);
        parts.Should().OnlyContain(part => part.Length > 0);
        parts.SelectMany(static part => part).Should().OnlyContain(value => IsBase64UrlCharacter(value));
        token.Should().NotContainAny("+", "/", "=");
    }

    private static CodeActionTokenPayload CreatePayload()
    {
        return new CodeActionTokenPayload
        {
            Kind = "Kind",
            ProviderId = "ProviderId",
            Title = "Title",
            EquivalenceKey = "EquivalenceKey",
            ActionPath = [1, 2],
            DiagnosticIds = ["DiagnosticId", "OtherDiagnosticId"],
            WorkspaceId = "WorkspaceId",
            WorkspaceEpoch = 3,
            TransactionRevision = 4,
            ExpiresAt = "2000-01-01T00:00:00.0000000Z",
            DocumentPath = "DocumentPath",
            Start = 5,
            Length = 6,
        };
    }

    private static string ReplaceFirstCharacter(string value)
    {
        var replacement = value[0] == 'A' ? 'B' : 'A';
        return replacement + value[1..];
    }

    private static bool IsBase64UrlCharacter(char value)
    {
        return char.IsAsciiLetterOrDigit(value) || value is '-' or '_';
    }
}
