using System.ComponentModel.DataAnnotations;

namespace Roslyn.Workbench.Mcp.ErrorReporting.Contracts;

internal sealed record GetErrorDetailsRequest
{
    [Required]
    /// <summary>
    /// Gets the Correlation Id.
    /// </summary>
    [Description("Correlation identifier returned with the failed tool invocation.")]
    public required Guid CorrelationId { get; init; }
}
