using System.Text.Json;
using System.Text.Json.Nodes;

using Roslyn.Workbench.Mcp.Contracts.CodeActions;
using Roslyn.Workbench.Mcp.TestSupport;

namespace Roslyn.Workbench.Mcp.Plugins.Core.Test;

public sealed class InspectionToolsTests
{
    private static readonly JsonSerializerOptions _serializerOptions = new(JsonSerializerDefaults.Web);

    [Fact]
    public void GIVEN_BundledCorePlugin_WHEN_RegisteringTools_THEN_ShouldPublishStage4InspectionSurface()
    {
        var plugin = new BundledCorePlugin();
        var registry = new PluginRegistry(plugin.Metadata);

        plugin.Register(registry);

        registry.RegisteredTools.Select(static tool => tool.Metadata.Name).Should().Contain(
        [
            "get-solution-structure",
            "get-project-details",
            "get-document-options",
            "get-document-outline",
            "search-symbols",
            "resolve-symbol",
            "get-symbol-info",
            "go-to-definition",
            "find-references",
            "find-callers",
            "find-implementations",
            "get-diagnostics",
            "list-code-actions",
            "describe-code-action",
            "stage-code-action",
            "stage-code-fix",
            "stage-fix-all",
        ]);
    }

    [Fact]
    public void GIVEN_BundledCorePlugin_WHEN_RegisteringTools_THEN_ShouldPublishBatch1ToolSurface()
    {
        var plugin = new BundledCorePlugin();
        var registry = new PluginRegistry(plugin.Metadata);

        plugin.Register(registry);

        registry.RegisteredTools.Select(static tool => tool.Metadata.Name).Should().Contain(
        [
            "get-symbol-members",
            "get-symbol-attributes",
            "find-derived-types",
            "get-type-hierarchy",
            "find-overloads",
            "get-partial-declarations",
            "analyze-control-flow",
            "analyze-data-flow",
            "get-operation-tree",
            "get-control-flow-graph",
            "rename-symbol",
            "sort-usings",
            "format-document",
        ]);

        registry.RegisteredTools.Select(static tool => tool.Metadata.Name).Should().Contain(BuiltInCodeActionAuditCases.VisibleDedicatedToolNames);
    }

    [Fact]
    public void GIVEN_BundledCorePlugin_WHEN_RegisteringTools_THEN_ShouldHideDeferredCodeActionFamilies()
    {
        var plugin = new BundledCorePlugin();
        var registry = new PluginRegistry(plugin.Metadata);

        plugin.Register(registry);

        BuiltInCodeActionAuditCases.HiddenDedicatedToolNames.Should().BeEmpty();
    }

    [Fact]
    public void GIVEN_BundledCorePlugin_WHEN_RegisteringConvertAutoPropertyTool_THEN_ShouldUseDedicatedRequestContract()
    {
        var plugin = new BundledCorePlugin();
        var registry = new PluginRegistry(plugin.Metadata);

        plugin.Register(registry);

        var tool = registry.RegisteredTools.Single(static registeredTool => registeredTool.Metadata.Name == "convert-auto-property-to-full-property");

        tool.RequestType.Should().Be(typeof(ConvertAutoPropertyToFullPropertyRequest));
    }

    [Fact]
    public void GIVEN_BundledCorePlugin_WHEN_RegisteringRemainingContractNarrowedTools_THEN_ShouldPublishLiveToolSurface()
    {
        var plugin = new BundledCorePlugin();
        var registry = new PluginRegistry(plugin.Metadata);

        plugin.Register(registry);

        registry.RegisteredTools.Select(static tool => tool.Metadata.Name).Should().Contain(["move-type-to-file", "convert-property"]);
    }

    [Fact]
    public void GIVEN_BundledCorePlugin_WHEN_RegisteringTools_THEN_ShouldExposePendingPromotionToolSurface()
    {
        var plugin = new BundledCorePlugin();
        var registry = new PluginRegistry(plugin.Metadata);

        plugin.Register(registry);

        registry.RegisteredTools.Select(static tool => tool.Metadata.Name).Should().Contain(
            BuiltInCodeActionAuditCases.PendingPromotionCandidates
                .Select(static auditCase => auditCase.ToolName)
                .Where(static toolName => !string.IsNullOrWhiteSpace(toolName)));
    }

    [Fact]
    public void GIVEN_BundledCorePlugin_WHEN_RegisteringTools_THEN_ShouldPublishPoint2QuerySurface()
    {
        var plugin = new BundledCorePlugin();
        var registry = new PluginRegistry(plugin.Metadata);

        plugin.Register(registry);

        registry.RegisteredTools.Select(static tool => tool.Metadata.Name).Should().Contain(
        [
            "get-code-metrics",
            "get-code-context",
            "find-callees",
            "find-overrides",
            "get-symbol-dependencies",
            "get-symbol-dependents",
            "get-dependency-graph",
            "find-dependency-cycles",
            "find-duplicate-code",
            "get-change-impact",
            "get-api-surface",
            "get-test-impact",
            "find-unused-symbols",
            "analyze-nullability",
            "analyze-async",
            "analyze-disposables",
        ]);
    }

    [Fact]
    public async Task GIVEN_InspectionWorkspace_WHEN_ExecutingStructuralAndSemanticTools_THEN_ShouldReturnProjectedRoslynData()
    {
        using var fixture = await InspectionSampleFixture.CreateAsync();
        var coordinator = WorkspaceCoordinatorFactory.Create(new WorkspaceCoordinatorOptions
        {
            DefaultMaxResults = 100,
            MaxConcurrentQueries = 2,
            MaxResponseBytes = 65536,
        }, toolExecutionServices: BundledCoreToolExecutionServicesFactory.Create());
        var openResult = await coordinator.OpenAsync(new WorkspaceOpenRequest
        {
            Path = fixture.ProjectPath,
        }, CancellationToken.None);
        var plugin = new BundledCorePlugin();
        var registry = new PluginRegistry(plugin.Metadata);
        plugin.Register(registry);
        var executor = new ToolExecutor(coordinator);
        var resolveLocation = fixture.GetLocation("GreetingFormatter");

        openResult.Outcome.Should().Be(ToolOutcome.Succeeded);

        var solutionStructure = await ExecuteAsync<SolutionStructureData>(executor, registry, "get-solution-structure", new Dictionary<string, JsonElement>());
        var projectDetails = await ExecuteAsync<ProjectDetailsData>(executor, registry, "get-project-details", new Dictionary<string, JsonElement>
        {
            ["project"] = JsonSerializer.SerializeToElement(new ProjectSelector
            {
                Path = "Sample.csproj",
            }),
        });
        var documentOptions = await ExecuteAsync<DocumentOptionsData>(executor, registry, "get-document-options", new Dictionary<string, JsonElement>
        {
            ["document"] = JsonSerializer.SerializeToElement(new DocumentSelector
            {
                Path = "Formatting.cs",
            }),
        });
        var searchSymbols = await ExecuteAsync<SymbolSearchData>(executor, registry, "search-symbols", new Dictionary<string, JsonElement>
        {
            ["query"] = JsonSerializer.SerializeToElement("Greeting"),
        });
        var resolveSymbol = await ExecuteAsync<ResolveSymbolData>(executor, registry, "resolve-symbol", new Dictionary<string, JsonElement>
        {
            ["location"] = JsonSerializer.SerializeToElement(resolveLocation),
            ["expectedSnapshot"] = JsonSerializer.SerializeToElement(new SnapshotPrecondition
            {
                WorkspaceEpoch = openResult.WorkspaceEpoch!.Value,
            }),
        });
        var symbolInfo = await ExecuteAsync<SymbolInfoData>(executor, registry, "get-symbol-info", new Dictionary<string, JsonElement>
        {
            ["symbol"] = JsonSerializer.SerializeToElement(resolveSymbol.Data!.Selector),
            ["includeDocumentation"] = JsonSerializer.SerializeToElement(true),
            ["expectedSnapshot"] = JsonSerializer.SerializeToElement(new SnapshotPrecondition
            {
                WorkspaceEpoch = openResult.WorkspaceEpoch!.Value,
            }),
        });
        var definition = await ExecuteAsync<DefinitionData>(executor, registry, "go-to-definition", new Dictionary<string, JsonElement>
        {
            ["symbol"] = JsonSerializer.SerializeToElement(resolveSymbol.Data!.Selector),
            ["expectedSnapshot"] = JsonSerializer.SerializeToElement(new SnapshotPrecondition
            {
                WorkspaceEpoch = openResult.WorkspaceEpoch!.Value,
            }),
        });
        var references = await ExecuteAsync<ReferenceSearchData>(executor, registry, "find-references", new Dictionary<string, JsonElement>
        {
            ["symbol"] = JsonSerializer.SerializeToElement(resolveSymbol.Data!.Selector),
            ["expectedSnapshot"] = JsonSerializer.SerializeToElement(new SnapshotPrecondition
            {
                WorkspaceEpoch = openResult.WorkspaceEpoch!.Value,
            }),
        });
        var callers = await ExecuteAsync<CallerSearchData>(executor, registry, "find-callers", new Dictionary<string, JsonElement>
        {
            ["symbol"] = JsonSerializer.SerializeToElement(new SymbolSelector
            {
                DocumentationCommentId = "M:Sample.GreetingFormatter.Format(System.String)",
            }),
        });
        var implementations = await ExecuteAsync<ImplementationSearchData>(executor, registry, "find-implementations", new Dictionary<string, JsonElement>
        {
            ["symbol"] = JsonSerializer.SerializeToElement(new SymbolSelector
            {
                DocumentationCommentId = "T:Sample.IMessageFormatter",
            }),
        });
        var diagnostics = await ExecuteAsync<DiagnosticsData>(executor, registry, "get-diagnostics", new Dictionary<string, JsonElement>());
        var outline = await ExecuteAsync<DocumentOutlineData>(executor, registry, "get-document-outline", new Dictionary<string, JsonElement>
        {
            ["document"] = JsonSerializer.SerializeToElement(new DocumentSelector
            {
                Path = "Formatting.cs",
            }),
        });

        solutionStructure.Data!.Projects.Should().ContainSingle(static project => project.Name == "Sample");
        projectDetails.Data!.Project!.Name.Should().Be("Sample");
        documentOptions.Data!.LanguageVersion.Should().NotBeNullOrWhiteSpace();
        documentOptions.Data.AnalyzerConfig!.EditorConfigPaths.Should().Contain(static path => path.EndsWith(".editorconfig", StringComparison.Ordinal));
        documentOptions.Data.AnalyzerConfig.Options.Should().ContainKey("build_property.targetframework");
        searchSymbols.Data!.Symbols.Should().Contain(static symbol => symbol.DisplayName.Contains("GreetingFormatter", StringComparison.Ordinal));
        resolveSymbol.Data!.Symbol!.DisplayName.Should().Contain("GreetingFormatter");
        symbolInfo.Data!.Documentation.Should().NotBeNull();
        definition.Data!.Definitions.Should().NotBeEmpty();
        references.Data!.References.Should().NotBeEmpty();
        callers.Data!.Callers.Should().Contain(static caller => caller.Caller!.DisplayName.Contains("Call", StringComparison.Ordinal));
        implementations.Data!.Implementations.Should().Contain(static symbol => symbol.DisplayName.Contains("GreetingFormatter", StringComparison.Ordinal));
        diagnostics.Data!.Diagnostics.Should().Contain(static diagnostic => diagnostic.Id == "CS0219");
        EnumerateOutline(outline.Data!.Root!).Should().Contain(static node => node.Name == "GreetingFormatter");
    }

