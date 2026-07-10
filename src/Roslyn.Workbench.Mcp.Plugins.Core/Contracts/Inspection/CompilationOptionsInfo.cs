namespace Roslyn.Workbench.Mcp.Plugins.Core.Contracts.Inspection;

/// <summary>
/// Represents selected Roslyn compilation options for a project.
/// </summary>
public sealed record CompilationOptionsInfo
{
    /// <summary>
    /// Gets the output kind.
    /// </summary>
    public string OutputKind { get; init; } = string.Empty;

    /// <summary>
    /// Gets the nullable context mode.
    /// </summary>
    public string? NullableContext { get; init; }

    /// <summary>
    /// Gets a value indicating whether unsafe code is allowed.
    /// </summary>
    public bool AllowUnsafe { get; init; }

    /// <summary>
    /// Gets the optimization level.
    /// </summary>
    public string OptimizationLevel { get; init; } = string.Empty;

    /// <summary>
    /// Gets the warning level.
    /// </summary>
    public int WarningLevel { get; init; }

    /// <summary>
    /// Gets the effective preprocessor symbols.
    /// </summary>
    public IReadOnlyList<string> PreprocessorSymbols { get; init; } = [];
}
