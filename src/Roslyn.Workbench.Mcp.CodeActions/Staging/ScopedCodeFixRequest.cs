using Roslyn.Workbench.Mcp.Contracts.Selectors;

namespace Roslyn.Workbench.Mcp.CodeActions.Staging;

/// <summary>
/// Describes a host-owned scoped code-fix selection and staging request.
/// </summary>
public sealed record ScopedCodeFixRequest
{
    /// <summary>
    /// Gets the scope to apply the code fix to.
    /// </summary>
    public ScopeSelector? Scope { get; init; }

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
    /// Gets the optional synthetic diagnostic ID to use when no matching diagnostics are produced for a document.
    /// </summary>
    public string? SyntheticDiagnosticId { get; init; }

    /// <summary>
    /// Gets the optional cap on changed source documents.
    /// </summary>
    public int? MaxChanges { get; init; }
}
