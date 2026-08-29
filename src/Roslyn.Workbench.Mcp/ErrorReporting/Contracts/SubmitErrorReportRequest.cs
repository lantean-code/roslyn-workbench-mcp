using System.ComponentModel.DataAnnotations;

namespace Roslyn.Workbench.Mcp.ErrorReporting.Contracts;

internal sealed record SubmitErrorReportRequest
{
    [Required]
    /// <summary>
    /// Gets the Submission Handle.
    /// </summary>
    [Description("Opaque handle returned for the exact reviewed payload to submit.")]
    public required string SubmissionHandle { get; init; }
}
