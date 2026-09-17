namespace Roslyn.Workbench.Mcp.Workspace.Transactions;

/// <summary>
/// Identifies a compiler error independently from its movable source span.
/// </summary>
internal sealed record CompilerDiagnosticIdentity
{
    /// <summary>
    /// Gets the canonical project path or name.
    /// </summary>
    public required string Project { get; init; }

    /// <summary>
    /// Gets the loaded target framework when available.
    /// </summary>
    public string? TargetFramework { get; init; }

    /// <summary>
    /// Gets the compiler diagnostic identifier.
    /// </summary>
    public required string Id { get; init; }

    /// <summary>
    /// Gets the invariant-culture diagnostic message.
    /// </summary>
    public required string Message { get; init; }

    /// <summary>
    /// Gets the source path or non-source location kind.
    /// </summary>
    public required FileSystemPathKey Location { get; init; }
}
