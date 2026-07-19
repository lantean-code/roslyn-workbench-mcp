using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using Microsoft.Extensions.Options;

namespace Roslyn.Workbench.Mcp.CodeActions.Composition;

internal sealed class MefCodeActionProviderCatalog : ICodeActionProviderCatalog
{
    private readonly CodeActionProviderCatalogComposition _composition;

    public CodeActionProviderCatalogStatus Status => _composition.Status;

    public HostServices? WorkspaceHostServices => _composition.WorkspaceHostServices;

    public IReadOnlyList<CodeRefactoringProvider> RefactoringProviders => _composition.RefactoringProviders;

    public IReadOnlyList<CodeFixProvider> CodeFixProviders => _composition.CodeFixProviders;

    public MefCodeActionProviderCatalog(
        IOptions<CodeActionCompositionOptions> options,
        IMefHostExportProviderCompatibilityAdapter exportProvider)
    {
        _composition = Compose(options.Value, exportProvider);
    }

    [SuppressMessage(
        "Design",
        "CA1031:Do not catch general exception types",
        Justification = "Code Action assemblies and providers are external startup inputs; composition failures must disable only the Code Action component and remain visible through component status.")]
    private static CodeActionProviderCatalogComposition Compose(
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
            assemblies = ResolveAssemblies(options);
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

    private static Assembly[] ResolveAssemblies(CodeActionCompositionOptions options)
    {
        var assemblies = new List<Assembly>(MefHostServices.DefaultAssemblies);
        if (options.IncludeBuiltInAssemblies)
        {
            assemblies.Add(Assembly.Load("Microsoft.CodeAnalysis.Features"));
            assemblies.Add(Assembly.Load("Microsoft.CodeAnalysis.CSharp.Features"));
        }

        assemblies.AddRange(options.AdditionalAssemblies);
        return assemblies.Distinct(CodeActionAssemblyIdentityComparer.Instance).ToArray();
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

    private static CodeActionProviderCatalogComposition Available(
        HostServices hostServices,
        CodeRefactoringProvider[] refactorings,
        CodeFixProvider[] codeFixes)
    {
        return new CodeActionProviderCatalogComposition
        {
            Status = new CodeActionProviderCatalogStatus
            {
                IsAvailable = true,
                Version = typeof(Microsoft.CodeAnalysis.Workspace).Assembly.GetName().Version?.ToString(),
                Message = $"Composed {refactorings.Length} refactoring providers and {codeFixes.Length} code-fix providers.",
            },
            WorkspaceHostServices = hostServices,
            RefactoringProviders = refactorings,
            CodeFixProviders = codeFixes,
        };
    }

    private static CodeActionProviderCatalogComposition Unavailable(string message)
    {
        return new CodeActionProviderCatalogComposition
        {
            Status = new CodeActionProviderCatalogStatus
            {
                IsAvailable = false,
                Message = message,
            },
            WorkspaceHostServices = null,
            RefactoringProviders = [],
            CodeFixProviders = [],
        };
    }

    private static string StageFailure(string stage, Exception exception)
    {
        return $"Failed while {stage} ({exception.GetType().Name}).";
    }
}
