using Microsoft.CodeAnalysis;

namespace Roslyn.Workbench.Mcp.Plugins.Analyzers;

internal sealed class PluginAuthoringSymbols
{
    public INamedTypeSymbol WorkspaceType { get; }

    public INamedTypeSymbol PluginContractType { get; }

    public INamedTypeSymbol PluginConfigurationType { get; }

    public INamedTypeSymbol QueryHandlerType { get; }

    public INamedTypeSymbol MutationHandlerType { get; }

    public INamedTypeSymbol BuilderType { get; }

    public bool CompilationDeclaresPlugin { get; }

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
