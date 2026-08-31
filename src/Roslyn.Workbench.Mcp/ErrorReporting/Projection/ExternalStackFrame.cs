namespace Roslyn.Workbench.Mcp.ErrorReporting.Projection;

/// <summary>
/// Contains the externally eligible identity and source location of one diagnostic stack frame.
/// </summary>
internal sealed record ExternalStackFrame
{
    /// <summary>
    /// Gets the component that owns the frame.
    /// </summary>
    public required ErrorReportComponent Component { get; init; }

    /// <summary>
    /// Gets the assembly name, when available.
    /// </summary>
    public string? Assembly { get; init; }

    /// <summary>
    /// Gets the declaring type name, when available.
    /// </summary>
    public string? Type { get; init; }

    /// <summary>
    /// Gets the method name, when available.
    /// </summary>
    public string? Method { get; init; }

    /// <summary>
    /// Gets the source file name for a Roslyn Workbench frame, without its directory path.
    /// </summary>
    public string? File { get; init; }

    /// <summary>
    /// Gets the one-based source line for a Roslyn Workbench frame, when available.
    /// </summary>
    public int? Line { get; init; }
}
