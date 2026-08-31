using System.Collections.Immutable;

namespace Roslyn.Workbench.Mcp.ErrorReporting.Projection;

/// <summary>
/// Applies optional submission-time redactions to an already projected error report.
/// </summary>
internal static class ExternalErrorReportRedactor
{
    /// <summary>
    /// Creates a copy of a report with every captured exception message removed.
    /// </summary>
    /// <param name="report">The reviewed report whose exception messages should be redacted.</param>
    /// <returns>A copy that preserves diagnostic types and frames but omits exception messages.</returns>
    public static ExternalErrorReport RemoveExceptionMessages(ExternalErrorReport report)
    {
        var exceptions = ImmutableArray.CreateBuilder<ExternalException>(report.Exceptions.Length);
        foreach (var exception in report.Exceptions)
        {
            exceptions.Add(exception with { Message = null });
        }

        return report with { Exceptions = exceptions.MoveToImmutable() };
    }
}
