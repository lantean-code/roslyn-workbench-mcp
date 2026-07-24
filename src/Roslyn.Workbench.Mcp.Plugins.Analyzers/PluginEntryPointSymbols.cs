using Microsoft.CodeAnalysis;

namespace Roslyn.Workbench.Mcp.Plugins.Analyzers;

internal sealed class PluginEntryPointSymbols
{
    public INamedTypeSymbol PluginAttribute { get; }

    public INamedTypeSymbol PluginInterface { get; }

    public INamedTypeSymbol ToolAttribute { get; }

    public INamedTypeSymbol QueryHandlerDefinition { get; }

    public INamedTypeSymbol MutationHandlerDefinition { get; }

    public string SupportedApiVersion { get; }

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
