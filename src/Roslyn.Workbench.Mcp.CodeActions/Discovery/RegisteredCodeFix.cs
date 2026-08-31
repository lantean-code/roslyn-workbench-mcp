using System.Collections.Immutable;

namespace Roslyn.Workbench.Mcp.CodeActions.Discovery;

/// <summary>
/// Captures one root Code Fix registered by a provider callback.
/// </summary>
internal sealed record RegisteredCodeFix
{
    /// <summary>
    /// Gets the registered root action.
    /// </summary>
    public required CodeAction Action { get; init; }

    /// <summary>
    /// Gets the diagnostics associated with the registration.
    /// </summary>
    public required ImmutableArray<Diagnostic> Diagnostics { get; init; }

    /// <summary>
    /// Gets the source span covered by the associated diagnostics.
    /// </summary>
    public required TextSpan TargetSpan { get; init; }

    /// <summary>
    /// Gets the action's registration order within the provider callback.
    /// </summary>
    public required int RootIndex { get; init; }
}
