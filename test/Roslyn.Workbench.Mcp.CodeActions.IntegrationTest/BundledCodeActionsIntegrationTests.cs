using System.Text.Json;

namespace Roslyn.Workbench.Mcp.CodeActions.Test;

[Trait("Category", "Integration")]
public sealed class BundledCodeActionsIntegrationTests
{
    [Fact]
    public void GIVEN_BundledPlugins_WHEN_RegisteringTools_THEN_ShouldPublishInitialCodeActionSurface()
    {
        var registry = BundledPluginRegistryFactory.CreateRegistry();

        registry.RegisteredTools.Select(static tool => tool.Metadata.Name).Should().Contain(
        [
            "list-code-actions",
            "describe-code-action",
            "stage-code-action",
            "stage-code-fix",
            "stage-fix-all",
        ]);
    }

    [Fact]
    public void GIVEN_BundledPlugins_WHEN_RegisteringDedicatedRefactoringTools_THEN_ShouldPublishLiveToolSurface()
    {
        var registry = BundledPluginRegistryFactory.CreateRegistry();

        registry.RegisteredTools.Select(static tool => tool.Metadata.Name).Should().Contain(BuiltInCodeActionAuditCases.VisibleDedicatedToolNames);
        registry.RegisteredTools.Select(static tool => tool.Metadata.Name).Should().Contain(["move-type-to-file", "convert-property"]);
    }

    [Fact]
    public void GIVEN_BundledPlugins_WHEN_RegisteringConvertAutoPropertyTool_THEN_ShouldUseDedicatedRequestContract()
    {
        var registry = BundledPluginRegistryFactory.CreateRegistry();

        var tool = registry.RegisteredTools.Single(static registeredTool => registeredTool.Metadata.Name == "convert-auto-property-to-full-property");

        tool.RequestType.Should().Be(typeof(ConvertAutoPropertyToFullPropertyRequest));
    }

