using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Roslyn.Workbench.Mcp.CodeActions.Test.Discovery;

public sealed class CodeActionAnalyzerActivatorTests
{
    private readonly CodeActionAnalyzerActivator _target;

    public CodeActionAnalyzerActivatorTests()
    {
        _target = new CodeActionAnalyzerActivator();
    }

    [Fact]
    public void GIVEN_AnalyzerType_WHEN_ActivatingByType_THEN_ShouldCreateAnalyzer()
    {
        var result = _target.Activate(typeof(AvailableAnalyzer));

        result.Status.Should().Be(CodeActionAnalyzerActivationStatus.Available);
        result.Analyzer.Should().BeOfType<AvailableAnalyzer>();
    }

    [Fact]
    public void GIVEN_NonAnalyzerType_WHEN_ActivatingByType_THEN_ShouldReportIncompatibleType()
    {
        var result = _target.Activate(typeof(CodeActionAnalyzerActivatorTests));

        result.Status.Should().Be(CodeActionAnalyzerActivationStatus.IncompatibleType);
        result.Analyzer.Should().BeNull();
    }

    [Theory]
    [InlineData(typeof(ConstructorlessAnalyzer))]
    [InlineData(typeof(ThrowingAnalyzer))]
    [InlineData(typeof(AbstractAnalyzer))]
    [InlineData(typeof(GenericAnalyzer<>))]
    [SuppressMessage(
        "Design",
        "CA1062:Validate arguments of public methods",
        Justification = "xUnit requires public test methods and supplies the non-null Type values declared by the InlineData attributes.")]
    public void GIVEN_AnalyzerCannotBeConstructed_WHEN_Activating_THEN_ShouldReportConstructionFailed(Type analyzerType)
    {
        var result = _target.Activate(analyzerType);

        result.Status.Should().Be(CodeActionAnalyzerActivationStatus.ConstructionFailed);
        result.IsAvailable.Should().BeFalse();
        result.Analyzer.Should().BeNull();
    }

#pragma warning disable CA1812 // Analyzer fixtures are activated or deliberately rejected through reflection.
#pragma warning disable RS1001 // Private analyser fixtures are resolved explicitly by type name rather than exported.

    private sealed class AvailableAnalyzer : DiagnosticAnalyzer
    {
        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } = [];

        public override void Initialize(AnalysisContext context)
        {
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
            context.EnableConcurrentExecution();
        }
    }

    private sealed class ConstructorlessAnalyzer : DiagnosticAnalyzer
    {
        public ConstructorlessAnalyzer(string value)
        {
            Value = value;
        }

        public string Value { get; }

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } = [];

        public override void Initialize(AnalysisContext context)
        {
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
            context.EnableConcurrentExecution();
        }
    }

    private sealed class ThrowingAnalyzer : DiagnosticAnalyzer
    {
        public ThrowingAnalyzer()
        {
            throw new InvalidOperationException("Construction failed.");
        }

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } = [];

        public override void Initialize(AnalysisContext context)
        {
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
            context.EnableConcurrentExecution();
        }
    }

    private abstract class AbstractAnalyzer : DiagnosticAnalyzer
    {
    }

    private sealed class GenericAnalyzer<T> : DiagnosticAnalyzer
    {
        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } = [];

        public override void Initialize(AnalysisContext context)
        {
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
            context.EnableConcurrentExecution();
        }
    }

#pragma warning restore RS1001
#pragma warning restore CA1812
}
