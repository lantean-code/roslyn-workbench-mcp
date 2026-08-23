namespace Roslyn.Workbench.Mcp.CodeActions.Discovery;

internal sealed record CodeActionProviderFailure
{
    public required string ProviderId { get; init; }

    public required string Operation { get; init; }

    public required string ExceptionType { get; init; }
}
