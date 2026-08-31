namespace Roslyn.Workbench.Mcp.ErrorReporting.Projection;

/// <summary>
/// Converts locally captured failures into privacy-filtered reports eligible for external review.
/// </summary>
internal interface IExternalErrorReportProjector
{
    /// <summary>
    /// Projects a captured error into its externally reportable form.
    /// </summary>
    /// <param name="record">The locally retained diagnostic record.</param>
    /// <param name="reportId">The opaque identifier assigned to the projected report.</param>
    /// <returns>A privacy-filtered report containing only externally eligible diagnostic fields.</returns>
    ExternalErrorReport Project(CapturedErrorRecord record, string reportId);
}