    [Fact]
    public async Task GIVEN_InspectionWorkspace_WHEN_ExecutingBatch1QueryTools_THEN_ShouldReturnRoslynDrivenData()
    {
        using var fixture = await InspectionSampleFixture.CreateAsync();
        var coordinator = WorkspaceCoordinatorFactory.Create(new WorkspaceCoordinatorOptions
        {
            DefaultMaxResults = 100,
            MaxConcurrentQueries = 2,
            MaxResponseBytes = 65536,
        }, toolExecutionServices: BundledCoreToolExecutionServicesFactory.Create());
        var openResult = await coordinator.OpenAsync(new WorkspaceOpenRequest
        {
            Path = fixture.ProjectPath,
        }, CancellationToken.None);
        var plugin = new BundledCorePlugin();
        var registry = new PluginRegistry(plugin.Metadata);
        var executor = new ToolExecutor(coordinator);

        plugin.Register(registry);
        openResult.Outcome.Should().Be(ToolOutcome.Succeeded);

        var symbolMembers = await ExecuteAsync<SymbolMembersData>(executor, registry, "get-symbol-members", new Dictionary<string, JsonElement>
        {
            ["symbol"] = JsonSerializer.SerializeToElement(new SymbolSelector
            {
                DocumentationCommentId = "T:Sample.GreetingFormatter",
            }),
            ["includeInherited"] = JsonSerializer.SerializeToElement(true),
        });
        var symbolAttributes = await ExecuteAsync<SymbolAttributesData>(executor, registry, "get-symbol-attributes", new Dictionary<string, JsonElement>
        {
            ["symbol"] = JsonSerializer.SerializeToElement(new SymbolSelector
            {
                DocumentationCommentId = "T:Sample.GreetingFormatter",
            }),
            ["includeInherited"] = JsonSerializer.SerializeToElement(true),
        });
        var derivedTypes = await ExecuteAsync<DerivedTypesData>(executor, registry, "find-derived-types", new Dictionary<string, JsonElement>
        {
            ["symbol"] = JsonSerializer.SerializeToElement(new SymbolSelector
            {
                DocumentationCommentId = "T:Sample.FormatterBase",
            }),
        });
        var typeHierarchy = await ExecuteAsync<TypeHierarchyData>(executor, registry, "get-type-hierarchy", new Dictionary<string, JsonElement>
        {
            ["symbol"] = JsonSerializer.SerializeToElement(new SymbolSelector
            {
                DocumentationCommentId = "T:Sample.GreetingFormatter",
            }),
            ["includeDerived"] = JsonSerializer.SerializeToElement(true),
        });
        var overloads = await ExecuteAsync<OverloadSearchData>(executor, registry, "find-overloads", new Dictionary<string, JsonElement>
        {
            ["symbol"] = JsonSerializer.SerializeToElement(new SymbolSelector
            {
                DocumentationCommentId = "M:Sample.GreetingFormatter.Format(System.String)",
            }),
        });
        var partialDeclarations = await ExecuteAsync<PartialDeclarationsData>(executor, registry, "get-partial-declarations", new Dictionary<string, JsonElement>
        {
            ["symbol"] = JsonSerializer.SerializeToElement(new SymbolSelector
            {
                DocumentationCommentId = "T:Sample.PartialFormatter",
            }),
        });
        var controlFlow = await ExecuteAsync<ControlFlowAnalysisData>(executor, registry, "analyze-control-flow", new Dictionary<string, JsonElement>
        {
            ["location"] = JsonSerializer.SerializeToElement(fixture.GetLocation("if (trimmed.Length == 0)")),
            ["expectedSnapshot"] = JsonSerializer.SerializeToElement(new SnapshotPrecondition
            {
                WorkspaceEpoch = openResult.WorkspaceEpoch!.Value,
            }),
        });
        var dataFlow = await ExecuteAsync<DataFlowAnalysisData>(executor, registry, "analyze-data-flow", new Dictionary<string, JsonElement>
        {
            ["location"] = JsonSerializer.SerializeToElement(fixture.GetLocation("var upper = trimmed.ToUpperInvariant();")),
            ["expectedSnapshot"] = JsonSerializer.SerializeToElement(new SnapshotPrecondition
            {
                WorkspaceEpoch = openResult.WorkspaceEpoch!.Value,
            }),
        });
        var operationTree = await ExecuteAsync<OperationTreeData>(executor, registry, "get-operation-tree", new Dictionary<string, JsonElement>
        {
            ["location"] = JsonSerializer.SerializeToElement(fixture.GetLocation("formatter.Format(\"hi\")")),
            ["expectedSnapshot"] = JsonSerializer.SerializeToElement(new SnapshotPrecondition
            {
                WorkspaceEpoch = openResult.WorkspaceEpoch!.Value,
            }),
        });
        var controlFlowGraph = await ExecuteAsync<ControlFlowGraphData>(executor, registry, "get-control-flow-graph", new Dictionary<string, JsonElement>
        {
            ["symbol"] = JsonSerializer.SerializeToElement(new SymbolSelector
            {
                DocumentationCommentId = "M:Sample.FlowSamples.Analyse(System.String)",
            }),
        });

        symbolMembers.Data!.Members.Should().Contain(static symbol => symbol.DisplayName.Contains("Decorate", StringComparison.Ordinal));
        symbolMembers.Data.Members.Should().Contain(static symbol => symbol.DisplayName.Contains("Prefix", StringComparison.Ordinal));
        symbolAttributes.Data!.Attributes.Should().Contain(static attribute => attribute.Name.Contains("Serializable", StringComparison.Ordinal));
        symbolAttributes.Data.Attributes.Should().Contain(static attribute => attribute.Name.Contains("Obsolete", StringComparison.Ordinal));
        derivedTypes.Data!.DerivedTypes.Should().Contain(static node => node.Type!.DisplayName.Contains("DerivedGreetingFormatter", StringComparison.Ordinal));
        typeHierarchy.Data!.BaseTypes.Should().Contain(static symbol => symbol.DisplayName.Contains("FormatterBase", StringComparison.Ordinal));
        typeHierarchy.Data.Interfaces.Should().Contain(static symbol => symbol.DisplayName.Contains("IMessageFormatter", StringComparison.Ordinal));
        typeHierarchy.Data.DerivedTypes.Should().Contain(static node => node.Type!.DisplayName.Contains("DerivedGreetingFormatter", StringComparison.Ordinal));
        overloads.Data!.Overloads.Should().HaveCount(2);
        partialDeclarations.Data!.Declarations.Should().HaveCount(2);
        controlFlow.Data!.Exits.Should().NotBeEmpty();
        dataFlow.Data!.DataFlowsOut.Should().Contain(static symbol => symbol.DisplayName.Contains("upper", StringComparison.Ordinal));
        operationTree.Data!.Root!.Kind.Should().Contain("Invocation");
        controlFlowGraph.Data!.Blocks.Should().NotBeEmpty();
    }

    [Fact]
    public async Task GIVEN_InspectionWorkspace_WHEN_ExecutingPoint2QueryTools_THEN_ShouldReturnProjectedContextAndRelationships()
    {
        using var fixture = await InspectionSampleFixture.CreateAsync();
        var coordinator = WorkspaceCoordinatorFactory.Create(new WorkspaceCoordinatorOptions
        {
            DefaultMaxResults = 100,
            MaxConcurrentQueries = 2,
            MaxResponseBytes = 65536,
        }, toolExecutionServices: BundledCoreToolExecutionServicesFactory.Create());
        var openResult = await coordinator.OpenAsync(new WorkspaceOpenRequest
        {
            Path = fixture.ProjectPath,
        }, CancellationToken.None);
        var plugin = new BundledCorePlugin();
        var registry = new PluginRegistry(plugin.Metadata);
        var executor = new ToolExecutor(coordinator);

        plugin.Register(registry);
        openResult.Outcome.Should().Be(ToolOutcome.Succeeded);

        var codeContext = await ExecuteAsync<JsonElement>(executor, registry, "get-code-context", new Dictionary<string, JsonElement>
        {
            ["location"] = JsonSerializer.SerializeToElement(fixture.GetLocation("var unused = 42;")),
            ["includeDiagnostics"] = JsonSerializer.SerializeToElement(true),
            ["includeEnclosingSymbols"] = JsonSerializer.SerializeToElement(true),
            ["expectedSnapshot"] = JsonSerializer.SerializeToElement(new SnapshotPrecondition
            {
                WorkspaceEpoch = openResult.WorkspaceEpoch!.Value,
            }),
        });
        var callees = await ExecuteAsync<JsonElement>(executor, registry, "find-callees", new Dictionary<string, JsonElement>
        {
            ["symbol"] = JsonSerializer.SerializeToElement(new SymbolSelector
            {
                DocumentationCommentId = "M:Sample.FormatterCaller.Call",
            }),
        });
        var overrides = await ExecuteAsync<JsonElement>(executor, registry, "find-overrides", new Dictionary<string, JsonElement>
        {
            ["symbol"] = JsonSerializer.SerializeToElement(new SymbolSelector
            {
                DocumentationCommentId = "M:Sample.FormatterBase.Decorate(System.String)",
            }),
        });
        var branchOnlyControlFlowGraph = await ExecuteAsync<ControlFlowGraphData>(executor, registry, "get-control-flow-graph", new Dictionary<string, JsonElement>
        {
            ["symbol"] = JsonSerializer.SerializeToElement(new SymbolSelector
            {
                DocumentationCommentId = "M:Sample.FlowSamples.Analyse(System.String)",
            }),
        });
        var exceptionalControlFlowGraph = await ExecuteAsync<ControlFlowGraphData>(executor, registry, "get-control-flow-graph", new Dictionary<string, JsonElement>
        {
            ["symbol"] = JsonSerializer.SerializeToElement(new SymbolSelector
            {
                DocumentationCommentId = "M:Sample.FlowSamples.AnalyseExceptional(System.String)",
            }),
        });

        codeContext.Data!.GetProperty("text").GetString().Should().Contain("var unused = 42;");
        codeContext.Data.GetProperty("diagnostics").EnumerateArray().Select(static diagnostic => diagnostic.GetProperty("id").GetString()).Should().Contain("CS0219");
        codeContext.Data.GetProperty("enclosingSymbols").EnumerateArray().Select(static symbol => symbol.GetProperty("displayName").GetString()).Should().Contain(static displayName => displayName!.Contains("GreetingFormatter.Format", StringComparison.Ordinal));

        callees.Data!.GetProperty("callees").EnumerateArray().Select(static callee => callee.GetProperty("displayName").GetString()).Should().Contain(static displayName => displayName!.Contains("GreetingFormatter.Format", StringComparison.Ordinal));
        callees.Data.GetProperty("callees").EnumerateArray().Select(static callee => callee.GetProperty("displayName").GetString()).Should().Contain(static displayName => displayName!.Contains("GreetingFormatter.GreetingFormatter", StringComparison.Ordinal) || displayName!.Contains(".ctor", StringComparison.Ordinal));

        overrides.Data!.GetProperty("overrides").EnumerateArray().Select(static symbol => symbol.GetProperty("displayName").GetString()).Should().Contain(static displayName => displayName!.Contains("GreetingFormatter.Decorate", StringComparison.Ordinal));
        overrides.Data.GetProperty("overrides").EnumerateArray().Select(static symbol => symbol.GetProperty("displayName").GetString()).Should().Contain(static displayName => displayName!.Contains("DerivedGreetingFormatter.Decorate", StringComparison.Ordinal));

        branchOnlyControlFlowGraph.Data!.Regions.Should().NotBeEmpty();
        branchOnlyControlFlowGraph.Data.Regions.Select(static region => region.Kind).Should().Contain("Root");
        exceptionalControlFlowGraph.Data!.Regions.Select(static region => region.Kind).Should().Contain(static kind => kind.Contains("Try", StringComparison.Ordinal) || kind.Contains("Catch", StringComparison.Ordinal) || kind.Contains("Finally", StringComparison.Ordinal));
    }

