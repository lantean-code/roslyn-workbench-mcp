namespace Roslyn.Workbench.Mcp.Workspace.State;

/// <summary>
/// Identifies a transaction using a positive process-monotonic value.
/// </summary>
internal readonly record struct WorkspaceTransactionId
{
    /// <summary>
    /// Gets the positive transaction identifier value.
    /// </summary>
    public long Value { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="WorkspaceTransactionId"/> structure.
    /// </summary>
    /// <param name="value">The positive identifier value.</param>
    public WorkspaceTransactionId(long value)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(value);
        Value = value;
    }
}
