namespace Roslyn.Workbench.Mcp.Plugins.Execution;

/// <summary>
/// Represents the leased execution context for one tool invocation.
/// </summary>
/// <typeparam name="TContext">The context type.</typeparam>
public sealed class ToolExecutionContextLease<TContext> : IAsyncDisposable
    where TContext : class, IToolExecutionContext
{
    private readonly IAsyncDisposable? _lease;

    private ToolExecutionContextLease(TContext? context, PluginExecutionResultBox? shortCircuitResult, IAsyncDisposable? lease)
    {
        Context = context;
        ShortCircuitResult = shortCircuitResult;
        _lease = lease;
    }

    /// <summary>
    /// Gets the leased execution context, when one was acquired.
    /// </summary>
    public TContext? Context { get; }

    /// <summary>
    /// Gets the host-generated short-circuit result, when the invocation should not reach the plugin.
    /// </summary>
    public PluginExecutionResultBox? ShortCircuitResult { get; }

    /// <summary>
    /// Creates a successful leased context.
    /// </summary>
    /// <param name="context">The leased execution context.</param>
    /// <param name="lease">The lease to dispose when execution completes.</param>
    /// <returns>The leased execution context.</returns>
    public static ToolExecutionContextLease<TContext> Acquired(TContext context, IAsyncDisposable? lease = null)
    {
        ArgumentNullException.ThrowIfNull(context);

        return new ToolExecutionContextLease<TContext>(context, null, lease);
    }

    /// <summary>
    /// Creates a short-circuit result with an optional execution context.
    /// </summary>
    /// <param name="result">The short-circuit result.</param>
    /// <param name="context">The optional execution context.</param>
    /// <param name="lease">The lease to dispose when execution completes.</param>
    /// <returns>The short-circuit context lease.</returns>
    public static ToolExecutionContextLease<TContext> Rejected(
        PluginExecutionResultBox result,
        TContext? context = null,
        IAsyncDisposable? lease = null)
    {
        ArgumentNullException.ThrowIfNull(result);

        return new ToolExecutionContextLease<TContext>(context, result, lease);
    }

    /// <inheritdoc />
    public ValueTask DisposeAsync()
    {
        return _lease is null ? ValueTask.CompletedTask : _lease.DisposeAsync();
    }
}
