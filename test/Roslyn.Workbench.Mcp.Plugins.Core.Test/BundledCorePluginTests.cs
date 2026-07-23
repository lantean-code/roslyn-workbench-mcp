namespace Roslyn.Workbench.Mcp.Plugins.Core.Test;

public sealed class BundledCorePluginTests
{
    [Fact]
    public void GIVEN_NullConfiguration_WHEN_Configuring_THEN_ShouldThrowArgumentNullException()
    {
        var target = new BundledCorePlugin();

        var action = () => target.Configure(null!);

        action.Should().Throw<ArgumentNullException>().WithParameterName("configuration");
    }

    [Fact]
    public void GIVEN_BundledPlugin_WHEN_ConfiguringAndMaterialising_THEN_ShouldPublishEveryExpectedToolExactlyOnce()
    {
        var plugin = new BundledCorePlugin();
        var configuration = new PluginConfiguration();
        plugin.Configure(configuration);
        configuration.Freeze();
        var metadata = new PluginMetadata
        {
            PluginId = "roslyn.workbench.core",
            DisplayName = "Roslyn Workbench Core",
            Version = "1.0.0",
            SupportedApiVersion = PluginApiVersions.V1,
        };

        var configurationPreparer = new PluginConfigurationPreparer(
            new PluginHandlerTypeInspector(),
            new PluginHandlerContractResolver(),
            new PluginHandlerWarningInspector());

        var toolRegistrationMaterializer = new PluginToolRegistrationMaterializer();

        var preparation = configurationPreparer.Prepare(
            metadata,
            configuration,
            PluginContractAccessibility.AllowNonPublic);
        var result = toolRegistrationMaterializer.Materialize(preparation);

        var metadataByName = result.Tools.ToDictionary(static tool => tool.Tool.Metadata.Name, static tool => tool.Tool.Metadata, StringComparer.Ordinal);
        var names = metadataByName.Keys.ToArray();
        names.Should().BeEquivalentTo(ExpectedDescriptions.Keys);
        names.Should().OnlyHaveUniqueItems();
        foreach (var expectedDescription in ExpectedDescriptions)
        {
            metadataByName[expectedDescription.Key].Description.Should().Be(expectedDescription.Value);
        }

        result.Tools.Should().OnlyContain(static tool => tool.Tool.Metadata.ResultSummary == null);
        result.Tools
            .Where(static tool => tool.Tool.Metadata.Behavior.Destructive)
            .Select(static tool => tool.Tool.Metadata.Name)
            .Should()
            .BeEquivalentTo("rename-symbol", "sort-usings", "format-document");
    }

    private static readonly IReadOnlyDictionary<string, string> ExpectedDescriptions = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["get-solution-structure"] = "Returns solution folders, projects, target frameworks and direct project relationships.",
        ["get-project-details"] = "Returns project metadata, options and selected document details.",
        ["get-document-options"] = "Returns language, parse and analyzer-config options for a document.",
        ["get-document-outline"] = "Returns a semantic outline for one document.",
        ["get-code-metrics"] = "Returns projected code metrics for a scope or symbol.",
        ["get-code-context"] = "Returns a bounded code window with the enclosing semantic context for a selected location.",
        ["search-symbols"] = "Searches declarations by name, metadata name and optional semantic filters.",
        ["resolve-symbol"] = "Resolves the symbol at a location or selection and returns its canonical selector.",
        ["get-symbol-info"] = "Returns detailed metadata for a resolved symbol.",
        ["get-symbol-members"] = "Lists declared members and optional inherited or interface members for a resolved symbol.",
        ["get-symbol-attributes"] = "Returns declared and inherited attributes for a resolved symbol.",
        ["go-to-definition"] = "Finds source or metadata definitions for a resolved symbol.",
        ["find-references"] = "Finds source references, optionally including declarations and access classification.",
        ["find-callers"] = "Returns direct source call sites and containing symbols.",
        ["find-callees"] = "Returns symbols directly invoked by a method or selected executable body.",
        ["find-implementations"] = "Finds implementations of an interface or abstract member.",
        ["find-overrides"] = "Finds overrides of a virtual or abstract member.",
        ["find-derived-types"] = "Finds derived types for a resolved base type.",
        ["get-type-hierarchy"] = "Returns base, interface, and optional derived type relationships for a resolved type.",
        ["find-overloads"] = "Returns overload signatures for a resolved method or constructor.",
        ["get-partial-declarations"] = "Returns the declarations for a partial type or method.",
        ["get-symbol-dependencies"] = "Returns the direct symbols used by a resolved symbol.",
        ["get-symbol-dependents"] = "Returns symbols that directly depend on a resolved symbol.",
        ["get-dependency-graph"] = "Returns a bounded dependency graph for the selected scope and granularity.",
        ["find-dependency-cycles"] = "Returns detected dependency cycles for the selected scope and granularity.",
        ["find-unused-symbols"] = "Returns candidate unused locals and members from compiler diagnostics.",
        ["find-duplicate-code"] = "Returns duplicate executable blocks that normalize to the same statement sequence.",
        ["get-diagnostics"] = "Returns compiler and analyzer diagnostics for a selected scope.",
        ["analyze-nullability"] = "Returns nullable-flow diagnostics for a selected scope or location.",
        ["analyze-async"] = "Returns supported async antipattern findings for a selected scope.",
        ["analyze-disposables"] = "Returns advisory findings for undisposed local disposable values.",
        ["analyze-control-flow"] = "Analyzes control flow for a selected executable region.",
        ["analyze-data-flow"] = "Analyzes data flow for a selected executable region.",
        ["get-operation-tree"] = "Returns a projected IOperation tree for a selected region.",
        ["get-control-flow-graph"] = "Returns a projected control-flow graph for a symbol or selected region.",
        ["get-change-impact"] = "Returns a bounded impact summary and supporting source locations for a symbol change.",
        ["get-api-surface"] = "Returns exported API symbols for a selected scope.",
        ["get-test-impact"] = "Returns likely impacted tests for a resolved symbol.",
        ["rename-symbol"] = "Stages a symbol rename across the effective solution.",
        ["sort-usings"] = "Stages an ordered set of using directives for one document.",
        ["format-document"] = "Stages Roslyn formatting for one document or one selected range.",
    };
}
