using System.ComponentModel.DataAnnotations;

namespace Roslyn.Workbench.Mcp.ErrorReporting.Contracts;

/// <summary>
/// Selects a captured failure whose local diagnostic details should be returned.
/// </summary>
internal sealed record GetErrorDetailsRequest
{
    /// <summary>
    /// Gets the correlation identifier returned by the failed tool invocation.
    /// </summary>
    [Required]
    [Description("Correlation identifier returned with the failed tool invocation.")]
    public required Guid CorrelationId { get; init; }
}
