using System.ComponentModel.DataAnnotations;

namespace Roslyn.Workbench.Mcp.ErrorReporting.Contracts;

/// <summary>
/// Selects the exact reviewed payload to submit to the configured error-reporting provider.
/// </summary>
internal sealed record SubmitErrorReportRequest
{
    /// <summary>
    /// Gets the opaque handle returned with the reviewed payload.
    /// </summary>
    [Required]
    [Description("Opaque handle returned for the exact reviewed payload to submit.")]
    public required string SubmissionHandle { get; init; }
}
