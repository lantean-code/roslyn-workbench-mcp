namespace Roslyn.Workbench.Mcp.CodeActions.References;

internal sealed record CodeActionDiagnosticIdentity
{
    public required string Id { get; init; }

    public required string Message { get; init; }

    public required int Start { get; init; }

    public required int Length { get; init; }
}
