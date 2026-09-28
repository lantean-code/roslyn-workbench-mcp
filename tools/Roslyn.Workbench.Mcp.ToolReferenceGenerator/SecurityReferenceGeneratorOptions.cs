namespace Roslyn.Workbench.Mcp.ToolReferenceGenerator;

/// <summary>
/// Identifies the inputs and output used for security-reference generation.
/// </summary>
internal sealed class SecurityReferenceGeneratorOptions
{
    /// <summary>
    /// Gets the generated publication directory.
    /// </summary>
    public required string OutputDirectory { get; init; }

    /// <summary>
    /// Gets the checked-in code-derived surface baseline.
    /// </summary>
    public required string BaselineFile { get; init; }

    /// <summary>
    /// Gets the authored invariant manifest.
    /// </summary>
    public required string ManifestFile { get; init; }

    /// <summary>
    /// Gets the repository root used to resolve evidence paths.
    /// </summary>
    public required string RepositoryRoot { get; init; }

    /// <summary>
    /// Gets the immutable Git revision used for source evidence links.
    /// </summary>
    public required string SourceRevision { get; init; }

    /// <summary>
    /// Gets whether the checked-in surface baseline should be explicitly refreshed.
    /// </summary>
    public bool UpdateBaseline { get; init; }
}
