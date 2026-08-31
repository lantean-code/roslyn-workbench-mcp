using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using Microsoft.Extensions.Options;

namespace Roslyn.Workbench.Mcp.CodeActions.Composition;

/// <summary>
/// Composes eligible C# Code Action providers from configured Roslyn MEF assemblies.
/// </summary>
internal sealed class MefCodeActionComposition : ICodeActionComposition
{
    private readonly CodeActionCompositionState _composition;

    /// <summary>
    /// Gets the status.
    /// </summary>
    public CodeActionCompositionStatus Status => _composition.Status;

    /// <summary>
    /// Gets the workspace host services.
    /// </summary>
    public HostServices? WorkspaceHostServices => _composition.WorkspaceHostServices;

    /// <summary>
    /// Gets the refactoring providers.
    /// </summary>
    public IReadOnlyList<CodeRefactoringProvider> RefactoringProviders => _composition.RefactoringProviders;

    /// <summary>
    /// Gets the code fix providers.
    /// </summary>
    public IReadOnlyList<CodeFixProvider> CodeFixProviders => _composition.CodeFixProviders;

    /// <summary>
    /// Initializes a new instance of the <see cref="MefCodeActionComposition"/> class.
    /// </summary>
    /// <param name="options">The assembly-selection settings.</param>
    /// <param name="exportProvider">The compatibility adapter used to activate Roslyn MEF exports.</param>
    public MefCodeActionComposition(
        IOptions<CodeActionCompositionOptions> options,
        IMefHostExportProviderCompatibilityAdapter exportProvider)
    {
        _composition = Compose(options.Value, exportProvider);
    }

    [SuppressMessage(
        "Design",
        "CA1031:Do not catch general exception types",
        Justification = "Code Action assemblies and providers are external startup inputs; composition failures must disable only the Code Action component and remain visible through component status.")]
    private static CodeActionCompositionState Compose(
        CodeActionCompositionOptions options,
        IMefHostExportProviderCompatibilityAdapter exportProvider)
    {
        if (!options.IncludeBuiltInAssemblies && options.AdditionalAssemblies.Count == 0)
        {
            return Unavailable("No code-action provider assemblies were configured.");
        }

        Assembly[] assemblies;
        try
        {
            assemblies = CodeActionAssemblyResolver.Resolve(options).ToArray();
        }
        catch (Exception exception)
        {
            // Provider assemblies are external startup inputs; composition failure is published as component status.
            return Unavailable(StageFailure("resolving code-action provider assemblies", exception));
        }

        if (assemblies.Length == 0)
        {
            return Unavailable("No code-action assemblies were configured.");
        }

        MefHostServices hostServices;
        try
        {
            hostServices = MefHostServices.Create(assemblies);
        }
        catch (Exception exception)
        {
            // Roslyn MEF composition is an external compatibility boundary and must not prevent server startup.
            return Unavailable(StageFailure("creating Roslyn MEF host services", exception));
        }

        var refactoringExports = exportProvider.ReadExports<CodeRefactoringProvider>(hostServices);
        if (!refactoringExports.IsSuccessful)
        {
            return Unavailable($"Failed while reading Roslyn refactoring exports: {refactoringExports.Error}");
        }

        var codeFixExports = exportProvider.ReadExports<CodeFixProvider>(hostServices);
        if (!codeFixExports.IsSuccessful)
        {
            return Unavailable($"Failed while reading Roslyn code-fix exports: {codeFixExports.Error}");
        }

        CodeRefactoringProvider[] refactorings;
        CodeFixProvider[] codeFixes;
        try
        {
            refactorings = refactoringExports.Exports.Where(IsCSharpProvider).ToArray();
            codeFixes = codeFixExports.Exports.Where(IsCSharpProvider).ToArray();
        }
        catch (Exception exception)
        {
            // Provider metadata comes from external assemblies; invalid metadata disables only Code Actions.
            return Unavailable(StageFailure("reading code-action provider metadata", exception));
        }

        if (refactorings.Length == 0 && codeFixes.Length == 0)
        {
            return Unavailable("No C# code-action providers were composed.");
        }

        return Available(hostServices, refactorings, codeFixes);
    }

    private static bool IsCSharpProvider(object provider)
    {
        var type = provider.GetType();
        var codeFix = type.GetCustomAttributes<ExportCodeFixProviderAttribute>(inherit: false).FirstOrDefault();
        if (codeFix is not null)
        {
            return codeFix.Languages.Contains(LanguageNames.CSharp, StringComparer.Ordinal);
        }

        var refactoring = type.GetCustomAttributes<ExportCodeRefactoringProviderAttribute>(inherit: false).FirstOrDefault();
        return refactoring is not null && refactoring.Languages.Contains(LanguageNames.CSharp, StringComparer.Ordinal);
    }

    private static CodeActionCompositionState Available(
        HostServices hostServices,
        CodeRefactoringProvider[] refactorings,
        CodeFixProvider[] codeFixes)
    {
        return CodeActionCompositionState.Available(
            hostServices,
            refactorings,
            codeFixes,
            typeof(Microsoft.CodeAnalysis.Workspace).Assembly.GetName().Version?.ToString(),
            $"Composed {refactorings.Length} refactoring providers and {codeFixes.Length} code-fix providers.");
    }

    private static CodeActionCompositionState Unavailable(string message)
    {
        return CodeActionCompositionState.Unavailable(message);
    }

    private static string StageFailure(string stage, Exception exception)
    {
        return $"Failed while {stage} ({exception.GetType().Name}).";
    }
}
