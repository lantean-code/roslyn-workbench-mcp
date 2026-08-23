using System.Collections.Immutable;

namespace Roslyn.Workbench.Mcp.CodeActions.Discovery;

internal sealed record CodeFixProviderMetadata
{
    public required CodeFixProvider Provider { get; init; }

    public required ImmutableArray<string> FixableDiagnosticIds { get; init; }
}
