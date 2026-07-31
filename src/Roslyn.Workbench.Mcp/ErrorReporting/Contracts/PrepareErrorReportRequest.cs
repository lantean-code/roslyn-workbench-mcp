using System.ComponentModel.DataAnnotations;

namespace Roslyn.Workbench.Mcp.ErrorReporting.Contracts;

internal sealed record PrepareErrorReportRequest
{
    [Required]
    public required Guid CorrelationId { get; init; }
}
