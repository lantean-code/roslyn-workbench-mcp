namespace Roslyn.Workbench.Mcp.ErrorReporting.Projection;

internal interface IExternalErrorReportProjector
{
    ExternalErrorReport Project(CapturedErrorRecord record, string reportId);
}
