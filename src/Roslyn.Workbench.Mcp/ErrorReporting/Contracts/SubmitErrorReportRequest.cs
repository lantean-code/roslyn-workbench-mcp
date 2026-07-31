using System.ComponentModel.DataAnnotations;

namespace Roslyn.Workbench.Mcp.ErrorReporting.Contracts;

internal sealed record SubmitErrorReportRequest
{
    [Required]
    public required string SubmissionHandle { get; init; }
}
