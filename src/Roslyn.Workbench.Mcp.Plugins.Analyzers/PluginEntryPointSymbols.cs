using Microsoft.CodeAnalysis;

namespace Roslyn.Workbench.Mcp.Plugins.Analyzers;

/// <summary>
/// Holds the symbols and version value needed to validate plugin entry points in one compilation.
/// </summary>
internal sealed class PluginEntryPointSymbols
{
    /// <summary>
    /// Gets the attribute that marks a plugin entry point.
    /// </summary>
    public INamedTypeSymbol PluginAttribute { get; }

    /// <summary>
    /// Gets the interface required on a plugin entry point.
    /// </summary>
    public INamedTypeSymbol PluginInterface { get; }

    /// <summary>
    /// Gets the attribute that publishes tool metadata from handler types.
    /// </summary>
    public INamedTypeSymbol ToolAttribute { get; }

    /// <summary>
    /// Gets the open generic query-handler contract.
    /// </summary>
    public INamedTypeSymbol QueryHandlerDefinition { get; }

    /// <summary>
    /// Gets the open generic mutation-handler contract.
    /// </summary>
    public INamedTypeSymbol MutationHandlerDefinition { get; }

    /// <summary>
    /// Gets the plugin API version supported by the referenced Plugins assembly.
    /// </summary>
    public string SupportedApiVersion { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="PluginEntryPointSymbols"/> class.
    /// </summary>
    /// <param name="pluginAttribute">The plugin marker attribute.</param>
    /// <param name="pluginInterface">The plugin entry-point interface.</param>
    /// <param name="toolAttribute">The tool metadata attribute.</param>
    /// <param name="queryHandlerDefinition">The open generic query-handler contract.</param>
    /// <param name="mutationHandlerDefinition">The open generic mutation-handler contract.</param>
    /// <param name="supportedApiVersion">The supported plugin API version.</param>
    public PluginEntryPointSymbols(
        INamedTypeSymbol pluginAttribute,
        INamedTypeSymbol pluginInterface,
        INamedTypeSymbol toolAttribute,
        INamedTypeSymbol queryHandlerDefinition,
        INamedTypeSymbol mutationHandlerDefinition,
        string supportedApiVersion)
    {
        PluginAttribute = pluginAttribute;
        PluginInterface = pluginInterface;
        ToolAttribute = toolAttribute;
        QueryHandlerDefinition = queryHandlerDefinition;
        MutationHandlerDefinition = mutationHandlerDefinition;
        SupportedApiVersion = supportedApiVersion;
    }
}