    [Fact]
    public async Task GIVEN_InspectionWorkspace_WHEN_ExecutingDependencyAndImpactQueryTools_THEN_ShouldReturnProjectedRelationships()
    {
        using var fixture = await InspectionSampleFixture.CreateAsync();
        var coordinator = WorkspaceCoordinatorFactory.Create(new WorkspaceCoordinatorOptions
        {
            DefaultMaxResults = 100,
            MaxConcurrentQueries = 2,
            MaxResponseBytes = 65536,
        }, toolExecutionServices: BundledCoreToolExecutionServicesFactory.Create());
        var openResult = await coordinator.OpenAsync(new WorkspaceOpenRequest
        {
            Path = fixture.ProjectPath,
        }, CancellationToken.None);
        var plugin = new BundledCorePlugin();
        var registry = new PluginRegistry(plugin.Metadata);
        var executor = new ToolExecutor(coordinator);

        plugin.Register(registry);
        openResult.Outcome.Should().Be(ToolOutcome.Succeeded);

        var dependencies = await ExecuteAsync<JsonElement>(executor, registry, "get-symbol-dependencies", new Dictionary<string, JsonElement>
        {
            ["symbol"] = JsonSerializer.SerializeToElement(new SymbolSelector
            {
                DocumentationCommentId = "M:Sample.GreetingFormatter.Format(System.String)",
            }),
        });
        var dependents = await ExecuteAsync<JsonElement>(executor, registry, "get-symbol-dependents", new Dictionary<string, JsonElement>
        {
            ["symbol"] = JsonSerializer.SerializeToElement(new SymbolSelector
            {
                DocumentationCommentId = "M:Sample.GreetingFormatter.Format(System.String)",
            }),
        });
        var changeImpact = await ExecuteAsync<JsonElement>(executor, registry, "get-change-impact", new Dictionary<string, JsonElement>
        {
            ["symbol"] = JsonSerializer.SerializeToElement(new SymbolSelector
            {
                DocumentationCommentId = "M:Sample.GreetingFormatter.Format(System.String)",
            }),
        });
        var apiSurface = await ExecuteAsync<JsonElement>(executor, registry, "get-api-surface", new Dictionary<string, JsonElement>
        {
            ["scope"] = JsonSerializer.SerializeToElement(new ScopeSelector
            {
                Kind = ScopeKind.Project,
                Project = new ProjectSelector
                {
                    Path = "Sample.csproj",
                },
            }),
        });

        dependencies.Data!.GetProperty("dependencies").EnumerateArray().Select(static dependency => dependency.GetProperty("symbol").GetProperty("displayName").GetString()).Should().Contain(static displayName => displayName!.Contains("ToUpperInvariant", StringComparison.Ordinal));
        dependencies.Data.GetProperty("dependencies").EnumerateArray().Select(static dependency => dependency.GetProperty("symbol").GetProperty("displayName").GetString()).Should().Contain(static displayName => displayName!.Contains("Decorate", StringComparison.Ordinal));

        dependents.Data!.GetProperty("dependents").EnumerateArray().Select(static dependent => dependent.GetProperty("displayName").GetString()).Should().Contain(static displayName => displayName!.Contains("FormatterCaller.Call", StringComparison.Ordinal));
        dependents.Data.GetProperty("dependents").EnumerateArray().Select(static dependent => dependent.GetProperty("displayName").GetString()).Should().Contain(static displayName => displayName!.Contains("GreetingFormatter.Format", StringComparison.Ordinal) && displayName!.Contains("bool", StringComparison.Ordinal));

        changeImpact.Data!.GetProperty("impact").GetProperty("referenceCount").GetInt32().Should().BeGreaterThan(0);
        changeImpact.Data.GetProperty("impact").GetProperty("callerCount").GetInt32().Should().BeGreaterThan(0);
        changeImpact.Data.GetProperty("locations").EnumerateArray().Select(static location => location.GetProperty("context").GetString()).Should().Contain(static context => context!.Contains("formatter.Format(\"hi\")", StringComparison.Ordinal));

        apiSurface.Data!.GetProperty("symbols").EnumerateArray().Select(static symbol => symbol.GetProperty("symbol").GetProperty("displayName").GetString()).Should().Contain(static displayName => displayName!.Contains("GreetingFormatter", StringComparison.Ordinal));
        apiSurface.Data.GetProperty("symbols").EnumerateArray().Select(static symbol => symbol.GetProperty("symbol").GetProperty("displayName").GetString()).Should().Contain(static displayName => displayName!.Contains("IMessageFormatter", StringComparison.Ordinal));
    }

    [Fact]
    public async Task GIVEN_InspectionWorkspace_WHEN_ExecutingGraphAndTestImpactQueryTools_THEN_ShouldReturnProjectedGraphCyclesAndImpactedTests()
    {
        using var fixture = await InspectionSampleFixture.CreateAsync();
        var coordinator = WorkspaceCoordinatorFactory.Create(new WorkspaceCoordinatorOptions
        {
            DefaultMaxResults = 100,
            MaxConcurrentQueries = 2,
            MaxResponseBytes = 65536,
        }, toolExecutionServices: BundledCoreToolExecutionServicesFactory.Create());
        var openResult = await coordinator.OpenAsync(new WorkspaceOpenRequest
        {
            Path = fixture.ProjectPath,
        }, CancellationToken.None);
        var plugin = new BundledCorePlugin();
        var registry = new PluginRegistry(plugin.Metadata);
        var executor = new ToolExecutor(coordinator);

        plugin.Register(registry);
        openResult.Outcome.Should().Be(ToolOutcome.Succeeded);

        var dependencyGraph = await ExecuteAsync<JsonElement>(executor, registry, "get-dependency-graph", new Dictionary<string, JsonElement>
        {
            ["scope"] = JsonSerializer.SerializeToElement(new ScopeSelector
            {
                Kind = ScopeKind.Project,
                Project = new ProjectSelector
                {
                    Path = "Sample.csproj",
                },
            }),
            ["granularity"] = JsonSerializer.SerializeToElement("Type"),
            ["maxDepth"] = JsonSerializer.SerializeToElement(2),
        });
        var dependencyCycles = await ExecuteAsync<JsonElement>(executor, registry, "find-dependency-cycles", new Dictionary<string, JsonElement>
        {
            ["scope"] = JsonSerializer.SerializeToElement(new ScopeSelector
            {
                Kind = ScopeKind.Project,
                Project = new ProjectSelector
                {
                    Path = "Sample.csproj",
                },
            }),
            ["granularity"] = JsonSerializer.SerializeToElement("Type"),
        });
        var testImpact = await ExecuteAsync<JsonElement>(executor, registry, "get-test-impact", new Dictionary<string, JsonElement>
        {
            ["symbol"] = JsonSerializer.SerializeToElement(new SymbolSelector
            {
                DocumentationCommentId = "M:Sample.FormatterCaller.Call",
            }),
        });

        dependencyGraph.Data!.GetProperty("nodes").EnumerateArray().Select(static node => node.GetProperty("displayName").GetString()).Should().Contain(static displayName => displayName!.Contains("FormatterCaller", StringComparison.Ordinal));
        dependencyGraph.Data.GetProperty("nodes").EnumerateArray().Select(static node => node.GetProperty("displayName").GetString()).Should().Contain(static displayName => displayName!.Contains("GreetingFormatter", StringComparison.Ordinal));
        dependencyGraph.Data.GetProperty("edges").EnumerateArray().Select(static edge => $"{edge.GetProperty("fromDisplayName").GetString()}->{edge.GetProperty("toDisplayName").GetString()}").Should().Contain(static edge => edge.Contains("FormatterCaller", StringComparison.Ordinal) && edge.Contains("GreetingFormatter", StringComparison.Ordinal));

        dependencyCycles.Data!.GetProperty("cycles").EnumerateArray().Select(static cycle => cycle.GetProperty("nodes").EnumerateArray().Select(static node => node.GetProperty("displayName").GetString()).ToArray()).Should().Contain(static cycleNodes =>
            cycleNodes.Any(static displayName => displayName!.Contains("AlphaCycle", StringComparison.Ordinal))
            && cycleNodes.Any(static displayName => displayName!.Contains("BetaCycle", StringComparison.Ordinal)));

        testImpact.Data!.GetProperty("tests").EnumerateArray().Select(static test => test.GetProperty("test").GetProperty("displayName").GetString()).Should().Contain(static displayName => displayName!.Contains("GIVEN_FormatterCaller_WHEN_CallingCall_THEN_ShouldReturnFormattedGreeting", StringComparison.Ordinal));
    }

