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
        ["get-project-details"] = "Inspects one selected project, returning target frameworks, compilation and parse options, project references, metadata references and analyzers. Use get-solution-structure to discover projects, then use this to inspect project configuration and dependencies. Document references are opt-in through includeDocuments and do not include file contents; use get-document-outline to inspect declarations in one document. Collections are bounded independently, including a default 200-document limit when requested. Check each returned collection for hasMore before treating it as complete.",
        ["get-document-options"] = "Inspects configuration for one selected document, returning language version and project-level nullable context. Use this to understand configuration behind diagnostic results; use get-diagnostics for findings or get-project-details for broader project configuration. Source nullable directives can override the reported project-level context. Detailed parse options and analyser configuration are omitted by default: request includeParseOptions for documentation mode and preprocessor symbols, or includeAnalyzerConfig for project configuration-file paths and syntax-tree analyser options.",
        ["get-document-outline"] = "Returns a semantic declaration tree for one document, with names, kinds, locations and nested children. Use this to navigate the structure of a known file; use search-symbols to discover declarations across documents, or get-symbol-info for detailed symbol metadata. Members are included by default. Defaults to 200 projected nodes below the document root and 16 hierarchy levels. Check truncated for content omitted by the node or depth limit; root may be null when syntax or semantic information is unavailable.",
        ["get-code-context"] = "Returns a bounded code window with the enclosing semantic context for a selected location.",
        ["search-symbols"] = "Finds source type and member declarations using a Roslyn name pattern and optional semantic filters. Use this to discover symbols by name; use resolve-symbol for a known source location, then get-symbol-info to inspect a chosen symbol. Searches the selected document or projects, or the loaded solution when scope is omitted. Filters narrow the candidate set. Returns symbol references, capped at 100 by default; check symbols.hasMore before treating the results as complete.",
        ["resolve-symbol"] = "Resolves the symbol at a source location or selection, returning a symbol reference, a reusable selector when available and bounded source declarations. Use this to turn a known location into a target for symbol inspection; use search-symbols when only a name is known, or get-symbol-info for detailed metadata. Echo the location snapshot in expectedSnapshot to reject stale locations. Returns up to 32 source declarations by default; check declarations.hasMore. Metadata-only symbols may have no source declarations.",
        ["get-symbol-info"] = "Inspects one resolved symbol, returning accessibility, modifiers, associated type and source declaration locations, plus method parameters and return type when applicable. Use this after search-symbols or resolve-symbol to understand a chosen declaration; use find-references for its usages or get-symbol-members to enumerate type members. XML documentation is opt-in through includeDocumentation. Defaults to 32 declarations and 64 method parameters, bounded independently; check declarations.hasMore and parameters.hasMore when present. Metadata-only symbols may have no source declarations.",
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
        ["get-partial-declarations"] = "Returns source declaration locations for a resolved symbol, ordered by document path and source position. Use this to navigate split partial type declarations; use get-symbol-info for symbol metadata or find-references for usages. Only declarations belonging to the resolved symbol are returned: partial method definition and implementation parts are not combined automatically. Metadata-only or implicitly declared symbols may return no declarations. Defaults to 32 locations; check declarations.hasMore before treating the results as complete.",
        ["get-symbol-dependencies"] = "Lists distinct direct dependencies found in a resolved symbol signature and supported executable operations, including referenced types and members. Use this for outgoing relationships; use get-symbol-dependents to find source symbols that reference the selected symbol, or get-dependency-graph for relationships across a scope. Dependencies may include metadata symbols. Containing assembly names are included by default. Returns up to 100 dependencies by default; check dependencies.hasMore before treating the inventory as complete.",
        ["get-symbol-dependents"] = "Finds distinct enclosing symbols at source references to a resolved symbol. Use this to identify incoming dependencies; use get-symbol-dependencies for outgoing dependencies, find-references for individual source occurrences, or get-change-impact for relationship counts. Searches the selected documents or projects, or the loaded solution when scope is omitted. Returns up to 100 dependent symbol references by default; check dependents.hasMore before treating the results as complete.",
        ["get-dependency-graph"] = "Builds a dependency graph at Project, Namespace, Type or Symbol granularity, with edges directed from dependents to dependencies. Use this to inspect relationships across a scope; use get-symbol-dependencies or get-symbol-dependents for one symbol, or get-change-impact for a symbol-centred count summary. Project granularity uses selected projects; other granularities use selected source documents. Omitted scope covers the loaded solution. Defaults to Type granularity, 200 nodes and 400 edges. Check both nodes.hasMore and edges.hasMore; omitted nodes also exclude their connected edges.",
        ["find-dependency-cycles"] = "Returns detected dependency cycles for the selected scope and granularity.",
        ["find-unused-symbols"] = "Uses only compiler diagnostics CS0168, CS0169 and CS0219 to identify candidate unused symbols. Use this to review potentially removable locals and private members; inspect find-references before removing code, or get-diagnostics for the underlying warnings. includeInternal widens the accessibility filter; generated-looking documents are excluded by default. Searches the selected scope, or the loaded solution when scope is omitted. Returns up to 50 candidates by default; check candidates.hasMore before treating the candidate list as complete.",
        ["find-duplicate-code"] = "Returns bounded duplicate executable-block pointers that normalize to the same statement sequence.",
        ["get-diagnostics"] = "Returns compiler diagnostics and diagnostics from analysers referenced by the selected projects. Use this for general diagnostic inspection; use analyze-async for the additional bundled AsyncFixer rule set, or get-document-options to inspect document configuration. Omitted scope searches the loaded solution. Document scope keeps diagnostics whose primary source location belongs to that document; broader scopes can include findings without source locations. Requested diagnostic IDs and severities filter results before the default 200-diagnostic limit. Check diagnostics.hasMore before treating the report as complete.",
        ["analyze-nullability"] = "Reports compiler diagnostics whose IDs begin with CS86 for focused nullable analysis. Use get-diagnostics for other compiler or project analyser findings, and get-document-options to inspect project-level nullable context. A supplied location overrides scope and keeps diagnostics intersecting its resolved span; echo its expectedSnapshot. Without a location, searches the selected scope or the loaded solution by default. Findings depend on compiler settings and are limited to 50 by default; check findings.hasMore.",
        ["analyze-async"] = "Reports only bundled AsyncFixer01 through AsyncFixer06 diagnostics plus compiler warning CS4014 for selected source documents. Use this for focused async/await review, including bundled rules when the project does not reference AsyncFixer; use get-diagnostics for general compiler and project analyser findings. Searches the selected scope, or the loaded solution when scope is omitted. Returns up to 50 findings by default; check findings.hasMore before treating the report as complete.",
        ["analyze-disposables"] = "Heuristically flags local declarations of IDisposable or IAsyncDisposable values that lack a recognised disposal pattern. Use this for advisory resource-handling review; verify ownership transfers and control flow before acting. Using declarations are skipped, and supported direct or finally-block Dispose/DisposeAsync calls are recognised. Analysis is limited to local declarations and recognised patterns; use get-diagnostics for configured compiler and analyser rules. Searches the selected scope, or the loaded solution when scope is omitted. Returns up to 50 findings by default; check findings.hasMore.",
        ["analyze-control-flow"] = "Analyses entry and exit reachability, exit points and return statements for an exact complete statement or contiguous sibling statements in one executable body. Use this to understand a selected region; use get-control-flow-graph for the enclosing executable graph, or analyze-data-flow for variable-flow information. The selection must match statement spans exactly; a caret or partial statement is rejected. Echo the current expectedSnapshot. Exit points and return statements are limited independently to 100 each by default; check exits.hasMore and returns.hasMore.",
        ["analyze-data-flow"] = "Returns Roslyn variable sets for declarations, reads, writes, captures and data flowing into or out of an exact expression, complete statement or contiguous sibling statements in one executable body. Use this when planning to extract or move selected code; use get-operation-tree for semantic operation structure, or analyze-control-flow for region reachability and exits. Echo the current expectedSnapshot and select exact syntax spans. Each variable category is limited independently to 50 symbols by default; check its hasMore before treating the category as complete.",
        ["get-operation-tree"] = "Projects a semantic IOperation tree with operation kinds, types, constant-value presence and source locations. Use this to inspect the semantic structure at a source location; use analyze-data-flow for variable-flow sets or get-control-flow-graph for branches and successors. If the selected syntax node has no operation, the first immediate child with an operation is used. Echo the current expectedSnapshot. Defaults to 200 total nodes and maxDepth 8, counting the root at depth zero. Check the result and node truncated flags; nodesLimit zero returns a null root.",
        ["get-control-flow-graph"] = "Builds a Roslyn control-flow graph for a source symbol or the executable body enclosing a source location, with basic blocks, reachability, successor ordinals, operations and flow regions. Use this to inspect branching across an executable body; use analyze-control-flow for reachability and exits of an exact statement region, or get-operation-tree for nested semantic operations. Defaults to 64 blocks, 32 regions and 32 operations per block, bounded independently. Check blocks.hasMore, regions.hasMore and each block operations.hasMore; successor ordinals may refer to blocks omitted by the block limit.",
        ["get-change-impact"] = "Summarises source references, method callers, overrides, implementing types for interfaces and a public-surface indicator for a resolved symbol. Use this as static relationship evidence when planning a symbol change; use get-symbol-dependents for the referencing symbols themselves, or get-dependency-graph for a scoped graph. Counts reflect the selected scope, or the loaded solution when scope is omitted. Supporting source-reference locations are limited to 100 by default, independently of the counts; check locations.hasMore before treating the returned locations as complete.",
        ["get-api-surface"] = "Lists API declarations from source in the selected scope, with symbol references, accessibility and obsolete status. Use this to inventory accessible types and members; use search-symbols to locate declarations by name, or get-symbol-info to inspect one symbol. Omitted scope searches the loaded solution. Defaults to Public accessibility, includes obsolete symbols and returns up to 100 symbols. Check symbols.hasMore before treating the inventory as complete.",
        ["get-test-impact"] = "Returns likely impacted tests for a resolved symbol.",
        ["rename-symbol"] = "Stages a symbol rename across the effective solution.",
        ["format-document"] = "Stages Roslyn formatting for one document or one selected range.",
    };
}
