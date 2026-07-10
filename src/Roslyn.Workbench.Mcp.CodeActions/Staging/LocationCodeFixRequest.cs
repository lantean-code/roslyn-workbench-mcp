using Roslyn.Workbench.Mcp.Workspace.Contracts.Selectors;

namespace Roslyn.Workbench.Mcp.CodeActions.Staging;

/// <summary>
/// Describes a host-owned location-scoped code-fix selection and staging request.
/// </summary>
public sealed record LocationCodeFixRequest
{
    /// <summary>
    /// Gets the selected source location to stage the code fix for.
    /// </summary>
    public LocationSelector? Location { get; init; }

    /// <summary>
    /// Gets the expected workspace snapshot for the operation.
    /// </summary>
    public SnapshotPrecondition? ExpectedSnapshot { get; init; }

    /// <summary>
    /// Gets the diagnostic IDs that must be supported by the selected code fix.
    /// </summary>
    public IReadOnlyList<string> DiagnosticIds { get; init; } = [];

    /// <summary>
    /// Gets the optional provider identity to require.
    /// </summary>
    public string? ProviderId { get; init; }

    /// <summary>
    /// Gets the optional action title to require.
    /// </summary>
    public string? Title { get; init; }

    /// <summary>
    /// Gets the optional equivalence key to require.
    /// </summary>
    public string? EquivalenceKey { get; init; }

    /// <summary>
    /// Gets the optional reflected analyzer type name to run when project analyzers do not produce matching diagnostics.
    /// </summary>
    public string? AnalyzerTypeName { get; init; }

    /// <summary>
    /// Gets the optional synthetic diagnostic ID to use when no matching diagnostics are produced at the selected location.
    /// </summary>
    public string? SyntheticDiagnosticId { get; init; }
}
