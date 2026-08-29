using System.Collections.Immutable;

namespace Roslyn.Workbench.Mcp.ErrorReporting.Projection;

internal static class ExternalErrorReportRedactor
{
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
