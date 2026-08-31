using System.Collections.Immutable;

namespace Roslyn.Workbench.Mcp.CodeActions.Discovery;

/// <summary>
/// Pairs a Code Fix provider with the diagnostic identifiers it can fix.
/// </summary>
internal sealed record CodeFixProviderMetadata
{
    /// <summary>
    /// Gets the activated Code Fix provider.
    /// </summary>
    public required CodeFixProvider Provider { get; init; }

    /// <summary>
    /// Gets the diagnostic identifiers supported by the provider.
    /// </summary>
    public required ImmutableArray<string> FixableDiagnosticIds { get; init; }
}
