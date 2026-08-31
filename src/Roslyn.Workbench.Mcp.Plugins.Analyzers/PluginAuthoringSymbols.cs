using Microsoft.CodeAnalysis;

namespace Roslyn.Workbench.Mcp.Plugins.Analyzers;

/// <summary>
/// Holds the framework symbols required to enforce plugin authoring and configuration rules for one compilation.
/// </summary>
internal sealed class PluginAuthoringSymbols
{
    /// <summary>
    /// Gets the Roslyn workspace type used to identify direct workspace access.
    /// </summary>
    public INamedTypeSymbol WorkspaceType { get; }

    /// <summary>
    /// Gets the plugin entry-point contract type.
    /// </summary>
    public INamedTypeSymbol PluginContractType { get; }

    /// <summary>
    /// Gets the startup plugin configuration type whose lifetime is restricted to configuration.
    /// </summary>
    public INamedTypeSymbol PluginConfigurationType { get; }

    /// <summary>
    /// Gets the non-generic query-handler marker type.
    /// </summary>
    public INamedTypeSymbol QueryHandlerType { get; }

    /// <summary>
    /// Gets the non-generic mutation-handler marker type.
    /// </summary>
    public INamedTypeSymbol MutationHandlerType { get; }

    /// <summary>
    /// Gets the generic tool configuration builder type whose lifetime is restricted to configuration.
    /// </summary>
    public INamedTypeSymbol BuilderType { get; }

    /// <summary>
    /// Gets a value indicating whether the current compilation declares a plugin and should therefore receive plugin-specific diagnostics.
    /// </summary>
    public bool CompilationDeclaresPlugin { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="PluginAuthoringSymbols"/> class.
    /// </summary>
    /// <param name="workspaceType">The Roslyn workspace type.</param>
    /// <param name="pluginContractType">The plugin entry-point contract type.</param>
    /// <param name="pluginConfigurationType">The startup plugin configuration type.</param>
    /// <param name="queryHandlerType">The query-handler marker type.</param>
    /// <param name="mutationHandlerType">The mutation-handler marker type.</param>
    /// <param name="builderType">The generic tool configuration builder type.</param>
    /// <param name="compilationDeclaresPlugin">Whether the compilation declares a plugin.</param>
    public PluginAuthoringSymbols(
        INamedTypeSymbol workspaceType,
        INamedTypeSymbol pluginContractType,
        INamedTypeSymbol pluginConfigurationType,
        INamedTypeSymbol queryHandlerType,
        INamedTypeSymbol mutationHandlerType,
        INamedTypeSymbol builderType,
        bool compilationDeclaresPlugin)
    {
        WorkspaceType = workspaceType;
        PluginContractType = pluginContractType;
        PluginConfigurationType = pluginConfigurationType;
        QueryHandlerType = queryHandlerType;
        MutationHandlerType = mutationHandlerType;
        BuilderType = builderType;
        CompilationDeclaresPlugin = compilationDeclaresPlugin;
    }
}
