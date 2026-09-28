using System.Text.Json.Nodes;

namespace Roslyn.Workbench.Mcp.ToolReferenceGenerator;

/// <summary>
/// Describes one deterministic security-relevant product surface item.
/// </summary>
internal sealed class SecuritySurfaceItem
{
    /// <summary>
    /// Gets the stable category-qualified surface key.
    /// </summary>
    public required string Key { get; init; }

    /// <summary>
    /// Gets the surface category.
    /// </summary>
    public required string Category { get; init; }

    /// <summary>
    /// Gets the machine-derived facts for this surface item.
    /// </summary>
    public required JsonObject Facts { get; init; }
}

/// <summary>
/// Represents the authored security-invariant manifest.
/// </summary>
internal sealed class SecurityInvariantManifest
{
    /// <summary>
    /// Gets the manifest format identifier.
    /// </summary>
    public required string Format { get; init; }

    /// <summary>
    /// Gets the authored entries.
    /// </summary>
    public required IReadOnlyList<SecurityInvariantEntry> Entries { get; init; }
}

/// <summary>
/// Describes one enforced invariant, delegated responsibility or limitation.
/// </summary>
internal sealed class SecurityInvariantEntry
{
    /// <summary>
    /// Gets the stable entry identifier.
    /// </summary>
    public required string Id { get; init; }

    /// <summary>
    /// Gets the entry title.
    /// </summary>
    public required string Title { get; init; }

    /// <summary>
    /// Gets the entry classification.
    /// </summary>
    public required string Classification { get; init; }

    /// <summary>
    /// Gets the actor responsible for the described outcome.
    /// </summary>
    public required string Actor { get; init; }

    /// <summary>
    /// Gets the protected outcome or delegated responsibility.
    /// </summary>
    public required string Outcome { get; init; }

    /// <summary>
    /// Gets the boundary at which enforcement or observation begins and ends.
    /// </summary>
    public required string Boundary { get; init; }

    /// <summary>
    /// Gets the residual risk and explicit non-guarantees.
    /// </summary>
    public required string ResidualRisk { get; init; }

    /// <summary>
    /// Gets the exact generated surface keys covered by this entry.
    /// </summary>
    public required IReadOnlyList<string> SurfaceKeys { get; init; }

    /// <summary>
    /// Gets executable evidence supporting an enforced invariant.
    /// </summary>
    public required IReadOnlyList<SecurityEvidenceReference> Evidence { get; init; }
}

/// <summary>
/// Identifies one executable test used as security evidence.
/// </summary>
internal sealed class SecurityEvidenceReference
{
    /// <summary>
    /// Gets the evidence level.
    /// </summary>
    public required string Level { get; init; }

    /// <summary>
    /// Gets the repository-relative test project path.
    /// </summary>
    public required string Project { get; init; }

    /// <summary>
    /// Gets the repository-relative source-file path.
    /// </summary>
    public required string Source { get; init; }

    /// <summary>
    /// Gets the fully qualified test method name.
    /// </summary>
    public required string Test { get; init; }
}
