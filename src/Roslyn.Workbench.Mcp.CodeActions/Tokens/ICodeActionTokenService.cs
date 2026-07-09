namespace Roslyn.Workbench.Mcp.CodeActions.Tokens;

internal interface ICodeActionTokenService
{
    string Encode(CodeActionTokenPayload payload);

    bool TryDecode(string token, out CodeActionTokenPayload payload);
}
