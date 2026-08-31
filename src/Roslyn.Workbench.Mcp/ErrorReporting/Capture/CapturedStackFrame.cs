namespace Roslyn.Workbench.Mcp.ErrorReporting.Capture;

/// <summary>
/// Identifies a stack frame retained from a tool failure and classifies the component that owns it.
/// </summary>
internal sealed record CapturedStackFrame
{
    /// <summary>
    /// Roslyn Workbench component that owns this frame.
    /// </summary>
    [Description("Roslyn Workbench component that owns this frame.")]
    public ErrorReportComponent Component { get; init; }

    /// <summary>
    /// Assembly name, when available.
    /// </summary>
    [Description("Assembly name, when available.")]
    public string? Assembly { get; init; }

    /// <summary>
    /// Declaring type name, when available.
    /// </summary>
    [Description("Declaring type name, when available.")]
    public string? Type { get; init; }

    /// <summary>
    /// Method name, when available.
    /// </summary>
    [Description("Method name, when available.")]
    public string? Method { get; init; }

    /// <summary>
    /// First-party source file path recorded by symbols, when available.
    /// </summary>
    [Description("First-party source file path recorded by symbols, when available.")]
    public string? File { get; init; }

    /// <summary>
    /// One-based source line number, when available.
    /// </summary>
    [Description("One-based source line number, when available.")]
    public int? Line { get; init; }
}
