using System.ComponentModel.DataAnnotations;

namespace Roslyn.Workbench.Mcp.ErrorReporting.Contracts;

internal sealed record PrepareErrorReportRequest
{
    [Required]
    /// <summary>
    /// Gets the Correlation Id.
    /// </summary>
    [Description("Correlation identifier of the captured failure to prepare for review.")]
    public required Guid CorrelationId { get; init; }
}
