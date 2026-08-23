using System.Collections.Immutable;

namespace Roslyn.Workbench.Mcp.CodeActions.Discovery;

internal sealed record RegisteredCodeFix
{
    public required CodeAction Action { get; init; }

    public required ImmutableArray<Diagnostic> Diagnostics { get; init; }

    public required TextSpan TargetSpan { get; init; }

    public required int RootIndex { get; init; }
}
