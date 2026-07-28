namespace Roslyn.Workbench.Mcp.CodeActions.Discovery;

internal sealed record CodeActionAnalyzerIndexWarning
{
    public required string AnalyzerTypeName { get; init; }

    public required CodeActionAnalyzerActivationStatus Status { get; init; }
}
