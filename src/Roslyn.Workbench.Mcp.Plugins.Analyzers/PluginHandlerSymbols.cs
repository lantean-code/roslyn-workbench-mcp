using Microsoft.CodeAnalysis;

namespace Roslyn.Workbench.Mcp.Plugins.Analyzers;

internal sealed class PluginHandlerSymbols
{
    public INamedTypeSymbol QueryHandlerMarker { get; }

    public INamedTypeSymbol MutationHandlerMarker { get; }

    public INamedTypeSymbol QueryHandlerDefinition { get; }

    public INamedTypeSymbol MutationHandlerDefinition { get; }

    public INamedTypeSymbol RoslynToolAttribute { get; }

    public INamedTypeSymbol DisposableInterface { get; }

    public INamedTypeSymbol AsyncDisposableInterface { get; }

    public INamedTypeSymbol? ImportAttribute { get; }

    public INamedTypeSymbol? ImportManyAttribute { get; }

    public INamedTypeSymbol? ImportingConstructorAttribute { get; }

    public bool CompilationDeclaresPlugin { get; }

    public PluginHandlerSymbols(
        INamedTypeSymbol queryHandlerMarker,
        INamedTypeSymbol mutationHandlerMarker,
        INamedTypeSymbol queryHandlerDefinition,
        INamedTypeSymbol mutationHandlerDefinition,
        INamedTypeSymbol roslynToolAttribute,
        INamedTypeSymbol disposableInterface,
        INamedTypeSymbol asyncDisposableInterface,
        INamedTypeSymbol? importAttribute,
        INamedTypeSymbol? importManyAttribute,
        INamedTypeSymbol? importingConstructorAttribute,
        bool compilationDeclaresPlugin)
    {
        QueryHandlerMarker = queryHandlerMarker;
        MutationHandlerMarker = mutationHandlerMarker;
        QueryHandlerDefinition = queryHandlerDefinition;
        MutationHandlerDefinition = mutationHandlerDefinition;
        RoslynToolAttribute = roslynToolAttribute;
        DisposableInterface = disposableInterface;
        AsyncDisposableInterface = asyncDisposableInterface;
        ImportAttribute = importAttribute;
        ImportManyAttribute = importManyAttribute;
        ImportingConstructorAttribute = importingConstructorAttribute;
        CompilationDeclaresPlugin = compilationDeclaresPlugin;
    }
}
