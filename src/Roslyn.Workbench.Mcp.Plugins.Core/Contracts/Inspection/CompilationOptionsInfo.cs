namespace Roslyn.Workbench.Mcp.Plugins.Core.Contracts.Inspection;

/// <summary>
/// Represents selected Roslyn compilation options for a project.
/// </summary>
internal sealed record CompilationOptionsInfo
{
    /// <summary>
    /// Gets the output kind.
    /// </summary>
    [Description("The output kind.")]
    public required string OutputKind { get; init; }

    /// <summary>
    /// Gets the nullable context mode.
    /// </summary>
    [Description("The nullable context mode.")]
    public string? NullableContext { get; init; }

    /// <summary>
    /// Gets a value indicating whether unsafe code is allowed.
    /// </summary>
    [Description("Whether unsafe code is allowed.")]
    public bool AllowUnsafe { get; init; }

    /// <summary>
    /// Gets the optimization level.
    /// </summary>
    [Description("The optimization level.")]
    public required string OptimizationLevel { get; init; }

    /// <summary>
    /// Gets the warning level.
    /// </summary>
    [Description("The warning level.")]
    public int WarningLevel { get; init; }

    /// <summary>
    /// Gets the effective preprocessor symbols.
    /// </summary>
    [Description("The effective preprocessor symbols.")]
    public IReadOnlyList<string> PreprocessorSymbols { get; init; } = [];
}
