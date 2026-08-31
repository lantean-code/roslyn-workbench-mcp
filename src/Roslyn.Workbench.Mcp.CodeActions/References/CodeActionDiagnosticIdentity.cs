namespace Roslyn.Workbench.Mcp.CodeActions.References;

/// <summary>
/// Identifies the diagnostic used to rediscover a Code Action.
/// </summary>
internal sealed record CodeActionDiagnosticIdentity
{
    /// <summary>
    /// Gets the diagnostic identifier.
    /// </summary>
    public required string Id { get; init; }

    /// <summary>
    /// Gets the diagnostic message used to distinguish otherwise matching diagnostics.
    /// </summary>
    public required string Message { get; init; }

    /// <summary>
    /// Gets the diagnostic span's zero-based start position.
    /// </summary>
    public required int Start { get; init; }

    /// <summary>
    /// Gets the diagnostic span length.
    /// </summary>
    public required int Length { get; init; }
}
