using System.ComponentModel.DataAnnotations;

namespace Roslyn.Workbench.Mcp.ErrorReporting.Contracts;

/// <summary>
/// Selects a captured failure to project into a reviewable error report.
/// </summary>
internal sealed record PrepareErrorReportRequest
{
    /// <summary>
    /// Gets the correlation identifier of the captured failure to prepare.
    /// </summary>
    [Required]
    [Description("Correlation identifier of the captured failure to prepare for review.")]
    public required Guid CorrelationId { get; init; }
}
