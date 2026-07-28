namespace Roslyn.Workbench.Mcp.CodeActions.References;

internal sealed record CodeActionDiagnosticIdentity
{
    public string Id { get; init; } = string.Empty;

    public string Message { get; init; } = string.Empty;

    public int Start { get; init; }

    public int Length { get; init; }
}