    [Fact]
    public async Task GIVEN_ActiveTransactionAndBuiltInCodeActions_WHEN_RemovingUnusedUsings_THEN_ShouldStageStructuredMutation()
    {
        using var fixture = await InspectionSampleFixture.CreateAsync();
        var coordinator = CreateBuiltInCoordinator();
        var openResult = await coordinator.OpenAsync(new WorkspaceOpenRequest
        {
            Path = fixture.ProjectPath,
        }, CancellationToken.None);
        await coordinator.StartTransactionAsync(new TransactionStartRequest(), CancellationToken.None);
        var plugin = new BundledCodeActionsPlugin();
        var registry = new PluginRegistry(plugin.Metadata);
        var executor = coordinator;

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
        var coordinator = CreateBuiltInCoordinator();
        var openResult = await coordinator.OpenAsync(new WorkspaceOpenRequest
        {
            Path = fixture.ProjectPath,
        }, CancellationToken.None);
        await coordinator.StartTransactionAsync(new TransactionStartRequest(), CancellationToken.None);
        var plugin = new BundledCodeActionsPlugin();
        var registry = new PluginRegistry(plugin.Metadata);
        var executor = coordinator;

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
        var coordinator = CreateBuiltInCoordinator();
        var openResult = await coordinator.OpenAsync(new WorkspaceOpenRequest
        {
            Path = fixture.ProjectPath,
        }, CancellationToken.None);
        await coordinator.StartTransactionAsync(new TransactionStartRequest(), CancellationToken.None);
        var plugin = new BundledCodeActionsPlugin();
        var registry = new PluginRegistry(plugin.Metadata);
        var executor = coordinator;

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
        var coordinator = CreateBuiltInCoordinator();
        var openResult = await coordinator.OpenAsync(new WorkspaceOpenRequest
        {
            Path = fixture.ProjectPath,
        }, CancellationToken.None);
        await coordinator.StartTransactionAsync(new TransactionStartRequest(), CancellationToken.None);
        var plugin = new BundledCodeActionsPlugin();
        var registry = new PluginRegistry(plugin.Metadata);
        var executor = coordinator;

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
        var coordinator = CreateBuiltInCoordinator();
        var openResult = await coordinator.OpenAsync(new WorkspaceOpenRequest
        {
            Path = fixture.ProjectPath,
        }, CancellationToken.None);
        await coordinator.StartTransactionAsync(new TransactionStartRequest(), CancellationToken.None);
        var plugin = new BundledCodeActionsPlugin();
        var registry = new PluginRegistry(plugin.Metadata);
        var executor = coordinator;

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
        var coordinator = CreateBuiltInCoordinator();
        var openResult = await coordinator.OpenAsync(new WorkspaceOpenRequest
        {
            Path = fixture.ProjectPath,
        }, CancellationToken.None);
        await coordinator.StartTransactionAsync(new TransactionStartRequest(), CancellationToken.None);
        var plugin = new BundledCodeActionsPlugin();
        var registry = new PluginRegistry(plugin.Metadata);
        var executor = coordinator;

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
        var coordinator = CreateBuiltInCoordinator();
        var openResult = await coordinator.OpenAsync(new WorkspaceOpenRequest
        {
            Path = fixture.ProjectPath,
        }, CancellationToken.None);
        await coordinator.StartTransactionAsync(new TransactionStartRequest(), CancellationToken.None);
        var plugin = new BundledCodeActionsPlugin();
        var registry = new PluginRegistry(plugin.Metadata);
        var executor = coordinator;

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
        var coordinator = CreateBuiltInCoordinator();
        var openResult = await coordinator.OpenAsync(new WorkspaceOpenRequest
        {
            Path = fixture.ProjectPath,
        }, CancellationToken.None);
        await coordinator.StartTransactionAsync(new TransactionStartRequest(), CancellationToken.None);
        var plugin = new BundledCodeActionsPlugin();
        var registry = new PluginRegistry(plugin.Metadata);
        var executor = coordinator;

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
        var coordinator = CreateBuiltInCoordinator();
        var openResult = await coordinator.OpenAsync(new WorkspaceOpenRequest
        {
            Path = fixture.ProjectPath,
        }, CancellationToken.None);
        await coordinator.StartTransactionAsync(new TransactionStartRequest(), CancellationToken.None);
        var plugin = new BundledCodeActionsPlugin();
        var registry = new PluginRegistry(plugin.Metadata);
        var executor = coordinator;

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
        var coordinator = CreateBuiltInCoordinator();
        var openResult = await coordinator.OpenAsync(new WorkspaceOpenRequest
        {
            Path = fixture.ProjectPath,
        }, CancellationToken.None);
        await coordinator.StartTransactionAsync(new TransactionStartRequest(), CancellationToken.None);
        var plugin = new BundledCodeActionsPlugin();
        var registry = new PluginRegistry(plugin.Metadata);
        var executor = coordinator;

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
        var coordinator = CreateBuiltInCoordinator();
        var openResult = await coordinator.OpenAsync(new WorkspaceOpenRequest
        {
            Path = fixture.ProjectPath,
        }, CancellationToken.None);
        await coordinator.StartTransactionAsync(new TransactionStartRequest(), CancellationToken.None);
        var plugin = new BundledCodeActionsPlugin();
        var registry = new PluginRegistry(plugin.Metadata);
        var executor = coordinator;

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

    private static async Task<ToolResult<TResponse>> ExecuteAsync<TResponse>(
        IToolExecutionContextFactory executor,
        PluginRegistry registry,
        string toolName,
        IDictionary<string, JsonElement> arguments,
        bool expectProtocolSuccess = true)
    {
        return await PluginToolTestHarness.InvokeAsync<TResponse>(executor, registry, toolName, arguments, expectProtocolSuccess);
    }

    private static IWorkspaceRuntime CreateBuiltInCoordinator()
    {
        return BundledCoreToolTestHarness.CreateBuiltInCodeActionCoordinator();
    }
}
