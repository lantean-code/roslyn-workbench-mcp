using System.Diagnostics.CodeAnalysis;

namespace Roslyn.Workbench.Mcp.PluginLoading;

/// <summary>
/// Reports whether all plugin contracts are publishable and collects any blocking schema diagnostics.
/// </summary>
internal sealed class PluginTransportSchemaPreflightResult
{
    /// <summary>
    /// Gets a value indicating whether every plugin tool schema passed preflight validation.
    /// </summary>
    [MemberNotNullWhen(false, nameof(Failures))]
    public bool Succeeded { get; }

    /// <summary>
    /// Gets the schema diagnostics that prevent plugin tools from being published.
    /// </summary>
    public IReadOnlyList<DiagnosticInfo>? Failures { get; }

    private PluginTransportSchemaPreflightResult(
        bool succeeded,
        IReadOnlyList<DiagnosticInfo>? failures)
    {
        Succeeded = succeeded;
        Failures = failures;
    }

    /// <summary>
    /// Creates a result that represents successful completion.
    /// </summary>
    /// <returns>A result that represents successful completion.</returns>
    public static PluginTransportSchemaPreflightResult Success()
    {
        return new PluginTransportSchemaPreflightResult(true, null);
    }

    /// <summary>
    /// Creates a result that represents failure.
    /// </summary>
    /// <param name="failures">The transport-schema failures produced by preflight validation.</param>
    /// <returns>A result that represents failure.</returns>
    public static PluginTransportSchemaPreflightResult Failure(IReadOnlyList<DiagnosticInfo> failures)
    {
        return new PluginTransportSchemaPreflightResult(false, failures);
    }
}