    [Fact]
    public async Task GIVEN_InspectionWorkspace_WHEN_ExecutingRemainingAnalysisQueryTools_THEN_ShouldReturnProjectedFindings()
    {
        using var fixture = await InspectionSampleFixture.CreateAsync();
        var coordinator = WorkspaceCoordinatorFactory.Create(new WorkspaceCoordinatorOptions
        {
            DefaultMaxResults = 100,
            MaxConcurrentQueries = 2,
            MaxResponseBytes = 65536,
        }, toolExecutionServices: BundledCoreToolExecutionServicesFactory.Create());
        var openResult = await coordinator.OpenAsync(new WorkspaceOpenRequest
        {
            Path = fixture.ProjectPath,
        }, CancellationToken.None);
        var plugin = new BundledCorePlugin();
        var registry = new PluginRegistry(plugin.Metadata);
        var executor = new ToolExecutor(coordinator);

        plugin.Register(registry);
        openResult.Outcome.Should().Be(ToolOutcome.Succeeded);

        var unusedSymbols = await ExecuteAsync<JsonElement>(executor, registry, "find-unused-symbols", new Dictionary<string, JsonElement>
        {
            ["scope"] = JsonSerializer.SerializeToElement(new ScopeSelector
            {
                Kind = ScopeKind.Document,
                Document = new DocumentSelector
                {
                    Path = "RemoveUnusedVariable.cs",
                },
            }),
        });
        var nullability = await ExecuteAsync<JsonElement>(executor, registry, "analyze-nullability", new Dictionary<string, JsonElement>
        {
            ["scope"] = JsonSerializer.SerializeToElement(new ScopeSelector
            {
                Kind = ScopeKind.Document,
                Document = new DocumentSelector
                {
                    Path = "EnableNullable.cs",
                },
            }),
        });
        var asyncAnalysis = await ExecuteAsync<JsonElement>(executor, registry, "analyze-async", new Dictionary<string, JsonElement>
        {
            ["scope"] = JsonSerializer.SerializeToElement(new ScopeSelector
            {
                Kind = ScopeKind.Document,
                Document = new DocumentSelector
                {
                    Path = "Formatting.cs",
                },
            }),
        });
        var disposableAnalysis = await ExecuteAsync<JsonElement>(executor, registry, "analyze-disposables", new Dictionary<string, JsonElement>
        {
            ["scope"] = JsonSerializer.SerializeToElement(new ScopeSelector
            {
                Kind = ScopeKind.Document,
                Document = new DocumentSelector
                {
                    Path = "Formatting.cs",
                },
            }),
        });

        unusedSymbols.Data!.GetProperty("candidates").EnumerateArray().Select(static candidate => candidate.GetProperty("symbol").GetProperty("displayName").GetString()).Should().Contain(static displayName => displayName!.Contains("unused", StringComparison.Ordinal));
        unusedSymbols.Data.GetProperty("candidates").EnumerateArray().Select(static candidate => candidate.GetProperty("reasons").EnumerateArray().Select(static reason => reason.GetString()).ToArray()).Should().Contain(static reasons =>
            reasons.Any(static reason => reason!.Contains("CS0219", StringComparison.Ordinal)));

        nullability.Data!.GetProperty("findings").EnumerateArray().Select(static finding => finding.GetProperty("diagnostic").GetProperty("id").GetString()).Should().Contain("CS8602");
        nullability.Data.GetProperty("findings").EnumerateArray().Select(static finding => finding.GetProperty("diagnostic").GetProperty("message").GetString()).Should().Contain(static message => message!.Contains("possibly null", StringComparison.OrdinalIgnoreCase));

        asyncAnalysis.Data!.GetProperty("findings").EnumerateArray().Select(static finding => finding.GetProperty("kind").GetString()).Should().Contain("AsyncWithoutAwait");
        asyncAnalysis.Data.GetProperty("findings").EnumerateArray().Select(static finding => finding.GetProperty("kind").GetString()).Should().Contain("UnawaitedTask");

        disposableAnalysis.Data!.GetProperty("findings").EnumerateArray().Select(static finding => finding.GetProperty("kind").GetString()).Should().Contain("UndisposedLocal");
        disposableAnalysis.Data.GetProperty("findings").EnumerateArray().Select(static finding => finding.GetProperty("type").GetProperty("displayName").GetString()).Should().Contain(static displayName => displayName!.Contains("MemoryStream", StringComparison.Ordinal));
    }

    [Fact]
    public async Task GIVEN_InspectionWorkspace_WHEN_ExecutingMetricsAndDuplicateQueryTools_THEN_ShouldReturnProjectedMetricsAndMatches()
    {
        using var fixture = await InspectionSampleFixture.CreateAsync();
        var coordinator = WorkspaceCoordinatorFactory.Create(new WorkspaceCoordinatorOptions
        {
            DefaultMaxResults = 100,
            MaxConcurrentQueries = 2,
            MaxResponseBytes = 65536,
        }, toolExecutionServices: BundledCoreToolExecutionServicesFactory.Create());
        var openResult = await coordinator.OpenAsync(new WorkspaceOpenRequest
        {
            Path = fixture.ProjectPath,
        }, CancellationToken.None);
        var plugin = new BundledCorePlugin();
        var registry = new PluginRegistry(plugin.Metadata);
        var executor = new ToolExecutor(coordinator);

        plugin.Register(registry);
        openResult.Outcome.Should().Be(ToolOutcome.Succeeded);

        var typeMetrics = await ExecuteAsync<JsonElement>(executor, registry, "get-code-metrics", new Dictionary<string, JsonElement>
        {
            ["symbol"] = JsonSerializer.SerializeToElement(new SymbolSelector
            {
                DocumentationCommentId = "T:Sample.GreetingFormatter",
            }),
            ["includeChildren"] = JsonSerializer.SerializeToElement(true),
        });
        var metrics = await ExecuteAsync<JsonElement>(executor, registry, "get-code-metrics", new Dictionary<string, JsonElement>
        {
            ["scope"] = JsonSerializer.SerializeToElement(new ScopeSelector
            {
                Kind = ScopeKind.Document,
                Document = new DocumentSelector
                {
                    Path = "Formatting.cs",
                },
            }),
            ["symbol"] = JsonSerializer.SerializeToElement(new SymbolSelector
            {
                DocumentationCommentId = "M:Sample.ConditionalSamples.DescribeValue(System.Int32)",
            }),
        });
        var duplicateCode = await ExecuteAsync<JsonElement>(executor, registry, "find-duplicate-code", new Dictionary<string, JsonElement>
        {
            ["scope"] = JsonSerializer.SerializeToElement(new ScopeSelector
            {
                Kind = ScopeKind.Document,
                Document = new DocumentSelector
                {
                    Path = "Formatting.cs",
                },
            }),
            ["minimumStatements"] = JsonSerializer.SerializeToElement(3),
        });

        typeMetrics.Data!.GetProperty("metrics").EnumerateArray().Select(static metric => metric.GetProperty("symbol").GetProperty("displayName").GetString()).Should().Contain(static displayName => displayName!.Contains("GreetingFormatter.Format", StringComparison.Ordinal));
        metrics.Data!.GetProperty("metrics").EnumerateArray().Select(static metric => metric.GetProperty("symbol").GetProperty("displayName").GetString()).Should().Contain(static displayName => displayName!.Contains("ConditionalSamples.DescribeValue", StringComparison.Ordinal));
        metrics.Data.GetProperty("metrics").EnumerateArray().Select(static metric => metric.GetProperty("cyclomaticComplexity").GetInt32()).Should().Contain(static complexity => complexity >= 3);
        metrics.Data.GetProperty("metrics").EnumerateArray().Select(static metric => metric.GetProperty("logicalLines").GetInt32()).Should().Contain(static logicalLines => logicalLines >= 5);

        duplicateCode.Data!.GetProperty("groups").EnumerateArray().Select(static group => group.GetProperty("occurrences").EnumerateArray().Select(static occurrence => occurrence.GetProperty("symbol").GetProperty("displayName").GetString()).ToArray()).Should().Contain(static displays =>
            displays.Any(static display => display!.Contains("DuplicateCodeSamples.ComputeOne", StringComparison.Ordinal))
            && displays.Any(static display => display!.Contains("DuplicateCodeSamples.ComputeTwo", StringComparison.Ordinal)));
        duplicateCode.Data.GetProperty("groups").EnumerateArray().Select(static group => group.GetProperty("statementCount").GetInt32()).Should().Contain(static statementCount => statementCount >= 3);
    }

    [Fact]
    public async Task GIVEN_InvalidDuplicateCodeThreshold_WHEN_ExecutingDuplicateCodeTool_THEN_ShouldRejectInvalidRequest()
    {
        using var fixture = await InspectionSampleFixture.CreateAsync();
        var coordinator = WorkspaceCoordinatorFactory.Create(new WorkspaceCoordinatorOptions
        {
            DefaultMaxResults = 100,
            MaxConcurrentQueries = 2,
            MaxResponseBytes = 65536,
        }, toolExecutionServices: BundledCoreToolExecutionServicesFactory.Create());
        var openResult = await coordinator.OpenAsync(new WorkspaceOpenRequest
        {
            Path = fixture.ProjectPath,
        }, CancellationToken.None);
        var plugin = new BundledCorePlugin();
        var registry = new PluginRegistry(plugin.Metadata);
        var executor = new ToolExecutor(coordinator);

        plugin.Register(registry);
        openResult.Outcome.Should().Be(ToolOutcome.Succeeded);

        var result = await ExecuteAsync<DuplicateCodeData>(executor, registry, "find-duplicate-code", new Dictionary<string, JsonElement>
        {
            ["minimumStatements"] = JsonSerializer.SerializeToElement(0),
        }, expectProtocolSuccess: false);

        result.Outcome.Should().Be(ToolOutcome.Rejected);
        result.Error!.Code.Should().Be("InvalidRequest");
    }

    [Fact]
    public async Task GIVEN_ActiveTransaction_WHEN_ExecutingBatch1MutationTools_THEN_ShouldStageStructuredMutations()
    {
        using var fixture = await InspectionSampleFixture.CreateAsync();
        var coordinator = WorkspaceCoordinatorFactory.Create(new WorkspaceCoordinatorOptions
        {
            DefaultMaxResults = 100,
            MaxConcurrentQueries = 2,
            MaxResponseBytes = 65536,
        }, toolExecutionServices: BundledCoreToolExecutionServicesFactory.Create());
        var openResult = await coordinator.OpenAsync(new WorkspaceOpenRequest
        {
            Path = fixture.ProjectPath,
        }, CancellationToken.None);
        var plugin = new BundledCorePlugin();
        var registry = new PluginRegistry(plugin.Metadata);
        var executor = new ToolExecutor(coordinator);

        plugin.Register(registry);
        openResult.Outcome.Should().Be(ToolOutcome.Succeeded);

        var startResult = await coordinator.StartTransactionAsync(new TransactionStartRequest(), CancellationToken.None);
        startResult.Outcome.Should().Be(ToolOutcome.Succeeded);

        var renameResult = await ExecuteAsync<MutationData>(executor, registry, "rename-symbol", new Dictionary<string, JsonElement>
        {
            ["symbol"] = JsonSerializer.SerializeToElement(new SymbolSelector
            {
                DocumentationCommentId = "T:Sample.StateHolder",
            }),
            ["newName"] = JsonSerializer.SerializeToElement("SessionState"),
            ["expectedSnapshot"] = JsonSerializer.SerializeToElement(new SnapshotPrecondition
            {
                WorkspaceEpoch = openResult.WorkspaceEpoch!.Value,
                TransactionRevision = startResult.Data!.Transaction!.Revision,
            }),
        });
        var sortUsingsResult = await ExecuteAsync<MutationData>(executor, registry, "sort-usings", new Dictionary<string, JsonElement>
        {
            ["document"] = JsonSerializer.SerializeToElement(new DocumentSelector
            {
                Path = "Usings.cs",
            }),
            ["expectedSnapshot"] = JsonSerializer.SerializeToElement(new SnapshotPrecondition
            {
                WorkspaceEpoch = openResult.WorkspaceEpoch!.Value,
                TransactionRevision = renameResult.Data!.Transaction!.Revision,
            }),
        });
        var formatDocumentResult = await ExecuteAsync<MutationData>(executor, registry, "format-document", new Dictionary<string, JsonElement>
        {
            ["document"] = JsonSerializer.SerializeToElement(new DocumentSelector
            {
                Path = "Usings.cs",
            }),
            ["expectedSnapshot"] = JsonSerializer.SerializeToElement(new SnapshotPrecondition
            {
                WorkspaceEpoch = openResult.WorkspaceEpoch!.Value,
                TransactionRevision = sortUsingsResult.Data!.Transaction!.Revision,
            }),
        });

        renameResult.Data!.Operation.Should().Be("rename-symbol");
        renameResult.Data.Transaction!.Revision.Should().Be(1);
        sortUsingsResult.Data!.Operation.Should().Be("sort-usings");
        sortUsingsResult.Data.Transaction!.Revision.Should().Be(2);
        formatDocumentResult.Data!.Operation.Should().Be("format-document");
        formatDocumentResult.Data.Transaction!.Revision.Should().Be(3);
    }

