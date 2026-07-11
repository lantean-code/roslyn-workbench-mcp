using System.Reflection;

namespace Roslyn.Workbench.Mcp.CodeActions.Composition;

internal sealed class CodeActionRuntimeComposer : ICodeActionRuntimeComposer
{
    public CodeActionRuntimeComposer()
    {
    }

    public CodeActionRuntime Compose(CodeActionRuntimeOptions options)
    {

        if (!options.IncludeBuiltInAssemblies && options.AdditionalAssemblies.Count == 0)
        {
            return Unavailable("No code-action provider assemblies were configured.");
        }

        try
        {
            var assemblies = ResolveAssemblies(options);
            if (assemblies.Count == 0)
            {
                return Unavailable("No code-action assemblies were configured.");
            }

            var hostServices = MefHostServices.Create(assemblies);
            var refactorings = GetExports<CodeRefactoringProvider>(hostServices)
                .Where(IsCSharpProvider)
                .ToArray();
            var codeFixes = GetExports<CodeFixProvider>(hostServices)
                .Where(IsCSharpProvider)
                .ToArray();

            if (refactorings.Length == 0 && codeFixes.Length == 0)
            {
                return Unavailable("No C# code-action providers were composed.");
            }

            return new CodeActionRuntime
            {
                Status = new CodeActionRuntimeStatus
                {
                    IsAvailable = true,
                    Version = typeof(Microsoft.CodeAnalysis.Workspace).Assembly.GetName().Version?.ToString(),
                    Message = $"Composed {refactorings.Length} refactoring providers and {codeFixes.Length} code-fix providers.",
                },
                WorkspaceHostServices = hostServices,
                RefactoringProviders = refactorings,
                CodeFixProviders = codeFixes,
                TokenLifetime = options.TokenLifetime,
            };
        }
        catch (Exception exception)
        {
            return Unavailable(exception.Message);
        }
    }

    private static IReadOnlyList<Assembly> ResolveAssemblies(CodeActionRuntimeOptions options)
    {
        var assemblies = new List<Assembly>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        void AddAssembly(Assembly assembly)
        {
            var key = string.IsNullOrWhiteSpace(assembly.Location)
                ? assembly.FullName ?? assembly.GetName().Name ?? Guid.NewGuid().ToString("n")
                : assembly.Location;
            if (seen.Add(key))
            {
                assemblies.Add(assembly);
            }
        }

        foreach (var assembly in MefHostServices.DefaultAssemblies)
        {
            AddAssembly(assembly);
        }

        if (options.IncludeBuiltInAssemblies)
        {
            AddAssembly(Assembly.Load("Microsoft.CodeAnalysis.Features"));
            AddAssembly(Assembly.Load("Microsoft.CodeAnalysis.CSharp.Features"));
        }

        foreach (var assembly in options.AdditionalAssemblies)
        {
            AddAssembly(assembly);
        }

        return assemblies;
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

    private static CodeActionRuntime Unavailable(string message)
    {
        return new CodeActionRuntime
        {
            Status = new CodeActionRuntimeStatus
            {
                IsAvailable = false,
                Message = message,
            },
            WorkspaceHostServices = null,
            RefactoringProviders = [],
            CodeFixProviders = [],
        };
    }

    private static IReadOnlyList<T> GetExports<T>(MefHostServices hostServices)
    {
        var method = typeof(MefHostServices)
            .GetMethods(BindingFlags.Instance | BindingFlags.NonPublic)
            .Single(candidate =>
                candidate.Name.Contains("IMefHostExportProvider.GetExports", StringComparison.Ordinal)
                && candidate.IsGenericMethodDefinition
                && candidate.GetGenericArguments().Length == 1);
        var closedMethod = method.MakeGenericMethod(typeof(T));
        var exports = (System.Collections.IEnumerable?)closedMethod.Invoke(hostServices, null) ?? Array.Empty<object>();
        var values = new List<T>();

        foreach (var export in exports)
        {
            if (export?.GetType().GetProperty("Value")?.GetValue(export) is T value)
            {
                values.Add(value);
            }
        }

        return values;
    }
}
