using Microsoft.CodeAnalysis;

namespace Roslyn.Workbench.Mcp.Plugins.Analyzers;

/// <summary>
/// Holds framework and plugin symbols used to validate handler contracts, state and composition.
/// </summary>
internal sealed class PluginHandlerSymbols
{
    /// <summary>
    /// Gets the non-generic query-handler marker interface.
    /// </summary>
    public INamedTypeSymbol QueryHandlerMarker { get; }

    /// <summary>
    /// Gets the non-generic mutation-handler marker interface.
    /// </summary>
    public INamedTypeSymbol MutationHandlerMarker { get; }

    /// <summary>
    /// Gets the open generic query-handler contract.
    /// </summary>
    public INamedTypeSymbol QueryHandlerDefinition { get; }

    /// <summary>
    /// Gets the open generic mutation-handler contract.
    /// </summary>
    public INamedTypeSymbol MutationHandlerDefinition { get; }

    /// <summary>
    /// Gets the attribute that publishes a handler as an MCP tool.
    /// </summary>
    public INamedTypeSymbol RoslynToolAttribute { get; }

    /// <summary>
    /// Gets the synchronous disposable interface used to detect handler-owned lifetimes.
    /// </summary>
    public INamedTypeSymbol DisposableInterface { get; }

    /// <summary>
    /// Gets the asynchronous disposable interface used to detect handler-owned lifetimes.
    /// </summary>
    public INamedTypeSymbol AsyncDisposableInterface { get; }

    /// <summary>
    /// Gets the MEF single-import attribute when the composition package is referenced.
    /// </summary>
    public INamedTypeSymbol? ImportAttribute { get; }

    /// <summary>
    /// Gets the MEF multiple-import attribute when the composition package is referenced.
    /// </summary>
    public INamedTypeSymbol? ImportManyAttribute { get; }

    /// <summary>
    /// Gets the MEF importing-constructor attribute when the composition package is referenced.
    /// </summary>
    public INamedTypeSymbol? ImportingConstructorAttribute { get; }

    /// <summary>
    /// Gets a value indicating whether the compilation declares a plugin and should receive plugin-specific diagnostics.
    /// </summary>
    public bool CompilationDeclaresPlugin { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="PluginHandlerSymbols"/> class.
    /// </summary>
    /// <param name="queryHandlerMarker">The non-generic query-handler marker.</param>
    /// <param name="mutationHandlerMarker">The non-generic mutation-handler marker.</param>
    /// <param name="queryHandlerDefinition">The open generic query-handler contract.</param>
    /// <param name="mutationHandlerDefinition">The open generic mutation-handler contract.</param>
    /// <param name="roslynToolAttribute">The tool metadata attribute.</param>
    /// <param name="disposableInterface">The synchronous disposable interface.</param>
    /// <param name="asyncDisposableInterface">The asynchronous disposable interface.</param>
    /// <param name="importAttribute">The optional MEF single-import attribute.</param>
    /// <param name="importManyAttribute">The optional MEF multiple-import attribute.</param>
    /// <param name="importingConstructorAttribute">The optional MEF importing-constructor attribute.</param>
    /// <param name="compilationDeclaresPlugin">Whether the compilation declares a plugin.</param>
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