    [Fact]
    public async Task GIVEN_ActiveTransactionAndBuiltInCodeActions_WHEN_RemovingUnusedUsings_THEN_ShouldStageStructuredMutation()
    {
        using var fixture = await InspectionSampleFixture.CreateAsync();
        var runtime = CodeActionRuntimeFactory.Create(new CodeActionRuntimeOptions
        {
            IncludeBuiltInAssemblies = true,
        });
        var coordinator = WorkspaceCoordinatorFactory.Create(new WorkspaceCoordinatorOptions
        {
            DefaultMaxResults = 100,
            MaxConcurrentQueries = 2,
            MaxResponseBytes = 65536,
        }, codeActionRuntime: runtime, toolExecutionServices: BundledCoreToolExecutionServicesFactory.Create());
        var openResult = await coordinator.OpenAsync(new WorkspaceOpenRequest
        {
            Path = fixture.ProjectPath,
        }, CancellationToken.None);
        await coordinator.StartTransactionAsync(new TransactionStartRequest(), CancellationToken.None);
        var plugin = new BundledCorePlugin();
        var registry = new PluginRegistry(plugin.Metadata);
        var executor = new ToolExecutor(coordinator);

        plugin.Register(registry);

        var result = await ExecuteAsync<MutationData>(executor, registry, "remove-unused-usings", new Dictionary<string, JsonElement>
        {
            ["scope"] = JsonSerializer.SerializeToElement(new ScopeSelector
            {
                Kind = ScopeKind.Document,
                Document = new DocumentSelector
                {
                    Path = "Usings.cs",
                },
            }),
            ["expectedSnapshot"] = JsonSerializer.SerializeToElement(new SnapshotPrecondition
            {
                WorkspaceEpoch = openResult.WorkspaceEpoch!.Value,
                TransactionRevision = 0,
            }),
        });
        var preview = await coordinator.PreviewTransactionAsync(new TransactionPreviewRequest(), CancellationToken.None);

        result.Outcome.Should().Be(ToolOutcome.Succeeded);
        result.TransactionRevision.Should().Be(1);
        result.Data!.Operation.Should().Be("remove-unused-usings");
        preview.Data!.Documents.Should().ContainSingle(static change => change.Document!.Path == "Usings.cs");
        var documentPreview = preview.Data.Documents.Single(static change => change.Document!.Path == "Usings.cs").Preview!;
        (documentPreview.AddedLines + documentPreview.RemovedLines + documentPreview.ChangedLines).Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task GIVEN_ActiveTransactionAndBuiltInCodeActions_WHEN_AddingMissingUsings_THEN_ShouldStageStructuredMutation()
    {
        using var fixture = await InspectionSampleFixture.CreateAsync();
        var runtime = CodeActionRuntimeFactory.Create(new CodeActionRuntimeOptions
        {
            IncludeBuiltInAssemblies = true,
        });
        var coordinator = WorkspaceCoordinatorFactory.Create(new WorkspaceCoordinatorOptions
        {
            DefaultMaxResults = 100,
            MaxConcurrentQueries = 2,
            MaxResponseBytes = 65536,
        }, codeActionRuntime: runtime, toolExecutionServices: BundledCoreToolExecutionServicesFactory.Create());
        var openResult = await coordinator.OpenAsync(new WorkspaceOpenRequest
        {
            Path = fixture.ProjectPath,
        }, CancellationToken.None);
        await coordinator.StartTransactionAsync(new TransactionStartRequest(), CancellationToken.None);
        var plugin = new BundledCorePlugin();
        var registry = new PluginRegistry(plugin.Metadata);
        var executor = new ToolExecutor(coordinator);

        plugin.Register(registry);

        var result = await ExecuteAsync<MutationData>(executor, registry, "add-missing-usings", new Dictionary<string, JsonElement>
        {
            ["scope"] = JsonSerializer.SerializeToElement(new ScopeSelector
            {
                Kind = ScopeKind.Document,
                Document = new DocumentSelector
                {
                    Path = "MissingUsings.cs",
                },
            }),
            ["expectedSnapshot"] = JsonSerializer.SerializeToElement(new SnapshotPrecondition
            {
                WorkspaceEpoch = openResult.WorkspaceEpoch!.Value,
                TransactionRevision = 0,
            }),
        });
        var preview = await coordinator.PreviewTransactionAsync(new TransactionPreviewRequest(), CancellationToken.None);

        result.Outcome.Should().Be(ToolOutcome.Succeeded);
        result.TransactionRevision.Should().Be(1);
        result.Data!.Operation.Should().Be("add-missing-usings");
        preview.Data!.Documents.Should().ContainSingle(static change => change.Document!.Path == "MissingUsings.cs");
        var documentPreview = preview.Data.Documents.Single(static change => change.Document!.Path == "MissingUsings.cs").Preview!;
        (documentPreview.AddedLines + documentPreview.RemovedLines + documentPreview.ChangedLines).Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task GIVEN_ActiveTransactionAndBuiltInCodeActions_WHEN_InliningVariable_THEN_ShouldStageStructuredMutation()
    {
        using var fixture = await InspectionSampleFixture.CreateAsync();
        var runtime = CodeActionRuntimeFactory.Create(new CodeActionRuntimeOptions
        {
            IncludeBuiltInAssemblies = true,
        });
        var coordinator = WorkspaceCoordinatorFactory.Create(new WorkspaceCoordinatorOptions
        {
            DefaultMaxResults = 100,
            MaxConcurrentQueries = 2,
            MaxResponseBytes = 65536,
        }, codeActionRuntime: runtime, toolExecutionServices: BundledCoreToolExecutionServicesFactory.Create());
        var openResult = await coordinator.OpenAsync(new WorkspaceOpenRequest
        {
            Path = fixture.ProjectPath,
        }, CancellationToken.None);
        await coordinator.StartTransactionAsync(new TransactionStartRequest(), CancellationToken.None);
        var plugin = new BundledCorePlugin();
        var registry = new PluginRegistry(plugin.Metadata);
        var executor = new ToolExecutor(coordinator);

        plugin.Register(registry);

        var result = await ExecuteAsync<MutationData>(executor, registry, "inline-variable", new Dictionary<string, JsonElement>
        {
            ["symbol"] = JsonSerializer.SerializeToElement(new SymbolSelector
            {
                Location = fixture.GetLocation("formatted"),
            }),
            ["expectedSnapshot"] = JsonSerializer.SerializeToElement(new SnapshotPrecondition
            {
                WorkspaceEpoch = openResult.WorkspaceEpoch!.Value,
                TransactionRevision = 0,
            }),
        });
        var preview = await coordinator.PreviewTransactionAsync(new TransactionPreviewRequest(), CancellationToken.None);

        result.Outcome.Should().Be(ToolOutcome.Succeeded);
        result.TransactionRevision.Should().Be(1);
        result.Data!.Operation.Should().Be("inline-variable");
        preview.Data!.Documents.Should().ContainSingle(static change => change.Document!.Path == "Formatting.cs");
        var documentPreview = preview.Data.Documents.Single(static change => change.Document!.Path == "Formatting.cs").Preview!;
        (documentPreview.AddedLines + documentPreview.RemovedLines + documentPreview.ChangedLines).Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task GIVEN_ActiveTransactionAndBuiltInCodeActions_WHEN_ConvertingToInterpolatedString_THEN_ShouldStageStructuredMutation()
    {
        using var fixture = await InspectionSampleFixture.CreateAsync();
        var runtime = CodeActionRuntimeFactory.Create(new CodeActionRuntimeOptions
        {
            IncludeBuiltInAssemblies = true,
        });
        var coordinator = WorkspaceCoordinatorFactory.Create(new WorkspaceCoordinatorOptions
        {
            DefaultMaxResults = 100,
            MaxConcurrentQueries = 2,
            MaxResponseBytes = 65536,
        }, codeActionRuntime: runtime, toolExecutionServices: BundledCoreToolExecutionServicesFactory.Create());
        var openResult = await coordinator.OpenAsync(new WorkspaceOpenRequest
        {
            Path = fixture.ProjectPath,
        }, CancellationToken.None);
        await coordinator.StartTransactionAsync(new TransactionStartRequest(), CancellationToken.None);
        var plugin = new BundledCorePlugin();
        var registry = new PluginRegistry(plugin.Metadata);
        var executor = new ToolExecutor(coordinator);

        plugin.Register(registry);

        var result = await ExecuteAsync<MutationData>(executor, registry, "convert-to-interpolated-string", new Dictionary<string, JsonElement>
        {
            ["selection"] = JsonSerializer.SerializeToElement(fixture.GetLocation("formatted + \"!\"")),
            ["expectedSnapshot"] = JsonSerializer.SerializeToElement(new SnapshotPrecondition
            {
                WorkspaceEpoch = openResult.WorkspaceEpoch!.Value,
                TransactionRevision = 0,
            }),
        });
        var preview = await coordinator.PreviewTransactionAsync(new TransactionPreviewRequest(), CancellationToken.None);

        result.Outcome.Should().Be(ToolOutcome.Succeeded);
        result.TransactionRevision.Should().Be(1);
        result.Data!.Operation.Should().Be("convert-to-interpolated-string");
        preview.Data!.Documents.Should().ContainSingle(static change => change.Document!.Path == "Formatting.cs");
        var documentPreview = preview.Data.Documents.Single(static change => change.Document!.Path == "Formatting.cs").Preview!;
        (documentPreview.AddedLines + documentPreview.RemovedLines + documentPreview.ChangedLines).Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task GIVEN_ActiveTransactionAndBuiltInCodeActions_WHEN_ExtractingMethod_THEN_ShouldStageStructuredMutation()
    {
        using var fixture = await InspectionSampleFixture.CreateAsync();
        var runtime = CodeActionRuntimeFactory.Create(new CodeActionRuntimeOptions
        {
            IncludeBuiltInAssemblies = true,
        });
        var coordinator = WorkspaceCoordinatorFactory.Create(new WorkspaceCoordinatorOptions
        {
            DefaultMaxResults = 100,
            MaxConcurrentQueries = 2,
            MaxResponseBytes = 65536,
        }, codeActionRuntime: runtime, toolExecutionServices: BundledCoreToolExecutionServicesFactory.Create());
        var openResult = await coordinator.OpenAsync(new WorkspaceOpenRequest
        {
            Path = fixture.ProjectPath,
        }, CancellationToken.None);
        await coordinator.StartTransactionAsync(new TransactionStartRequest(), CancellationToken.None);
        var plugin = new BundledCorePlugin();
        var registry = new PluginRegistry(plugin.Metadata);
        var executor = new ToolExecutor(coordinator);

        plugin.Register(registry);

        var result = await ExecuteAsync<MutationData>(executor, registry, "extract-method", new Dictionary<string, JsonElement>
        {
            ["selection"] = JsonSerializer.SerializeToElement(fixture.GetSpanSelection("var upper = value.ToUpperInvariant();", "return Decorate(upper);")),
            ["targetKind"] = JsonSerializer.SerializeToElement(ExtractMethodTargetKind.Method),
            ["expectedSnapshot"] = JsonSerializer.SerializeToElement(new SnapshotPrecondition
            {
                WorkspaceEpoch = openResult.WorkspaceEpoch!.Value,
                TransactionRevision = 0,
            }),
        });
        var preview = await coordinator.PreviewTransactionAsync(new TransactionPreviewRequest(), CancellationToken.None);

        result.Outcome.Should().Be(ToolOutcome.Succeeded);
        result.TransactionRevision.Should().Be(1);
        result.Data!.Operation.Should().Be("extract-method");
        preview.Data!.Documents.Should().ContainSingle(static change => change.Document!.Path == "Formatting.cs");
        var documentPreview = preview.Data.Documents.Single(static change => change.Document!.Path == "Formatting.cs").Preview!;
        (documentPreview.AddedLines + documentPreview.RemovedLines + documentPreview.ChangedLines).Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task GIVEN_ActiveTransactionAndBuiltInCodeActions_WHEN_IntroducingParameter_THEN_ShouldStageStructuredMutation()
    {
        using var fixture = await InspectionSampleFixture.CreateAsync();
        var runtime = CodeActionRuntimeFactory.Create(new CodeActionRuntimeOptions
        {
            IncludeBuiltInAssemblies = true,
        });
        var coordinator = WorkspaceCoordinatorFactory.Create(new WorkspaceCoordinatorOptions
        {
            DefaultMaxResults = 100,
            MaxConcurrentQueries = 2,
            MaxResponseBytes = 65536,
        }, codeActionRuntime: runtime, toolExecutionServices: BundledCoreToolExecutionServicesFactory.Create());
        var openResult = await coordinator.OpenAsync(new WorkspaceOpenRequest
        {
            Path = fixture.ProjectPath,
        }, CancellationToken.None);
        await coordinator.StartTransactionAsync(new TransactionStartRequest(), CancellationToken.None);
        var plugin = new BundledCorePlugin();
        var registry = new PluginRegistry(plugin.Metadata);
        var executor = new ToolExecutor(coordinator);

        plugin.Register(registry);

        var result = await ExecuteAsync<MutationData>(executor, registry, "introduce-parameter", new Dictionary<string, JsonElement>
        {
            ["selection"] = JsonSerializer.SerializeToElement(fixture.GetSelection("value.Length")),
            ["strategy"] = JsonSerializer.SerializeToElement(IntroduceParameterStrategy.UpdateCallSitesDirectly),
            ["expectedSnapshot"] = JsonSerializer.SerializeToElement(new SnapshotPrecondition
            {
                WorkspaceEpoch = openResult.WorkspaceEpoch!.Value,
                TransactionRevision = 0,
            }),
        });
        var preview = await coordinator.PreviewTransactionAsync(new TransactionPreviewRequest(), CancellationToken.None);

        result.Outcome.Should().Be(ToolOutcome.Succeeded);
        result.TransactionRevision.Should().Be(1);
        result.Data!.Operation.Should().Be("introduce-parameter");
        preview.Data!.Documents.Should().ContainSingle(static change => change.Document!.Path == "Formatting.cs");
        var documentPreview = preview.Data.Documents.Single(static change => change.Document!.Path == "Formatting.cs").Preview!;
        (documentPreview.AddedLines + documentPreview.RemovedLines + documentPreview.ChangedLines).Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task GIVEN_ActiveTransactionAndBuiltInCodeActions_WHEN_EncapsulatingField_THEN_ShouldStageStructuredMutation()
    {
        using var fixture = await InspectionSampleFixture.CreateAsync();
        var runtime = CodeActionRuntimeFactory.Create(new CodeActionRuntimeOptions
        {
            IncludeBuiltInAssemblies = true,
        });
        var coordinator = WorkspaceCoordinatorFactory.Create(new WorkspaceCoordinatorOptions
        {
            DefaultMaxResults = 100,
            MaxConcurrentQueries = 2,
            MaxResponseBytes = 65536,
        }, codeActionRuntime: runtime, toolExecutionServices: BundledCoreToolExecutionServicesFactory.Create());
        var openResult = await coordinator.OpenAsync(new WorkspaceOpenRequest
        {
            Path = fixture.ProjectPath,
        }, CancellationToken.None);
        await coordinator.StartTransactionAsync(new TransactionStartRequest(), CancellationToken.None);
        var plugin = new BundledCorePlugin();
        var registry = new PluginRegistry(plugin.Metadata);
        var executor = new ToolExecutor(coordinator);

        plugin.Register(registry);

        var result = await ExecuteAsync<MutationData>(executor, registry, "encapsulate-field", new Dictionary<string, JsonElement>
        {
            ["field"] = JsonSerializer.SerializeToElement(new SymbolSelector
            {
                Location = fixture.GetLocation("_backingField"),
            }),
            ["expectedSnapshot"] = JsonSerializer.SerializeToElement(new SnapshotPrecondition
            {
                WorkspaceEpoch = openResult.WorkspaceEpoch!.Value,
                TransactionRevision = 0,
            }),
        });
        var preview = await coordinator.PreviewTransactionAsync(new TransactionPreviewRequest(), CancellationToken.None);

        result.Outcome.Should().Be(ToolOutcome.Succeeded);
        result.TransactionRevision.Should().Be(1);
        result.Data!.Operation.Should().Be("encapsulate-field");
        preview.Data!.Documents.Should().ContainSingle(static change => change.Document!.Path == "Formatting.cs");
        var documentPreview = preview.Data.Documents.Single(static change => change.Document!.Path == "Formatting.cs").Preview!;
        (documentPreview.AddedLines + documentPreview.RemovedLines + documentPreview.ChangedLines).Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task GIVEN_ActiveTransactionAndBuiltInCodeActions_WHEN_ConvertingForeachToLinq_THEN_ShouldStageStructuredMutation()
    {
        using var fixture = await InspectionSampleFixture.CreateAsync();
        var runtime = CodeActionRuntimeFactory.Create(new CodeActionRuntimeOptions
        {
            IncludeBuiltInAssemblies = true,
        });
        var coordinator = WorkspaceCoordinatorFactory.Create(new WorkspaceCoordinatorOptions
        {
            DefaultMaxResults = 100,
            MaxConcurrentQueries = 2,
            MaxResponseBytes = 65536,
        }, codeActionRuntime: runtime, toolExecutionServices: BundledCoreToolExecutionServicesFactory.Create());
        var openResult = await coordinator.OpenAsync(new WorkspaceOpenRequest
        {
            Path = fixture.ProjectPath,
        }, CancellationToken.None);
        await coordinator.StartTransactionAsync(new TransactionStartRequest(), CancellationToken.None);
        var plugin = new BundledCorePlugin();
        var registry = new PluginRegistry(plugin.Metadata);
        var executor = new ToolExecutor(coordinator);

        plugin.Register(registry);

        var result = await ExecuteAsync<MutationData>(executor, registry, "convert-foreach-linq", new Dictionary<string, JsonElement>
        {
            ["selection"] = JsonSerializer.SerializeToElement(fixture.GetLocation("foreach (var number in numbers)")),
            ["conversionKind"] = JsonSerializer.SerializeToElement(ConvertForeachLinqKind.ForeachToQuery),
            ["expectedSnapshot"] = JsonSerializer.SerializeToElement(new SnapshotPrecondition
            {
                WorkspaceEpoch = openResult.WorkspaceEpoch!.Value,
                TransactionRevision = 0,
            }),
        });
        var preview = await coordinator.PreviewTransactionAsync(new TransactionPreviewRequest(), CancellationToken.None);

        result.Outcome.Should().Be(ToolOutcome.Succeeded);
        result.TransactionRevision.Should().Be(1);
        result.Data!.Operation.Should().Be("convert-foreach-linq");
        preview.Data!.Documents.Should().ContainSingle(static change => change.Document!.Path == "Formatting.cs");
        var documentPreview = preview.Data.Documents.Single(static change => change.Document!.Path == "Formatting.cs").Preview!;
        (documentPreview.AddedLines + documentPreview.RemovedLines + documentPreview.ChangedLines).Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task GIVEN_ActiveTransactionAndBuiltInCodeActions_WHEN_IntroducingVariable_THEN_ShouldStageStructuredMutation()
    {
        using var fixture = await InspectionSampleFixture.CreateAsync();
        var runtime = CodeActionRuntimeFactory.Create(new CodeActionRuntimeOptions
        {
            IncludeBuiltInAssemblies = true,
        });
        var coordinator = WorkspaceCoordinatorFactory.Create(new WorkspaceCoordinatorOptions
        {
            DefaultMaxResults = 100,
            MaxConcurrentQueries = 2,
            MaxResponseBytes = 65536,
        }, codeActionRuntime: runtime, toolExecutionServices: BundledCoreToolExecutionServicesFactory.Create());
        var openResult = await coordinator.OpenAsync(new WorkspaceOpenRequest
        {
            Path = fixture.ProjectPath,
        }, CancellationToken.None);
        await coordinator.StartTransactionAsync(new TransactionStartRequest(), CancellationToken.None);
        var plugin = new BundledCorePlugin();
        var registry = new PluginRegistry(plugin.Metadata);
        var executor = new ToolExecutor(coordinator);

        plugin.Register(registry);

        var result = await ExecuteAsync<MutationData>(executor, registry, "introduce-variable", new Dictionary<string, JsonElement>
        {
            ["selection"] = JsonSerializer.SerializeToElement(fixture.GetLocation("1 + 1")),
            ["kind"] = JsonSerializer.SerializeToElement(IntroduceVariableKind.LocalConstant),
            ["expectedSnapshot"] = JsonSerializer.SerializeToElement(new SnapshotPrecondition
            {
                WorkspaceEpoch = openResult.WorkspaceEpoch!.Value,
                TransactionRevision = 0,
            }),
        });
        var preview = await coordinator.PreviewTransactionAsync(new TransactionPreviewRequest(), CancellationToken.None);

        result.Outcome.Should().Be(ToolOutcome.Succeeded);
        result.TransactionRevision.Should().Be(1);
        result.Data!.Operation.Should().Be("introduce-variable");
        preview.Data!.Documents.Should().ContainSingle(static change => change.Document!.Path == "Formatting.cs");
        var documentPreview = preview.Data.Documents.Single(static change => change.Document!.Path == "Formatting.cs").Preview!;
        (documentPreview.AddedLines + documentPreview.RemovedLines + documentPreview.ChangedLines).Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task GIVEN_AmbiguousSelection_WHEN_ConvertingToInterpolatedString_THEN_ShouldRejectWithAmbiguousLocation()
    {
        using var fixture = await InspectionSampleFixture.CreateAsync();
        var runtime = CodeActionRuntimeFactory.Create(new CodeActionRuntimeOptions
        {
            IncludeBuiltInAssemblies = true,
        });
        var coordinator = WorkspaceCoordinatorFactory.Create(new WorkspaceCoordinatorOptions
        {
            DefaultMaxResults = 100,
            MaxConcurrentQueries = 2,
            MaxResponseBytes = 65536,
        }, codeActionRuntime: runtime, toolExecutionServices: BundledCoreToolExecutionServicesFactory.Create());
        var openResult = await coordinator.OpenAsync(new WorkspaceOpenRequest
        {
            Path = fixture.ProjectPath,
        }, CancellationToken.None);
        await coordinator.StartTransactionAsync(new TransactionStartRequest(), CancellationToken.None);
        var plugin = new BundledCorePlugin();
        var registry = new PluginRegistry(plugin.Metadata);
        var executor = new ToolExecutor(coordinator);

        plugin.Register(registry);

        var result = await ExecuteAsync<MutationData>(executor, registry, "convert-to-interpolated-string", new Dictionary<string, JsonElement>
        {
            ["selection"] = JsonSerializer.SerializeToElement(new LocationSelector
            {
                Selection = new TextSelectionSelector
                {
                    Document = new DocumentSelector
                    {
                        Path = "Formatting.cs",
                    },
                    SelectedText = "Format",
                },
            }),
            ["expectedSnapshot"] = JsonSerializer.SerializeToElement(new SnapshotPrecondition
            {
                WorkspaceEpoch = openResult.WorkspaceEpoch!.Value,
                TransactionRevision = 0,
            }),
        }, expectProtocolSuccess: false);

        result.Outcome.Should().Be(ToolOutcome.Rejected);
        result.Error!.Code.Should().Be("LocationAmbiguous");
    }

    [Fact]
    public async Task GIVEN_RemoveDeclarationFalse_WHEN_InliningVariable_THEN_ShouldRejectUnsupportedOption()
    {
        using var fixture = await InspectionSampleFixture.CreateAsync();
        var runtime = CodeActionRuntimeFactory.Create(new CodeActionRuntimeOptions
        {
            IncludeBuiltInAssemblies = true,
        });
        var coordinator = WorkspaceCoordinatorFactory.Create(new WorkspaceCoordinatorOptions
        {
            DefaultMaxResults = 100,
            MaxConcurrentQueries = 2,
            MaxResponseBytes = 65536,
        }, codeActionRuntime: runtime, toolExecutionServices: BundledCoreToolExecutionServicesFactory.Create());
        var openResult = await coordinator.OpenAsync(new WorkspaceOpenRequest
        {
            Path = fixture.ProjectPath,
        }, CancellationToken.None);
        await coordinator.StartTransactionAsync(new TransactionStartRequest(), CancellationToken.None);
        var plugin = new BundledCorePlugin();
        var registry = new PluginRegistry(plugin.Metadata);
        var executor = new ToolExecutor(coordinator);

        plugin.Register(registry);

        var result = await ExecuteAsync<MutationData>(executor, registry, "inline-variable", new Dictionary<string, JsonElement>
        {
            ["symbol"] = JsonSerializer.SerializeToElement(new SymbolSelector
            {
                Location = fixture.GetLocation("formatted"),
            }),
            ["removeDeclaration"] = JsonSerializer.SerializeToElement(false),
            ["expectedSnapshot"] = JsonSerializer.SerializeToElement(new SnapshotPrecondition
            {
                WorkspaceEpoch = openResult.WorkspaceEpoch!.Value,
                TransactionRevision = 0,
            }),
        }, expectProtocolSuccess: false);

        result.Outcome.Should().Be(ToolOutcome.Rejected);
        result.Error!.Code.Should().Be("UnsupportedOption");
    }

    [Fact]
    public async Task GIVEN_UnloadedWorkspace_WHEN_ExecutingStage4InspectionTool_THEN_ShouldRejectWithWorkspaceNotOpen()
    {
        var plugin = new BundledCorePlugin();
        var registry = new PluginRegistry(plugin.Metadata);
        var coordinator = WorkspaceCoordinatorFactory.Create(new WorkspaceCoordinatorOptions
        {
            DefaultMaxResults = 100,
            MaxConcurrentQueries = 2,
            MaxResponseBytes = 65536,
        }, toolExecutionServices: BundledCoreToolExecutionServicesFactory.Create());
        var executor = new ToolExecutor(coordinator);

        plugin.Register(registry);

        var result = await ExecuteAsync<SolutionStructureData>(executor, registry, "get-solution-structure", new Dictionary<string, JsonElement>(), expectProtocolSuccess: false);

        result.Outcome.Should().Be(ToolOutcome.Rejected);
        result.Error!.Code.Should().Be("WorkspaceNotOpen");
    }

    [Fact]
    public async Task GIVEN_AmbiguousSelection_WHEN_ResolvingSymbol_THEN_ShouldRejectWithAmbiguousLocation()
    {
        using var fixture = await InspectionSampleFixture.CreateAsync();
        var coordinator = WorkspaceCoordinatorFactory.Create(new WorkspaceCoordinatorOptions
        {
            DefaultMaxResults = 100,
            MaxConcurrentQueries = 2,
            MaxResponseBytes = 65536,
        }, toolExecutionServices: BundledCoreToolExecutionServicesFactory.Create());
        var openResult = await coordinator.OpenAsync(new WorkspaceOpenRequest
        {
            Path = fixture.ProjectPath,
        }, CancellationToken.None);
        var plugin = new BundledCorePlugin();
        var registry = new PluginRegistry(plugin.Metadata);
        var executor = new ToolExecutor(coordinator);

        plugin.Register(registry);
        openResult.Outcome.Should().Be(ToolOutcome.Succeeded);

        var result = await ExecuteAsync<ResolveSymbolData>(executor, registry, "resolve-symbol", new Dictionary<string, JsonElement>
        {
            ["location"] = JsonSerializer.SerializeToElement(new LocationSelector
            {
                Selection = new TextSelectionSelector
                {
                    Document = new DocumentSelector
                    {
                        Path = "Formatting.cs",
                    },
                    SelectedText = "Format",
                },
            }),
            ["expectedSnapshot"] = JsonSerializer.SerializeToElement(new SnapshotPrecondition
            {
                WorkspaceEpoch = openResult.WorkspaceEpoch!.Value,
            }),
        }, expectProtocolSuccess: false);

        result.Outcome.Should().Be(ToolOutcome.Rejected);
        result.Error!.Code.Should().Be("LocationAmbiguous");
    }

    [Fact]
    public async Task GIVEN_StaleSnapshot_WHEN_ResolvingSymbol_THEN_ShouldRejectWithSnapshotMismatch()
    {
        using var fixture = await InspectionSampleFixture.CreateAsync();
        var coordinator = WorkspaceCoordinatorFactory.Create(new WorkspaceCoordinatorOptions
        {
            DefaultMaxResults = 100,
            MaxConcurrentQueries = 2,
            MaxResponseBytes = 65536,
        }, toolExecutionServices: BundledCoreToolExecutionServicesFactory.Create());
        var openResult = await coordinator.OpenAsync(new WorkspaceOpenRequest
        {
            Path = fixture.ProjectPath,
        }, CancellationToken.None);
        var plugin = new BundledCorePlugin();
        var registry = new PluginRegistry(plugin.Metadata);
        var executor = new ToolExecutor(coordinator);

        plugin.Register(registry);
        openResult.Outcome.Should().Be(ToolOutcome.Succeeded);

        var result = await ExecuteAsync<ResolveSymbolData>(executor, registry, "resolve-symbol", new Dictionary<string, JsonElement>
        {
            ["location"] = JsonSerializer.SerializeToElement(fixture.GetLocation("GreetingFormatter")),
            ["expectedSnapshot"] = JsonSerializer.SerializeToElement(new SnapshotPrecondition
            {
                WorkspaceEpoch = openResult.WorkspaceEpoch!.Value + 1,
            }),
        }, expectProtocolSuccess: false);

        result.Outcome.Should().Be(ToolOutcome.Rejected);
        result.Error!.Code.Should().Be("SnapshotMismatch");
    }

    [Fact]
    public async Task GIVEN_MetadataSymbol_WHEN_GoingToDefinition_THEN_ShouldReturnMetadataLocation()
    {
        using var fixture = await InspectionSampleFixture.CreateAsync();
        var coordinator = WorkspaceCoordinatorFactory.Create(new WorkspaceCoordinatorOptions
        {
            DefaultMaxResults = 100,
            MaxConcurrentQueries = 2,
            MaxResponseBytes = 65536,
        }, toolExecutionServices: BundledCoreToolExecutionServicesFactory.Create());
        var openResult = await coordinator.OpenAsync(new WorkspaceOpenRequest
        {
            Path = fixture.ProjectPath,
        }, CancellationToken.None);
        var plugin = new BundledCorePlugin();
        var registry = new PluginRegistry(plugin.Metadata);
        var executor = new ToolExecutor(coordinator);

        plugin.Register(registry);
        openResult.Outcome.Should().Be(ToolOutcome.Succeeded);

        var resolveSymbol = await ExecuteAsync<ResolveSymbolData>(executor, registry, "resolve-symbol", new Dictionary<string, JsonElement>
        {
            ["location"] = JsonSerializer.SerializeToElement(fixture.GetLocation("ToUpperInvariant")),
            ["expectedSnapshot"] = JsonSerializer.SerializeToElement(new SnapshotPrecondition
            {
                WorkspaceEpoch = openResult.WorkspaceEpoch!.Value,
            }),
        });
        var definition = await ExecuteAsync<DefinitionData>(executor, registry, "go-to-definition", new Dictionary<string, JsonElement>
        {
            ["symbol"] = JsonSerializer.SerializeToElement(resolveSymbol.Data!.Selector),
        });

        definition.Data!.Definitions.Should().ContainSingle(static location => location.IsMetadata);
    }

    [Fact]
    public async Task GIVEN_FilteredDiagnosticsRequest_WHEN_QueryingDiagnostics_THEN_ShouldApplyFiltersWithoutDuplicates()
    {
        using var fixture = await InspectionSampleFixture.CreateAsync();
        var coordinator = WorkspaceCoordinatorFactory.Create(new WorkspaceCoordinatorOptions
        {
            DefaultMaxResults = 100,
            MaxConcurrentQueries = 2,
            MaxResponseBytes = 65536,
        }, toolExecutionServices: BundledCoreToolExecutionServicesFactory.Create());
        var openResult = await coordinator.OpenAsync(new WorkspaceOpenRequest
        {
            Path = fixture.ProjectPath,
        }, CancellationToken.None);
        var plugin = new BundledCorePlugin();
        var registry = new PluginRegistry(plugin.Metadata);
        var executor = new ToolExecutor(coordinator);

        plugin.Register(registry);
        openResult.Outcome.Should().Be(ToolOutcome.Succeeded);

        var result = await ExecuteAsync<DiagnosticsData>(executor, registry, "get-diagnostics", new Dictionary<string, JsonElement>
        {
            ["scope"] = JsonSerializer.SerializeToElement(new ScopeSelector
            {
                Kind = ScopeKind.Document,
                Document = new DocumentSelector
                {
                    Path = "Formatting.cs",
                },
            }),
            ["severities"] = JsonSerializer.SerializeToElement(new[] { "Warning" }),
            ["ids"] = JsonSerializer.SerializeToElement(new[] { "CS0219" }),
        });

        result.Data!.Diagnostics.Should().ContainSingle(static diagnostic => diagnostic.Id == "CS0219");
    }

    [Fact]
    public async Task GIVEN_LimitedSymbolSearch_WHEN_MultipleMatchesExist_THEN_ShouldReportHasMore()
    {
        using var fixture = await InspectionSampleFixture.CreateAsync();
        var coordinator = WorkspaceCoordinatorFactory.Create(new WorkspaceCoordinatorOptions
        {
            DefaultMaxResults = 100,
            MaxConcurrentQueries = 2,
            MaxResponseBytes = 65536,
        }, toolExecutionServices: BundledCoreToolExecutionServicesFactory.Create());
        var openResult = await coordinator.OpenAsync(new WorkspaceOpenRequest
        {
            Path = fixture.ProjectPath,
        }, CancellationToken.None);
        var plugin = new BundledCorePlugin();
        var registry = new PluginRegistry(plugin.Metadata);
        var executor = new ToolExecutor(coordinator);

        plugin.Register(registry);
        openResult.Outcome.Should().Be(ToolOutcome.Succeeded);

        var result = await ExecuteAsync<SymbolSearchData>(executor, registry, "search-symbols", new Dictionary<string, JsonElement>
        {
            ["query"] = JsonSerializer.SerializeToElement("Format"),
            ["limit"] = JsonSerializer.SerializeToElement(new ResultLimit
            {
                MaxResults = 1,
            }),
        });

        result.Data!.Symbols.Should().HaveCount(1);
        result.Data.HasMore.Should().BeTrue();
    }

    [Fact]
    public async Task GIVEN_LowResponseByteLimit_WHEN_SearchingSymbols_THEN_ShouldTruncateInsteadOfRejecting()
    {
        using var fixture = await InspectionSampleFixture.CreateAsync();
        var fullCoordinator = WorkspaceCoordinatorFactory.Create(new WorkspaceCoordinatorOptions
        {
            DefaultMaxResults = 100,
            MaxConcurrentQueries = 2,
            MaxResponseBytes = 65536,
        }, toolExecutionServices: BundledCoreToolExecutionServicesFactory.Create());
        var truncatedCoordinator = WorkspaceCoordinatorFactory.Create(new WorkspaceCoordinatorOptions
        {
            DefaultMaxResults = 100,
            MaxConcurrentQueries = 2,
            MaxResponseBytes = 500,
        }, toolExecutionServices: BundledCoreToolExecutionServicesFactory.Create());
        var plugin = new BundledCorePlugin();
        var registry = new PluginRegistry(plugin.Metadata);
        var fullExecutor = new ToolExecutor(fullCoordinator);
        var truncatedExecutor = new ToolExecutor(truncatedCoordinator);

        plugin.Register(registry);

        (await fullCoordinator.OpenAsync(new WorkspaceOpenRequest
        {
            Path = fixture.ProjectPath,
        }, CancellationToken.None)).Outcome.Should().Be(ToolOutcome.Succeeded);
        (await truncatedCoordinator.OpenAsync(new WorkspaceOpenRequest
        {
            Path = fixture.ProjectPath,
        }, CancellationToken.None)).Outcome.Should().Be(ToolOutcome.Succeeded);

        var fullResult = await ExecuteAsync<SymbolSearchData>(fullExecutor, registry, "search-symbols", new Dictionary<string, JsonElement>
        {
            ["query"] = JsonSerializer.SerializeToElement("Format"),
        });
        var truncatedResult = await ExecuteAsync<SymbolSearchData>(truncatedExecutor, registry, "search-symbols", new Dictionary<string, JsonElement>
        {
            ["query"] = JsonSerializer.SerializeToElement("Format"),
        });

        fullResult.Data!.Symbols.Count.Should().BeGreaterThan(1);
        truncatedResult.Outcome.Should().Be(ToolOutcome.Succeeded);
        truncatedResult.Data!.Symbols.Count.Should().BeLessThan(fullResult.Data.Symbols.Count);
        truncatedResult.Data.HasMore.Should().BeTrue();
    }

    [Fact]
    public async Task GIVEN_SolutionHierarchyWorkspace_WHEN_GettingSolutionStructure_THEN_ShouldReturnFoldersAndProjectFolderPaths()
    {
        using var fixture = await SolutionHierarchyFixture.CreateAsync();
        var coordinator = WorkspaceCoordinatorFactory.Create(new WorkspaceCoordinatorOptions
        {
            DefaultMaxResults = 100,
            MaxConcurrentQueries = 2,
            MaxResponseBytes = 65536,
        }, toolExecutionServices: BundledCoreToolExecutionServicesFactory.Create());
        var openResult = await coordinator.OpenAsync(new WorkspaceOpenRequest
        {
            Path = fixture.SolutionPath,
        }, CancellationToken.None);
        var plugin = new BundledCorePlugin();
        var registry = new PluginRegistry(plugin.Metadata);
        var executor = new ToolExecutor(coordinator);

        plugin.Register(registry);
        openResult.Outcome.Should().Be(ToolOutcome.Succeeded);

        var result = await ExecuteAsync<SolutionStructureData>(executor, registry, "get-solution-structure", new Dictionary<string, JsonElement>());

        result.Data!.Folders.Should().Contain(static folder => folder.Path == "src");
        result.Data.Folders.Should().Contain(static folder => folder.Path == "src/core" && folder.ParentPath == "src");
        result.Data.Projects.Should().ContainSingle(static project => project.Name == "Lib" && project.SolutionFolderPath == "src/core");
    }

    [Fact]
    public async Task GIVEN_PropertyReferences_WHEN_FindingReferences_THEN_ShouldClassifyWrites()
    {
        using var fixture = await InspectionSampleFixture.CreateAsync();
        var coordinator = WorkspaceCoordinatorFactory.Create(new WorkspaceCoordinatorOptions
        {
            DefaultMaxResults = 100,
            MaxConcurrentQueries = 2,
            MaxResponseBytes = 65536,
        }, toolExecutionServices: BundledCoreToolExecutionServicesFactory.Create());
        var openResult = await coordinator.OpenAsync(new WorkspaceOpenRequest
        {
            Path = fixture.ProjectPath,
        }, CancellationToken.None);
        var plugin = new BundledCorePlugin();
        var registry = new PluginRegistry(plugin.Metadata);
        var executor = new ToolExecutor(coordinator);

        plugin.Register(registry);
        openResult.Outcome.Should().Be(ToolOutcome.Succeeded);

        var result = await ExecuteAsync<ReferenceSearchData>(executor, registry, "find-references", new Dictionary<string, JsonElement>
        {
            ["symbol"] = JsonSerializer.SerializeToElement(new SymbolSelector
            {
                DocumentationCommentId = "P:Sample.StateHolder.Current",
            }),
            ["includeDefinitions"] = JsonSerializer.SerializeToElement(false),
            ["includeContext"] = JsonSerializer.SerializeToElement(true),
        });

        result.Data!.References.Should().Contain(static reference => reference.IsWrite && reference.Context == "Current = value;");
        result.Data.References.Should().Contain(static reference => !reference.IsWrite && reference.Context == "return Current;");
    }

    private static async Task<ToolResult<TResponse>> ExecuteAsync<TResponse>(
        ToolExecutor executor,
        PluginRegistry registry,
        string toolName,
        IDictionary<string, JsonElement> arguments,
        bool expectProtocolSuccess = true)
    {
        var registeredTool = registry.RegisteredTools.Single(tool => tool.Metadata.Name == toolName);
        var result = await executor.ExecuteAsync(registeredTool, arguments, CancellationToken.None);

        result.IsError.Should().Be(!expectProtocolSuccess);

        return DeserializeToolResult<TResponse>(registeredTool, result.StructuredContent!.Value, toolName);
    }

    private static ToolResult<TResponse> DeserializeToolResult<TResponse>(RegisteredTool registeredTool, JsonElement payload, string toolName)
    {
        if (payload.TryGetProperty("outcome", out _))
        {
            return JsonSerializer.Deserialize<ToolResult<TResponse>>(payload.GetRawText(), _serializerOptions)!;
        }

        if (!payload.GetProperty("ok").GetBoolean())
        {
            return ToolResult<TResponse>.Rejected(
                JsonSerializer.Deserialize<ToolError>(payload.GetProperty("error").GetRawText(), _serializerOptions)!,
                payload.TryGetProperty("next", out var nextElement) && nextElement.ValueKind != JsonValueKind.Null
                    ? JsonSerializer.Deserialize<RequiredAction>(nextElement.GetRawText(), _serializerOptions)
                    : null);
        }

        var data = registeredTool.ResponseDescriptor.Kind switch
        {
            ToolResponseShapeKind.Direct => DeserializeDirectData<TResponse>(payload),
            ToolResponseShapeKind.Singleton => JsonSerializer.Deserialize<TResponse>(payload.GetProperty("value").GetRawText(), _serializerOptions)!,
            ToolResponseShapeKind.Collection => DeserializeCollectionData<TResponse>(registeredTool.ResponseDescriptor, payload),
            ToolResponseShapeKind.Mutation => (TResponse)(object)DeserializeMutationData(payload, toolName),
            ToolResponseShapeKind.CodeActionList => (TResponse)(object)DeserializeCodeActionListData(payload),
            _ => throw new InvalidOperationException($"Unsupported response shape kind '{registeredTool.ResponseDescriptor.Kind}'."),
        };

        var transactionRevision = data is MutationData mutationData
            ? mutationData.Transaction?.Revision
            : null;

        return ToolResult<TResponse>.Succeeded(data, transactionRevision: transactionRevision);
    }

    private static TResponse DeserializeDirectData<TResponse>(JsonElement payload)
    {
        var node = JsonNode.Parse(payload.GetRawText())!.AsObject();
        node.Remove("ok");

        return node.Deserialize<TResponse>(_serializerOptions)!;
    }

    private static TResponse DeserializeCollectionData<TResponse>(ToolResponseDescriptor descriptor, JsonElement payload)
    {
        var node = JsonNode.Parse(payload.GetRawText())!.AsObject();
        var itemsNode = node["items"]?.DeepClone();
        var hasMoreNode = node["hasMore"]?.DeepClone();
        var truncatedByNode = node["truncatedBy"]?.DeepClone();

        node.Remove("ok");
        node.Remove("items");
        node.Remove("hasMore");
        node.Remove("truncatedBy");
        node[JsonNamingPolicy.CamelCase.ConvertName(descriptor.CollectionPropertyName!)] = itemsNode;
        node["hasMore"] = hasMoreNode;
        node["returnedCount"] = itemsNode is JsonArray itemsArray ? itemsArray.Count : 0;

        if (truncatedByNode is not null)
        {
            node["truncationReasons"] = truncatedByNode;
        }

        return node.Deserialize<TResponse>(_serializerOptions)!;
    }

    private static MutationData DeserializeMutationData(JsonElement payload, string toolName)
    {
        return new MutationData
        {
            Operation = toolName,
            Summary = payload.TryGetProperty("summary", out var summaryElement) && summaryElement.ValueKind == JsonValueKind.String
                ? summaryElement.GetString() ?? string.Empty
                : string.Empty,
            Transaction = payload.TryGetProperty("transaction", out var transactionElement)
                ? new TransactionInfo
                {
                    Revision = transactionElement.GetProperty("revision").GetInt32(),
                }
                : null,
        };
    }

    private static CodeActionListData DeserializeCodeActionListData(JsonElement payload)
    {
        var items = JsonSerializer.Deserialize<IReadOnlyList<CodeActionListItem>>(payload.GetProperty("items").GetRawText(), _serializerOptions) ?? [];

        return new CodeActionListData
        {
            Actions = items.Select(static item => new CodeActionInfo
            {
                ActionId = item.ActionId,
                Title = item.Title,
                ProviderId = item.ProviderId,
                Kind = item.Kind,
                ExecutionMode = item.ExecutionMode,
                ExecutorTool = item.ExecutorTool,
                DescribeTool = item.DescribeTool,
                UnsupportedReasonCode = item.UnsupportedReasonCode,
            }).ToArray(),
            ReturnedCount = items.Count,
            HasMore = payload.GetProperty("hasMore").GetBoolean(),
            TruncationReasons = payload.TryGetProperty("truncatedBy", out var truncatedByElement)
                ? JsonSerializer.Deserialize<IReadOnlyList<CollectionTruncation>>(truncatedByElement.GetRawText(), _serializerOptions)
                : null,
        };
    }

    private static IEnumerable<OutlineNode> EnumerateOutline(OutlineNode root)
    {
        yield return root;

        foreach (var child in root.Children)
        {
            foreach (var descendant in EnumerateOutline(child))
            {
                yield return descendant;
            }
        }
    }
}
