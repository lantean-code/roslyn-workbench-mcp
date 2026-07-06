using Roslyn.Workbench.Mcp.Contracts.Selectors;

namespace Roslyn.Workbench.Mcp.Plugins;

/// <summary>
/// Resolves tool selectors and snapshot preconditions against the current execution context.
/// </summary>
public interface IToolRequestResolver
{
    /// <summary>
    /// Resolves a document selector against the current context.
    /// </summary>
    /// <typeparam name="TResponse">The tool response payload type.</typeparam>
    /// <param name="selector">The document selector.</param>
    /// <param name="context">The current execution context.</param>
    /// <returns>The resolved document or a normalized rejection.</returns>
    ToolResolutionResult<Document, TResponse> ResolveDocument<TResponse>(DocumentSelector? selector, IToolExecutionContext context);

    /// <summary>
    /// Resolves a project selector against the current context.
    /// </summary>
    /// <typeparam name="TResponse">The tool response payload type.</typeparam>
    /// <param name="selector">The project selector.</param>
    /// <param name="context">The current execution context.</param>
    /// <returns>The resolved project or a normalized rejection.</returns>
    ToolResolutionResult<Project, TResponse> ResolveProject<TResponse>(ProjectSelector? selector, IToolExecutionContext context);

    /// <summary>
    /// Resolves the documents matched by a scope selector.
    /// </summary>
    /// <typeparam name="TResponse">The tool response payload type.</typeparam>
    /// <param name="scope">The scope selector.</param>
    /// <param name="context">The current execution context.</param>
    /// <returns>The resolved documents or a normalized rejection.</returns>
    ToolResolutionResult<IReadOnlyList<Document>, TResponse> ResolveDocuments<TResponse>(ScopeSelector? scope, IToolExecutionContext context);

    /// <summary>
    /// Resolves the projects matched by a scope selector.
    /// </summary>
    /// <typeparam name="TResponse">The tool response payload type.</typeparam>
    /// <param name="scope">The scope selector.</param>
    /// <param name="context">The current execution context.</param>
    /// <returns>The resolved projects or a normalized rejection.</returns>
    ToolResolutionResult<IReadOnlyList<Project>, TResponse> ResolveProjects<TResponse>(ScopeSelector? scope, IToolExecutionContext context);

    /// <summary>
    /// Resolves a symbol selector against the current context.
    /// </summary>
    /// <typeparam name="TResponse">The tool response payload type.</typeparam>
    /// <param name="selector">The symbol selector.</param>
    /// <param name="expectedSnapshot">The expected snapshot precondition.</param>
    /// <param name="context">The current execution context.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The resolved symbol or a normalized rejection.</returns>
    ValueTask<ToolResolutionResult<ISymbol, TResponse>> ResolveSymbolAsync<TResponse>(
        SymbolSelector? selector,
        SnapshotPrecondition? expectedSnapshot,
        IToolExecutionContext context,
        CancellationToken cancellationToken);

    /// <summary>
    /// Validates a snapshot precondition against the current context.
    /// </summary>
    /// <typeparam name="TResponse">The tool response payload type.</typeparam>
    /// <param name="context">The current execution context.</param>
    /// <param name="expectedSnapshot">The expected snapshot precondition.</param>
    /// <returns>A conflict result when the snapshot does not match; otherwise <see langword="null" />.</returns>
    PluginExecutionResult<TResponse>? ValidateSnapshot<TResponse>(IToolExecutionContext context, SnapshotPrecondition? expectedSnapshot);
}
