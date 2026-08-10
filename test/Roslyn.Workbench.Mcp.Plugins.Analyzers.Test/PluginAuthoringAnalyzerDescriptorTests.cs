using System.Globalization;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Roslyn.Workbench.Mcp.Plugins.Analyzers.Test;

public sealed class PluginAuthoringAnalyzerDescriptorTests
{
    [Fact]
    [Trait("Category", "Contract")]
    public void GIVEN_BatchOneDiagnostics_WHEN_ReadingDescriptors_THEN_ShouldExposeStableContracts()
    {
        var target = new PluginAuthoringAnalyzer();

        var descriptors = target.SupportedDiagnostics;
        (string Id, string Title, string Message)[] expected =
        [
            (
                "RWMCP001",
                "Do not mutate the Roslyn Workspace directly",
                "Do not call Workspace.TryApplyChanges; return a MutationCandidate through a mutation tool"),
            (
                "RWMCP002",
                "Use the invocation solution snapshot",
                "Do not read Workspace.CurrentSolution; use the invocation context's CurrentSolution snapshot"),
            (
                "RWMCP003",
                "Plugin configuration must complete synchronously",
                "IRoslynPlugin.Configure must not be async"),
            (
                "RWMCP004",
                "Do not retain startup configuration objects",
                "Do not retain or escape the startup configuration object or a tool configuration builder"),
            (
                "RWMCP022",
                "Use a protocol-compatible MCP tool name",
                "Tool name '{0}' must contain 1 to 128 ASCII letters, digits, underscores, hyphens, or periods"),
        ];

        descriptors.Should().HaveCount(expected.Length);
        for (var index = 0; index < expected.Length; index++)
        {
            var descriptor = descriptors[index];
            var (id, title, message) = expected[index];
            var actualTitle = descriptor.Title.ToString(CultureInfo.InvariantCulture);
            var actualMessage = descriptor.MessageFormat.ToString(CultureInfo.InvariantCulture);
            var expectedHelpLink =
                $"https://github.com/lantean-code/roslyn-workbench-mcp/blob/main/docs/PluginAuthoringDiagnostics.md#{id}";

            descriptor.Id.Should().Be(id);
            actualTitle.Should().Be(title);
            actualMessage.Should().Be(message);
            descriptor.Category.Should().Be("RoslynWorkbench.PluginAuthoring");
            descriptor.DefaultSeverity.Should().Be(DiagnosticSeverity.Error);
            descriptor.IsEnabledByDefault.Should().BeTrue();
            descriptor.HelpLinkUri.Should().Be(expectedHelpLink);
        }
    }

    [Fact]
    [Trait("Category", "Contract")]
    public void GIVEN_BatchTwoDiagnostics_WHEN_ReadingDescriptors_THEN_ShouldExposeStableContracts()
    {
        var target = new PluginHandlerAnalyzer();

        var descriptors = target.SupportedDiagnostics;
        (string Id, string Title, string Message, DiagnosticSeverity Severity)[] expected =
        [
            (
                "RWMCP005",
                "Implement exactly one handler contract",
                "Plugin handler '{0}' must implement exactly one closed query or mutation handler contract and no contract from the other family",
                DiagnosticSeverity.Error),
            (
                "RWMCP006",
                "Plugin handlers must not own a disposable lifetime",
                "Plugin handler '{0}' must not implement IDisposable or IAsyncDisposable",
                DiagnosticSeverity.Error),
            (
                "RWMCP007",
                "Plugin handlers must not declare MEF imports",
                "Plugin handler member '{0}' must not declare a MEF import",
                DiagnosticSeverity.Error),
            (
                "RWMCP008",
                "External transport contract types must be public",
                "Tool contract type '{0}' and all containing and component types must be public",
                DiagnosticSeverity.Error),
            (
                "RWMCP009",
                "Handler instance state requires thread-safety review",
                "Plugin handler member '{0}' introduces instance state and requires a thread-safety review",
                DiagnosticSeverity.Warning),
            (
                "RWMCP010",
                "Avoid mutable static handler state",
                "Plugin handler field '{0}' declares mutable static state",
                DiagnosticSeverity.Warning),
            (
                "RWMCP011",
                "Handler field may own a disposable resource",
                "Plugin handler field '{0}' may own a disposable resource",
                DiagnosticSeverity.Warning),
            (
                "RWMCP012",
                "Query tools cannot declare destructive behaviour",
                "Query handler '{0}' cannot declare destructive behaviour",
                DiagnosticSeverity.Error),
            (
                "RWMCP022",
                "Use a protocol-compatible MCP tool name",
                "Tool name '{0}' must contain 1 to 128 ASCII letters, digits, underscores, hyphens, or periods",
                DiagnosticSeverity.Error),
        ];

        descriptors.Should().HaveCount(expected.Length);
        for (var index = 0; index < expected.Length; index++)
        {
            var descriptor = descriptors[index];
            var (id, title, message, severity) = expected[index];
            var actualTitle = descriptor.Title.ToString(CultureInfo.InvariantCulture);
            var actualMessage = descriptor.MessageFormat.ToString(CultureInfo.InvariantCulture);
            var expectedHelpLink =
                $"https://github.com/lantean-code/roslyn-workbench-mcp/blob/main/docs/PluginAuthoringDiagnostics.md#{id}";

            descriptor.Id.Should().Be(id);
            actualTitle.Should().Be(title);
            actualMessage.Should().Be(message);
            descriptor.Category.Should().Be("RoslynWorkbench.PluginAuthoring");
            descriptor.DefaultSeverity.Should().Be(severity);
            descriptor.IsEnabledByDefault.Should().BeTrue();
            descriptor.HelpLinkUri.Should().Be(expectedHelpLink);
        }
    }

