namespace Roslyn.Workbench.Mcp.ErrorReporting;

/// <summary>
/// Classifies the owner of an exception or stack frame included in an error report.
/// </summary>
internal enum ErrorReportComponent
{
    /// <summary>
    /// The component could not be identified or is not safe to disclose.
    /// </summary>
    Unknown,
    /// <summary>
    /// The component belongs to a Roslyn Workbench assembly.
    /// </summary>
    RoslynWorkbench,
    /// <summary>
    /// The component belongs to a Microsoft.CodeAnalysis assembly loaded by the host.
    /// </summary>
    Roslyn,
    /// <summary>
    /// The component belongs to the .NET runtime or base class libraries loaded by the host.
    /// </summary>
    DotNet,
}
