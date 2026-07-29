using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.Diagnostics.Tracing;
using Microsoft.CodeAnalysis.Diagnostics;
using Roslyn.Workbench.Mcp.Workspace.Diagnostics;

namespace Roslyn.Workbench.Mcp.CodeActions.Test.Discovery;

public sealed class CodeActionBuiltInAnalyzerIndexTests
{
    [Fact]
    public void GIVEN_CSharpAnalyzers_WHEN_SelectingDiagnosticIds_THEN_ShouldReturnDistinctMatchingAnalyzers()
    {
        var activator = CreateActivator();
        var target = new CodeActionBuiltInAnalyzerIndex(
            [typeof(CodeActionBuiltInAnalyzerIndexTests).Assembly],
            activator.Object);

        var result = target.GetAnalyzers(new HashSet<string>(
            ["INDEX001", "INDEX002"],
            StringComparer.Ordinal));

        result.Should().ContainSingle(analyzer => analyzer is IndexedAnalyzer);
        activator.Verify(
            item => item.Activate(typeof(IndexedAnalyzer)),
            Times.Once);
    }

    [Fact]
    public void GIVEN_IndexHasBeenBuilt_WHEN_SelectingAgain_THEN_ShouldReuseInstanceState()
    {
        var activator = CreateActivator();
        var target = new CodeActionBuiltInAnalyzerIndex(
            [typeof(CodeActionBuiltInAnalyzerIndexTests).Assembly],
            activator.Object);

        var diagnosticIds = new HashSet<string>(["INDEX001"], StringComparer.Ordinal);
        _ = target.GetAnalyzers(diagnosticIds);
        _ = target.GetAnalyzers(diagnosticIds);

        activator.Verify(
            item => item.Activate(typeof(IndexedAnalyzer)),
            Times.Once);
    }

    [Fact]
    public void GIVEN_EmptyDiagnosticSelection_WHEN_SelectingAnalyzers_THEN_ShouldNotBuildIndex()
    {
        var activator = CreateActivator();
        var target = new CodeActionBuiltInAnalyzerIndex(
            [typeof(CodeActionBuiltInAnalyzerIndexTests).Assembly],
            activator.Object);

        var result = target.GetAnalyzers(new HashSet<string>(StringComparer.Ordinal));

        result.Should().BeEmpty();
        activator.Verify(
            item => item.Activate(It.IsAny<Type>()),
            Times.Never);
    }

    [Fact]
    public void GIVEN_ColdThenWarmIndex_WHEN_SelectingAnalyzers_THEN_ShouldRecordOnlyColdActivation()
    {
        var activator = CreateActivator();
        var target = new CodeActionBuiltInAnalyzerIndex(
            [typeof(CodeActionBuiltInAnalyzerIndexTests).Assembly],
            activator.Object);

        using var listener = new PerformanceEventListener();
        var diagnosticIds = new HashSet<string>(["INDEX001"], StringComparer.Ordinal);
        _ = target.GetAnalyzers(diagnosticIds);
        _ = target.GetAnalyzers(diagnosticIds);

        listener.Events.Should().ContainSingle(traceEvent =>
            traceEvent.EventName == "PhaseCompleted"
            && traceEvent.Payload != null
            && traceEvent.Payload.Count == 3
            && Equals(traceEvent.Payload[1], "code-action-diagnostics")
            && Equals(
                traceEvent.Payload[2],
                WorkbenchPerformanceEventSource.BuiltInAnalyzerActivationPhase));
    }

    [Fact]
    public void GIVEN_IncompatibleAnalyzerMetadata_WHEN_BuildingIndex_THEN_ShouldRecordWarningAndContinue()
    {
        var activator = CreateActivator();
        activator
            .Setup(item => item.Activate(typeof(UnavailableIndexedAnalyzer)))
            .Returns(CodeActionAnalyzerActivationResult.ConstructionFailed());

        var target = new CodeActionBuiltInAnalyzerIndex(
            [typeof(CodeActionBuiltInAnalyzerIndexTests).Assembly],
            activator.Object);

        var result = target.GetAnalyzers(new HashSet<string>(["INDEX001"], StringComparer.Ordinal));

        result.Should().ContainSingle(analyzer => analyzer is IndexedAnalyzer);
        target.Warnings.Should().Contain(warning =>
            warning.AnalyzerTypeName == typeof(UnavailableIndexedAnalyzer).FullName
            && warning.Status == CodeActionAnalyzerActivationStatus.ConstructionFailed);
    }

