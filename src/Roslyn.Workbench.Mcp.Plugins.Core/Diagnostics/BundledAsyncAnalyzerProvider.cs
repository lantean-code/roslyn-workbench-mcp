using System.Reflection;
using System.Runtime.Loader;

namespace Roslyn.Workbench.Mcp.Plugins.Core.Diagnostics;

internal sealed class BundledAsyncAnalyzerProvider : IBundledAsyncAnalyzerProvider
{
    public IReadOnlyList<DiagnosticAnalyzer> Analyzers { get; }

    public BundledAsyncAnalyzerProvider()
    {
        Analyzers = LoadAnalyzers();
    }

    private static DiagnosticAnalyzer[] LoadAnalyzers()
    {
        var assemblyPath = Path.Combine(AppContext.BaseDirectory, "AsyncFixer.dll");
        if (!File.Exists(assemblyPath))
        {
            throw new FileNotFoundException(
                "The bundled AsyncFixer analyzer assembly could not be found.",
                assemblyPath);
        }

        var assembly = AssemblyLoadContext.Default.LoadFromAssemblyPath(assemblyPath);
        var analyzers = assembly
            .GetTypes()
            .Where(static type =>
                !type.IsAbstract
                && !type.ContainsGenericParameters
                && typeof(DiagnosticAnalyzer).IsAssignableFrom(type)
                && type.GetCustomAttribute<DiagnosticAnalyzerAttribute>(inherit: false) is not null)
            .OrderBy(static type => type.FullName, StringComparer.Ordinal)
            .Select(CreateAnalyzer)
            .ToArray();

        ValidateDiagnosticSet(analyzers);
        return analyzers;
    }

    private static DiagnosticAnalyzer CreateAnalyzer(Type analyzerType)
    {
        if (Activator.CreateInstance(analyzerType) is not DiagnosticAnalyzer analyzer)
        {
            throw new InvalidOperationException(
                $"Bundled async analyzer '{analyzerType.FullName}' could not be activated.");
        }

        return analyzer;
    }

    private static void ValidateDiagnosticSet(IReadOnlyList<DiagnosticAnalyzer> analyzers)
    {
        var actualDiagnosticIds = analyzers
            .SelectMany(static analyzer => analyzer.SupportedDiagnostics)
            .Select(static descriptor => descriptor.Id)
            .ToHashSet(StringComparer.Ordinal);

        var expectedDiagnosticIds = new HashSet<string>(StringComparer.Ordinal)
        {
            "AsyncFixer01",
            "AsyncFixer02",
            "AsyncFixer03",
            "AsyncFixer04",
            "AsyncFixer05",
            "AsyncFixer06",
        };

        if (!actualDiagnosticIds.SetEquals(expectedDiagnosticIds))
        {
            throw new InvalidOperationException(
                "The bundled AsyncFixer analyzer set does not match the supported diagnostic contract.");
        }
    }
}
