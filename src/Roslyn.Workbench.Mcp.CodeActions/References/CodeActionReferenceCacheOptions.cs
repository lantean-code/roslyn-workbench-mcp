namespace Roslyn.Workbench.Mcp.CodeActions.References;

/// <summary>
/// Configures storage for temporary Code Action references.
/// </summary>
internal sealed class CodeActionReferenceCacheOptions
{
    /// <summary>
    /// Gets or sets the maximum total estimated size of cached references.
    /// </summary>
    public long SizeLimit { get; set; } = 75_000;
}
