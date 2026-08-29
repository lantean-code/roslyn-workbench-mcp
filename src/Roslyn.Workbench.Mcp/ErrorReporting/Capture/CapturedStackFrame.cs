namespace Roslyn.Workbench.Mcp.ErrorReporting.Capture;

internal sealed record CapturedStackFrame
{
    /// <summary>
    /// Gets the Component.
    /// </summary>
    [Description("Roslyn Workbench component that owns this frame.")]
    public ErrorReportComponent Component { get; init; }

    /// <summary>
    /// Gets the Assembly.
    /// </summary>
    [Description("Assembly name, when available.")]
    public string? Assembly { get; init; }

    /// <summary>
    /// Gets the Type.
    /// </summary>
    [Description("Declaring type name, when available.")]
    public string? Type { get; init; }

    /// <summary>
    /// Gets the Method.
    /// </summary>
    [Description("Method name, when available.")]
    public string? Method { get; init; }

    /// <summary>
    /// Gets the File.
    /// </summary>
    [Description("First-party source file path recorded by symbols, when available.")]
    public string? File { get; init; }

    /// <summary>
    /// Gets the Line.
    /// </summary>
    [Description("One-based source line number, when available.")]
    public int? Line { get; init; }
}
