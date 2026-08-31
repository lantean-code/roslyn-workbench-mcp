using System.Reflection;

namespace Roslyn.Workbench.Mcp.CodeActions.Composition;

/// <summary>
/// Selects the assemblies included in Code Action MEF composition.
/// </summary>
internal sealed class CodeActionCompositionOptions
{
    /// <summary>
    /// Gets or sets a value indicating whether Roslyn's built-in feature assemblies are included.
    /// </summary>
    public bool IncludeBuiltInAssemblies { get; set; } = true;

    /// <summary>
    /// Gets or sets additional assemblies containing Code Action providers.
    /// </summary>
    public IReadOnlyList<Assembly> AdditionalAssemblies { get; set; } = [];
}
