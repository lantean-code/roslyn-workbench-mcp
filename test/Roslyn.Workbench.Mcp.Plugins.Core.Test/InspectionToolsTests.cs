using System.Text.Json;

using AwesomeAssertions;

using Roslyn.Workbench.Mcp.Contracts.Inspection;
using Roslyn.Workbench.Mcp.Contracts.Refactorings;
using Roslyn.Workbench.Mcp.Contracts.Results;
using Roslyn.Workbench.Mcp.Contracts.Selectors;
using Roslyn.Workbench.Mcp.Contracts.Server;
using Roslyn.Workbench.Mcp.Contracts.Transactions;
using Roslyn.Workbench.Mcp.Plugins;
using Roslyn.Workbench.Mcp.Plugins.Core;
using Roslyn.Workbench.Mcp.TestSupport;
using Roslyn.Workbench.Mcp.Workspace;

using Xunit;

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
    public async Task GIVEN_InspectionWorkspace_WHEN_ExecutingStructuralAndSemanticTools_THEN_ShouldReturnProjectedRoslynData()
    {
        using var fixture = await InspectionSampleFixture.CreateAsync();
        var coordinator = WorkspaceCoordinatorFactory.Create(new WorkspaceCoordinatorOptions
        {
            DefaultMaxResults = 100,
            MaxConcurrentQueries = 2,
            MaxResponseBytes = 65536,
        });
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
        projectDetails.Data.Documents.Should().NotBeNull();
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
        });
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
    public async Task GIVEN_ActiveTransaction_WHEN_ExecutingBatch1MutationTools_THEN_ShouldStageStructuredMutations()
    {
        using var fixture = await InspectionSampleFixture.CreateAsync();
        var coordinator = WorkspaceCoordinatorFactory.Create(new WorkspaceCoordinatorOptions
        {
            DefaultMaxResults = 100,
            MaxConcurrentQueries = 2,
            MaxResponseBytes = 65536,
        });
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
            CodeActionService = runtime.CodeActionService,
            WorkspaceHostServices = runtime.WorkspaceHostServices,
            DefaultMaxResults = 100,
            MaxConcurrentQueries = 2,
            MaxResponseBytes = 65536,
        });
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
            CodeActionService = runtime.CodeActionService,
            WorkspaceHostServices = runtime.WorkspaceHostServices,
            DefaultMaxResults = 100,
            MaxConcurrentQueries = 2,
            MaxResponseBytes = 65536,
        });
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
            CodeActionService = runtime.CodeActionService,
            WorkspaceHostServices = runtime.WorkspaceHostServices,
            DefaultMaxResults = 100,
            MaxConcurrentQueries = 2,
            MaxResponseBytes = 65536,
        });
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
            CodeActionService = runtime.CodeActionService,
            WorkspaceHostServices = runtime.WorkspaceHostServices,
            DefaultMaxResults = 100,
            MaxConcurrentQueries = 2,
            MaxResponseBytes = 65536,
        });
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
            CodeActionService = runtime.CodeActionService,
            WorkspaceHostServices = runtime.WorkspaceHostServices,
            DefaultMaxResults = 100,
            MaxConcurrentQueries = 2,
            MaxResponseBytes = 65536,
        });
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
            CodeActionService = runtime.CodeActionService,
            WorkspaceHostServices = runtime.WorkspaceHostServices,
            DefaultMaxResults = 100,
            MaxConcurrentQueries = 2,
            MaxResponseBytes = 65536,
        });
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
            CodeActionService = runtime.CodeActionService,
            WorkspaceHostServices = runtime.WorkspaceHostServices,
            DefaultMaxResults = 100,
            MaxConcurrentQueries = 2,
            MaxResponseBytes = 65536,
        });
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
            CodeActionService = runtime.CodeActionService,
            WorkspaceHostServices = runtime.WorkspaceHostServices,
            DefaultMaxResults = 100,
            MaxConcurrentQueries = 2,
            MaxResponseBytes = 65536,
        });
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
            CodeActionService = runtime.CodeActionService,
            WorkspaceHostServices = runtime.WorkspaceHostServices,
            DefaultMaxResults = 100,
            MaxConcurrentQueries = 2,
            MaxResponseBytes = 65536,
        });
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
            CodeActionService = runtime.CodeActionService,
            WorkspaceHostServices = runtime.WorkspaceHostServices,
            DefaultMaxResults = 100,
            MaxConcurrentQueries = 2,
            MaxResponseBytes = 65536,
        });
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
            CodeActionService = runtime.CodeActionService,
            WorkspaceHostServices = runtime.WorkspaceHostServices,
            DefaultMaxResults = 100,
            MaxConcurrentQueries = 2,
            MaxResponseBytes = 65536,
        });
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
        });
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
        });
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
        });
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

        result.Outcome.Should().Be(ToolOutcome.Conflict);
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
        });
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
        });
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
        });
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
        });
        var truncatedCoordinator = WorkspaceCoordinatorFactory.Create(new WorkspaceCoordinatorOptions
        {
            DefaultMaxResults = 100,
            MaxConcurrentQueries = 2,
            MaxResponseBytes = 500,
        });
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
        });
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
        });
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

        return JsonSerializer.Deserialize<ToolResult<TResponse>>(result.StructuredContent!.Value.GetRawText(), _serializerOptions)!;
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
