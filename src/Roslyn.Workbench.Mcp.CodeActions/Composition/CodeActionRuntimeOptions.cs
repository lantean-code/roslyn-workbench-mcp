using System.Reflection;

namespace Roslyn.Workbench.Mcp.CodeActions.Composition;

/// <summary>
/// Represents the runtime options for MEF-backed code actions.
/// </summary>
internal sealed record CodeActionRuntimeOptions
{
    /// <summary>
    /// Gets the token lifetime for listed actions.
    /// </summary>
    public TimeSpan TokenLifetime { get; init; } = TimeSpan.FromMinutes(5);

    /// <summary>
    /// Gets a value indicating whether the built-in Roslyn feature assemblies should be included.
    /// </summary>
    public bool IncludeBuiltInAssemblies { get; init; } = true;

    /// <summary>
    /// Gets any additional provider assemblies to include.
    /// </summary>
    public IReadOnlyList<Assembly> AdditionalAssemblies { get; init; } = [];
}
