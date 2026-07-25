namespace Roslyn.Workbench.Mcp.CodeActions.Tokens;

internal interface ICodeActionTokenService
{
    bool TryEncode(CodeActionTokenPayload payload, out string token);

    bool TryDecode(string token, out CodeActionTokenPayload payload);
}
