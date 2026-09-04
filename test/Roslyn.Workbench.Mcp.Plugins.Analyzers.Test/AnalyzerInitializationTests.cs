namespace Roslyn.Workbench.Mcp.Plugins.Analyzers.Test;

public sealed class AnalyzerInitializationTests
{
    [Fact]
    public void GIVEN_NullAnalysisContext_WHEN_InitializingAuthoringAnalyzer_THEN_ShouldThrowArgumentNullException()
    {
        var target = new PluginAuthoringAnalyzer();

        AssertNullContextThrows(target);
    }

    [Fact]
    public void GIVEN_NullAnalysisContext_WHEN_InitializingEntryPointAnalyzer_THEN_ShouldThrowArgumentNullException()
    {
        var target = new PluginEntryPointAnalyzer();

        AssertNullContextThrows(target);
    }

    [Fact]
    public void GIVEN_NullAnalysisContext_WHEN_InitializingHandlerAnalyzer_THEN_ShouldThrowArgumentNullException()
    {
        var target = new PluginHandlerAnalyzer();

        AssertNullContextThrows(target);
    }

    [Fact]
    public void GIVEN_NullAnalysisContext_WHEN_InitializingInvocationAnalyzer_THEN_ShouldThrowArgumentNullException()
    {
        var target = new PluginInvocationAnalyzer();

        AssertNullContextThrows(target);
    }

    [Fact]
    public void GIVEN_NullAnalysisContext_WHEN_InitializingQueryCacheAnalyzer_THEN_ShouldThrowArgumentNullException()
    {
        var target = new PluginQueryCacheAnalyzer();

        AssertNullContextThrows(target);
    }

    private static void AssertNullContextThrows(
        Microsoft.CodeAnalysis.Diagnostics.DiagnosticAnalyzer target)
    {
        var action = () => target.Initialize(null!);

        action.Should().Throw<ArgumentNullException>().WithParameterName("context");
    }
}