    [Fact]
    [Trait("Category", "Contract")]
    public void GIVEN_BatchThreeDiagnostics_WHEN_ReadingDescriptors_THEN_ShouldExposeStableContracts()
    {
        DiagnosticAnalyzer[] analyzers =
        [
            new PluginInvocationAnalyzer(),
            new PluginEntryPointAnalyzer(),
        ];

        var descriptors = analyzers
            .SelectMany(static analyzer => analyzer.SupportedDiagnostics)
            .ToArray();

        (string Id, string Title, string Message, DiagnosticSeverity Severity)[] expected =
        [
            (
                "RWMCP013",
                "Observe the invocation cancellation token",
                "Handler method '{0}' does not meaningfully observe or forward its cancellation token",
                DiagnosticSeverity.Info),
            (
                "RWMCP014",
                "Bound agent-facing query collections",
                "Query response member '{0}' exposes an unbounded collection; use BoundedCollection<TItem>",
                DiagnosticSeverity.Warning),
            (
                "RWMCP015",
                "Plugin entry-point marker and contract must agree",
                "Plugin entry-point type '{0}' must be a concrete IRoslynPlugin implementation with RoslynPluginAttribute",
                DiagnosticSeverity.Error),
            (
                "RWMCP016",
                "A plugin assembly cannot declare multiple marked entry points",
                "Plugin entry point '{0}' conflicts with another RoslynPluginAttribute in this assembly",
                DiagnosticSeverity.Error),
            (
                "RWMCP017",
                "Declare the supported plugin API version",
                "Plugin API version must be the referenced Plugins API version '{0}'",
                DiagnosticSeverity.Error),
            (
                "RWMCP018",
                "Plugin identity metadata must not be blank",
                "Plugin {0} must not be null, empty or whitespace",
                DiagnosticSeverity.Error),
            (
                "RWMCP019",
                "Tool metadata must decorate a handler",
                "Type '{0}' declares RoslynToolAttribute but implements no closed query or mutation handler contract",
                DiagnosticSeverity.Error),
        ];

        descriptors.Should().HaveCount(expected.Length);
        for (var index = 0; index < expected.Length; index++)
        {
            var descriptor = descriptors[index];
            var (id, title, message, severity) = expected[index];
            var actualTitle = descriptor.Title.ToString(CultureInfo.InvariantCulture);
            var actualMessage = descriptor.MessageFormat.ToString(CultureInfo.InvariantCulture);
            var expectedHelpLink =
                $"https://github.com/lantean-code/roslyn-workbench-mcp/blob/main/docs/PluginAuthoringDiagnostics.md#{id}";

            descriptor.Id.Should().Be(id);
            actualTitle.Should().Be(title);
            actualMessage.Should().Be(message);
            descriptor.Category.Should().Be("RoslynWorkbench.PluginAuthoring");
            descriptor.DefaultSeverity.Should().Be(severity);
            descriptor.IsEnabledByDefault.Should().BeTrue();
            descriptor.HelpLinkUri.Should().Be(expectedHelpLink);
        }
    }
}
