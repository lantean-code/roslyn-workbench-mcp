using System.Collections.Frozen;
using System.Collections.Immutable;
using System.Reflection;
using Microsoft.Extensions.Options;

namespace Roslyn.Workbench.Mcp.CodeActions.Discovery;

/// <summary>
/// Lazily indexes built-in C# analyzers by the diagnostics they can produce.
/// </summary>
internal sealed class CodeActionBuiltInAnalyzerIndex : ICodeActionBuiltInAnalyzerIndex
{
    private const string _operationName = "code-action-diagnostics";

    private static readonly object _processStateLock = new();

    private static Lazy<IndexState>? _processState;

    private readonly Lazy<IndexState> _state;

    /// <summary>
    /// Gets analyzer types that could not be inspected or activated.
    /// </summary>
    public ImmutableArray<CodeActionAnalyzerIndexWarning> Warnings => _state.Value.Warnings;

    /// <summary>
    /// Initializes a new instance of the <see cref="CodeActionBuiltInAnalyzerIndex"/> class.
    /// </summary>
    /// <param name="options">The composition settings that enable or disable built-in assemblies.</param>
    /// <param name="analyzerActivator">The activator that creates built-in diagnostic analyzers.</param>
    public CodeActionBuiltInAnalyzerIndex(
        IOptions<CodeActionCompositionOptions> options,
        ICodeActionAnalyzerActivator analyzerActivator)
    {
        if (options.Value.IncludeBuiltInAssemblies)
        {
            _state = GetProcessState(analyzerActivator);
        }
        else
        {
            _state = CreateState([], analyzerActivator, measureActivation: false);
        }
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="CodeActionBuiltInAnalyzerIndex"/> class.
    /// </summary>
    /// <param name="assemblies">The assemblies searched for built-in analyzers.</param>
    /// <param name="analyzerActivator">The activator that creates built-in diagnostic analyzers.</param>
    internal CodeActionBuiltInAnalyzerIndex(
        IReadOnlyList<Assembly> assemblies,
        ICodeActionAnalyzerActivator analyzerActivator)
    {
        _state = CreateState(assemblies, analyzerActivator, measureActivation: true);
    }

    /// <summary>
    /// Gets distinct analyzers that may produce any requested diagnostic identifier.
    /// </summary>
    /// <param name="diagnosticIds">The diagnostic identifiers that constrain the operation.</param>
    /// <returns>The matching activated analyzer instances.</returns>
    public ImmutableArray<DiagnosticAnalyzer> GetAnalyzers(IReadOnlySet<string> diagnosticIds)
    {
        if (diagnosticIds.Count == 0)
        {
            return [];
        }

        var state = _state.Value;
        var analyzers = ImmutableArray.CreateBuilder<DiagnosticAnalyzer>();
        var seenTypes = new HashSet<Type>();
        foreach (var diagnosticId in diagnosticIds)
        {
            if (!state.AnalyzersByDiagnosticId.TryGetValue(diagnosticId, out var matchingAnalyzers))
            {
                continue;
            }

            foreach (var analyzer in matchingAnalyzers)
            {
                if (seenTypes.Add(analyzer.GetType()))
                {
                    analyzers.Add(analyzer);
                }
            }
        }

        return analyzers.ToImmutable();
    }

    private static IndexState Build(
        IReadOnlyList<Assembly> assemblies,
        ICodeActionAnalyzerActivator analyzerActivator,
        bool measureActivation)
    {
        using var phase = CreateActivationPhase(measureActivation);

        var analyzersByDiagnosticId = new Dictionary<string, List<DiagnosticAnalyzer>>(StringComparer.Ordinal);
        var warnings = ImmutableArray.CreateBuilder<CodeActionAnalyzerIndexWarning>();
        foreach (var assembly in assemblies)
        {
            foreach (var analyzerType in GetAnalyzerTypes(assembly))
            {
                var activation = analyzerActivator.Activate(analyzerType);
                if (!activation.IsAvailable)
                {
                    warnings.Add(CreateWarning(analyzerType, activation.Status));
                    continue;
                }

                ImmutableArray<DiagnosticDescriptor> supportedDiagnostics;
                try
                {
                    supportedDiagnostics = activation.Analyzer.SupportedDiagnostics;
                }
                catch (Exception exception) when (IsExpectedAnalyzerMetadataFailure(exception))
                {
                    warnings.Add(CreateWarning(
                        analyzerType,
                        CodeActionAnalyzerActivationStatus.InspectionFailed));

                    continue;
                }

                foreach (var descriptor in supportedDiagnostics)
                {
                    if (!analyzersByDiagnosticId.TryGetValue(descriptor.Id, out var analyzers))
                    {
                        analyzers = [];
                        analyzersByDiagnosticId.Add(descriptor.Id, analyzers);
                    }

                    analyzers.Add(activation.Analyzer);
                }
            }
        }

        var immutableIndex = analyzersByDiagnosticId.ToFrozenDictionary(
            static item => item.Key,
            static item => item.Value.ToImmutableArray(),
            StringComparer.Ordinal);

        return new IndexState(immutableIndex, warnings.ToImmutable());
    }

    private static PerformanceTraceScope CreateActivationPhase(bool measureActivation)
    {
        if (!measureActivation)
        {
            return default;
        }

        return WorkbenchPerformanceEventSource.Log.StartPhase(
            _operationName,
            WorkbenchPerformanceEventSource.BuiltInAnalyzerActivationPhase);
    }

    private static Lazy<IndexState> GetProcessState(ICodeActionAnalyzerActivator analyzerActivator)
    {
        lock (_processStateLock)
        {
            if (_processState is null)
            {
                var assemblies = CodeActionAssemblyResolver.ResolveBuiltInAssemblies();
                _processState = CreateState(
                    assemblies,
                    analyzerActivator,
                    measureActivation: true);
            }

            return _processState;
        }
    }

    private static Lazy<IndexState> CreateState(
        IReadOnlyList<Assembly> assemblies,
        ICodeActionAnalyzerActivator analyzerActivator,
        bool measureActivation)
    {
        var state = new Lazy<IndexState>(
            () => Build(assemblies, analyzerActivator, measureActivation),
            LazyThreadSafetyMode.ExecutionAndPublication);

        return state;
    }

    private static IEnumerable<Type> GetAnalyzerTypes(Assembly assembly)
    {
        Type[] types;
        try
        {
            types = assembly.GetTypes();
        }
        catch (ReflectionTypeLoadException exception)
        {
            types = exception.Types.OfType<Type>().ToArray();
        }

        foreach (var type in types)
        {
            if (type.IsAbstract || type.ContainsGenericParameters || !typeof(DiagnosticAnalyzer).IsAssignableFrom(type))
            {
                continue;
            }

            var attribute = type.GetCustomAttribute<DiagnosticAnalyzerAttribute>(inherit: false);
            if (attribute is not null
                && attribute.Languages.Contains(LanguageNames.CSharp, StringComparer.Ordinal))
            {
                yield return type;
            }
        }
    }

    private static CodeActionAnalyzerIndexWarning CreateWarning(
        Type analyzerType,
        CodeActionAnalyzerActivationStatus status)
    {
        return new CodeActionAnalyzerIndexWarning
        {
            AnalyzerTypeName = analyzerType.FullName ?? analyzerType.Name,
            Status = status,
        };
    }

    private static bool IsExpectedAnalyzerMetadataFailure(Exception exception)
    {
        return exception is ArgumentException
            or InvalidOperationException
            or NotSupportedException
            or TypeLoadException;
    }

    private sealed class IndexState
    {
        public FrozenDictionary<string, ImmutableArray<DiagnosticAnalyzer>> AnalyzersByDiagnosticId { get; }

        public ImmutableArray<CodeActionAnalyzerIndexWarning> Warnings { get; }

        public IndexState(
            FrozenDictionary<string, ImmutableArray<DiagnosticAnalyzer>> analyzersByDiagnosticId,
            ImmutableArray<CodeActionAnalyzerIndexWarning> warnings)
        {
            AnalyzersByDiagnosticId = analyzersByDiagnosticId;
            Warnings = warnings;
        }
    }
}
