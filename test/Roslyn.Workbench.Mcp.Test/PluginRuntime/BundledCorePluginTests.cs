namespace Roslyn.Workbench.Mcp.Test.PluginRuntime;

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
        configuration.ServiceDefinitions.Should().HaveCount(3);
        configuration.ServiceDefinitions.Select(static service => service.ServiceType).Should().BeEquivalentTo(
        [
            typeof(IAnalyzerDiagnosticService),
            typeof(IBundledAsyncAnalyzerProvider),
            typeof(IAsyncAnalyzerDiagnosticService),
        ]);

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
            .BeEquivalentTo("rename-symbol", "format-document");

        result.ServiceProviderLifetime.Should().NotBeNull();
        result.ServiceProviderLifetime!.Dispose();
    }

    private static readonly IReadOnlyDictionary<string, string> ExpectedDescriptions = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["get-solution-structure"] = "Returns bounded solution folders, projects, target frameworks and direct project relationships.",
        ["get-project-details"] = "Returns project metadata, options and selected document details.",
        ["get-document-options"] = "Returns language and optional detailed parse and analyzer-config options for a document.",
        ["get-document-outline"] = "Returns a bounded semantic outline for one document.",
        ["get-code-context"] = "Returns a bounded code window with the enclosing semantic context for a selected location.",
        ["search-symbols"] = "Searches declarations by name, metadata name and optional semantic filters.",
        ["resolve-symbol"] = "Resolves the symbol at a location or selection and returns its canonical selector.",
        ["get-symbol-info"] = "Returns detailed metadata for a resolved symbol.",
        ["get-symbol-members"] = "Lists declared members and optional inherited or interface members for a resolved symbol.",
        ["get-symbol-attributes"] = "Returns declared and inherited attributes for a resolved symbol.",
        ["go-to-definition"] = "Finds source or metadata definitions for a resolved symbol.",
        ["find-references"] = "Finds source usages of a resolved symbol, with locations, containing symbols and access classification where available. Use this to locate usages or inspect writes; use find-callers when you need incoming calls grouped by calling symbol. Searches the selected scope, or the loaded solution when scope is omitted. Declarations are included by default and share the default 100-occurrence limit with references. Check references.hasMore before treating the results as complete.",
        ["find-callers"] = "Finds callers of a resolved symbol and groups source call sites by calling symbol. Use this to trace incoming calls; use find-references for general source usages or find-callees for calls made by a selected member. Searches the selected scope, or the loaded solution when scope is omitted. Defaults to 100 callers and 100 call sites per caller, limited independently. Check callers.hasMore and each caller's callSites.hasMore before treating the results as complete.",
        ["find-callees"] = "Returns symbols directly invoked by a method or selected executable body.",
        ["find-implementations"] = "Finds implementations of a resolved interface or member and returns symbol references. Use this to locate implementing types or members; use find-overrides for overrides of a virtual or abstract member, or find-references for source usages. Searches projects selected by scope, or the loaded solution when scope is omitted. Returns up to 100 implementations by default; check implementations.hasMore before treating the results as complete.",
        ["find-overrides"] = "Finds overrides of a virtual or abstract member.",
        ["find-derived-types"] = "Finds source types derived from a resolved named type, with their inheritance distance. Use this to explore descendants; use get-type-hierarchy for base types as well, or find-implementations for interface member implementations. For an interface, includes derived interfaces and implementing types. Searches projects selected by scope, or the loaded solution when scope is omitted. Defaults to three inheritance levels and 100 returned types. Check derivedTypes.hasMore for truncation within maxDepth; deeper types are excluded.",
        ["get-type-hierarchy"] = "Returns base, interface, and optional derived type relationships for a resolved type.",
        ["find-overloads"] = "Returns overload signatures for a resolved method or constructor.",
        ["get-partial-declarations"] = "Returns the declarations for a partial type or method.",
        ["get-symbol-dependencies"] = "Returns the direct symbols used by a resolved symbol.",
        ["get-symbol-dependents"] = "Returns symbols that directly depend on a resolved symbol.",
        ["get-dependency-graph"] = "Returns a bounded dependency graph for the selected scope and granularity.",
        ["find-dependency-cycles"] = "Returns detected dependency cycles for the selected scope and granularity.",
        ["find-unused-symbols"] = "Returns candidate unused locals and members from compiler diagnostics.",
        ["find-duplicate-code"] = "Returns bounded duplicate executable-block pointers that normalize to the same statement sequence.",
        ["get-diagnostics"] = "Returns compiler and analyzer diagnostics for a selected scope.",
        ["analyze-nullability"] = "Returns nullable-flow diagnostics for a selected scope or location.",
        ["analyze-async"] = "Returns bundled AsyncFixer diagnostics and compiler diagnostic CS4014 for a selected scope.",
        ["analyze-disposables"] = "Returns advisory findings for undisposed local disposable values.",
        ["analyze-control-flow"] = "Analyzes control flow for an exact complete statement or contiguous statement range.",
        ["analyze-data-flow"] = "Analyzes data flow for an exact expression, complete statement, or contiguous statement range.",
        ["get-operation-tree"] = "Returns a bounded IOperation tree with metadata and exact source pointers.",
        ["get-control-flow-graph"] = "Returns a bounded control-flow graph with operation metadata and exact source pointers.",
        ["get-change-impact"] = "Returns a bounded impact summary and supporting source locations for a symbol change.",
        ["get-api-surface"] = "Lists API declarations from source in the selected scope, with symbol references, accessibility and obsolete status. Use this to inventory accessible types and members; use search-symbols to locate declarations by name, or get-symbol-info to inspect one symbol. Omitted scope searches the loaded solution. Defaults to Public accessibility, includes obsolete symbols and returns up to 100 symbols. Check symbols.hasMore before treating the inventory as complete.",
        ["get-test-impact"] = "Returns likely impacted tests for a resolved symbol.",
        ["rename-symbol"] = "Stages a symbol rename across the effective solution.",
        ["format-document"] = "Stages Roslyn formatting for one document or one selected range.",
    };
}
