using System.Collections.Immutable;
using Microsoft.CodeAnalysis;

namespace Roslyn.Workbench.Mcp.Plugins.Analyzers;

/// <summary>
/// Holds handler and response-shape symbols used to analyze plugin invocation implementations.
/// </summary>
internal sealed class PluginInvocationSymbols
{
    /// <summary>
    /// Gets the open generic query-handler contract.
    /// </summary>
    public INamedTypeSymbol QueryHandlerDefinition { get; }

    /// <summary>
    /// Gets the open generic mutation-handler contract.
    /// </summary>
    public INamedTypeSymbol MutationHandlerDefinition { get; }

    /// <summary>
    /// Gets the cancellation-token type used to track invocation cancellation handling.
    /// </summary>
    public INamedTypeSymbol CancellationTokenType { get; }

    /// <summary>
    /// Gets the open generic bounded collection used for agent-facing query results.
    /// </summary>
    public INamedTypeSymbol BoundedCollectionDefinition { get; }

    /// <summary>
    /// Gets the MCP protocol exception type when the transport package is referenced.
    /// </summary>
    public INamedTypeSymbol? McpProtocolExceptionType { get; }

    /// <summary>
    /// Gets the open generic collection types considered unbounded in query response contracts.
    /// </summary>
    public ImmutableArray<INamedTypeSymbol> RawCollectionDefinitions { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="PluginInvocationSymbols"/> class.
    /// </summary>
    /// <param name="queryHandlerDefinition">The open generic query-handler contract.</param>
    /// <param name="mutationHandlerDefinition">The open generic mutation-handler contract.</param>
    /// <param name="cancellationTokenType">The cancellation-token type.</param>
    /// <param name="boundedCollectionDefinition">The open generic bounded collection type.</param>
    /// <param name="mcpProtocolExceptionType">The optional MCP protocol exception type.</param>
    /// <param name="rawCollectionDefinitions">The open generic collection types treated as unbounded.</param>
    public PluginInvocationSymbols(
        INamedTypeSymbol queryHandlerDefinition,
        INamedTypeSymbol mutationHandlerDefinition,
        INamedTypeSymbol cancellationTokenType,
        INamedTypeSymbol boundedCollectionDefinition,
        INamedTypeSymbol? mcpProtocolExceptionType,
        ImmutableArray<INamedTypeSymbol> rawCollectionDefinitions)
    {
        QueryHandlerDefinition = queryHandlerDefinition;
        MutationHandlerDefinition = mutationHandlerDefinition;
        CancellationTokenType = cancellationTokenType;
        BoundedCollectionDefinition = boundedCollectionDefinition;
        McpProtocolExceptionType = mcpProtocolExceptionType;
        RawCollectionDefinitions = rawCollectionDefinitions;
    }
}