    [Fact]
    public void GIVEN_AnalyzerSupportedDiagnosticsFails_WHEN_BuildingIndex_THEN_ShouldRecordInspectionWarning()
    {
        var activator = CreateActivator();
        var target = new CodeActionBuiltInAnalyzerIndex(
            [typeof(CodeActionBuiltInAnalyzerIndexTests).Assembly],
            activator.Object);

        _ = target.GetAnalyzers(new HashSet<string>(["INDEX001"], StringComparer.Ordinal));

        target.Warnings.Should().Contain(warning =>
            warning.AnalyzerTypeName == typeof(MetadataFailureIndexedAnalyzer).FullName
            && warning.Status == CodeActionAnalyzerActivationStatus.InspectionFailed);
    }

    private static Mock<ICodeActionAnalyzerActivator> CreateActivator()
    {
        var activator = new Mock<ICodeActionAnalyzerActivator>();
        activator
            .Setup(item => item.Activate(It.IsAny<Type>()))
            .Returns<Type>(type =>
            {
                var analyzer = Activator.CreateInstance(type, nonPublic: true) as DiagnosticAnalyzer;
                if (analyzer is null)
                {
                    return CodeActionAnalyzerActivationResult.ConstructionFailed();
                }

                return CodeActionAnalyzerActivationResult.Available(analyzer);
            });

        return activator;
    }

#pragma warning disable RS1001 // Private analyser fixtures are discovered explicitly rather than exported.
#pragma warning disable RS1004 // Deliberately unavailable fixtures do not register analysis actions.
#pragma warning disable RS1036 // Test-only analysers do not ship and do not require banned API enforcement.
#pragma warning disable RS1038 // The analysers intentionally share the unit-test assembly with their tests.
#pragma warning disable RS1041 // The analysers are test fixtures rather than separately targeted compiler extensions.
#pragma warning disable CA1812 // Analyzer fixtures are activated or inspected through reflection.
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    private sealed class IndexedAnalyzer : DiagnosticAnalyzer
    {
        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } =
        [
            CreateDescriptor("INDEX001"),
            CreateDescriptor("INDEX002"),
        ];

        public override void Initialize(AnalysisContext context)
        {
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
            context.EnableConcurrentExecution();
        }
    }

    [DiagnosticAnalyzer(LanguageNames.VisualBasic)]
    private sealed class VisualBasicIndexedAnalyzer : DiagnosticAnalyzer
    {
        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } =
        [
            CreateDescriptor("INDEX001"),
        ];

        public override void Initialize(AnalysisContext context)
        {
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
            context.EnableConcurrentExecution();
        }
    }

    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    private sealed class UnavailableIndexedAnalyzer : DiagnosticAnalyzer
    {
        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } = [];

        public override void Initialize(AnalysisContext context)
        {
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
            context.EnableConcurrentExecution();
        }
    }

    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    private sealed class MetadataFailureIndexedAnalyzer : DiagnosticAnalyzer
    {
        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics
        {
            get
            {
                throw new InvalidOperationException("Metadata failure.");
            }
        }

        public override void Initialize(AnalysisContext context)
        {
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
            context.EnableConcurrentExecution();
        }
    }
#pragma warning restore RS1001
#pragma warning restore RS1004
#pragma warning restore RS1036
#pragma warning restore RS1038
#pragma warning restore RS1041
#pragma warning restore CA1812

    private static DiagnosticDescriptor CreateDescriptor(string diagnosticId)
    {
        return new DiagnosticDescriptor(
            diagnosticId,
            diagnosticId,
            diagnosticId,
            "Category",
            Microsoft.CodeAnalysis.DiagnosticSeverity.Warning,
            isEnabledByDefault: true);
    }

    private sealed class PerformanceEventListener : EventListener
    {
        private readonly ConcurrentQueue<EventWrittenEventArgs> _events = new();

        public IReadOnlyList<EventWrittenEventArgs> Events
        {
            get
            {
                return _events.ToArray();
            }
        }

        public PerformanceEventListener()
        {
            EnableEvents(WorkbenchPerformanceEventSource.Log, EventLevel.Informational);
        }

        protected override void OnEventWritten(EventWrittenEventArgs eventData)
        {
            _events.Enqueue(eventData);
        }
    }
}
