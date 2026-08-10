using System.Diagnostics.CodeAnalysis;

namespace Roslyn.Workbench.Mcp.PluginLoading;

internal sealed class PluginTransportSchemaPreflightResult
{
    [MemberNotNullWhen(false, nameof(Failures))]
    public bool Succeeded { get; }

    public IReadOnlyList<DiagnosticInfo>? Failures { get; }

    private PluginTransportSchemaPreflightResult(
        bool succeeded,
        IReadOnlyList<DiagnosticInfo>? failures)
    {
        Succeeded = succeeded;
        Failures = failures;
    }

    public static PluginTransportSchemaPreflightResult Success()
    {
        return new PluginTransportSchemaPreflightResult(true, null);
    }

    public static PluginTransportSchemaPreflightResult Failure(IReadOnlyList<DiagnosticInfo> failures)
    {
        return new PluginTransportSchemaPreflightResult(false, failures);
    }
}
