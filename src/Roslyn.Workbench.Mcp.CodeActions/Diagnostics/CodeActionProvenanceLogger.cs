using Microsoft.Extensions.Logging;

namespace Roslyn.Workbench.Mcp.CodeActions.Diagnostics;

/// <summary>
/// Writes selected Code Action provenance through structured application logging.
/// </summary>
internal sealed partial class CodeActionProvenanceLogger : ICodeActionProvenanceLogger
{
    private readonly ILogger<CodeActionProvenanceLogger> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="CodeActionProvenanceLogger"/> class.
    /// </summary>
    /// <param name="logger">The application logger used for structured provenance events.</param>
    public CodeActionProvenanceLogger(ILogger<CodeActionProvenanceLogger> logger)
    {
        _logger = logger;
    }

    /// <inheritdoc/>
    public void LogPreparedFixAll(CodeActionMutationProvenance provenance)
    {
        if (!_logger.IsEnabled(LogLevel.Information))
        {
            return;
        }

        var diagnosticIds = JoinDiagnosticIds(provenance.DiagnosticIds);

        LogPreparedFixAllCore(
            _logger,
            provenance.Provider.TypeName,
            provenance.Provider.AssemblyName,
            provenance.Provider.AssemblyVersion,
            provenance.FixAllProvider?.TypeName,
            provenance.FixAllProvider?.AssemblyName,
            provenance.FixAllProvider?.AssemblyVersion,
            provenance.FixAllScope,
            diagnosticIds,
            provenance.EquivalenceKey);
    }

    /// <inheritdoc/>
    public void LogStaged(CodeActionMutationProvenance provenance)
    {
        if (!_logger.IsEnabled(LogLevel.Information))
        {
            return;
        }

        var diagnosticIds = JoinDiagnosticIds(provenance.DiagnosticIds);

        LogStagedCore(
            _logger,
            provenance.Kind,
            provenance.Provider.TypeName,
            provenance.Provider.AssemblyName,
            provenance.Provider.AssemblyVersion,
            provenance.FixAllProvider?.TypeName,
            provenance.FixAllProvider?.AssemblyName,
            provenance.FixAllProvider?.AssemblyVersion,
            provenance.FixAllScope,
            diagnosticIds,
            provenance.EquivalenceKey);
    }

    private static string JoinDiagnosticIds(IReadOnlyList<string> diagnosticIds)
    {
        return string.Join(',', diagnosticIds);
    }

    [LoggerMessage(
        EventId = 1,
        Level = LogLevel.Information,
        Message = "Prepared Fix All from provider {ProviderType} in {ProviderAssembly} {ProviderAssemblyVersion} using {FixAllProviderType} in {FixAllProviderAssembly} {FixAllProviderAssemblyVersion} with scope {FixAllScope}, diagnostic IDs {DiagnosticIds}, and equivalence key {EquivalenceKey}.")]
    private static partial void LogPreparedFixAllCore(
        ILogger logger,
        string providerType,
        string providerAssembly,
        string providerAssemblyVersion,
        string? fixAllProviderType,
        string? fixAllProviderAssembly,
        string? fixAllProviderAssemblyVersion,
        string? fixAllScope,
        string diagnosticIds,
        string? equivalenceKey);

    [LoggerMessage(
        EventId = 2,
        Level = LogLevel.Information,
        Message = "Staged Code Action {ActionKind} from provider {ProviderType} in {ProviderAssembly} {ProviderAssemblyVersion} using Fix All provider {FixAllProviderType} in {FixAllProviderAssembly} {FixAllProviderAssemblyVersion} with scope {FixAllScope}, diagnostic IDs {DiagnosticIds}, and equivalence key {EquivalenceKey}.")]
    private static partial void LogStagedCore(
        ILogger logger,
        CodeActionMutationKind actionKind,
        string providerType,
        string providerAssembly,
        string providerAssemblyVersion,
        string? fixAllProviderType,
        string? fixAllProviderAssembly,
        string? fixAllProviderAssemblyVersion,
        string? fixAllScope,
        string diagnosticIds,
        string? equivalenceKey);
}
