namespace Roslyn.Workbench.Mcp.ErrorReporting.Configuration;

/// <summary>
/// Controls whether error reports are disabled, individually approved or pre-approved by server configuration.
/// </summary>
internal enum ErrorReportingConsentMode
{
    /// <summary>
    /// Disables error reporting and its MCP tools.
    /// </summary>
    Never,
    /// <summary>
    /// Requires the user to approve each prepared report before submission.
    /// </summary>
    Prompt,
    /// <summary>
    /// Treats every prepared report as approved by server configuration.
    /// </summary>
    Always,
}
