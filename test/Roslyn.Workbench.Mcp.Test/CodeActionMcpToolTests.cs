using System.Text.Json;
using System.Text.Json.Nodes;

namespace Roslyn.Workbench.Mcp.Test;

public sealed class CodeActionMcpToolTests
{
    private static readonly JsonSerializerOptions _serializerOptions = new(JsonSerializerDefaults.Web);

    [Fact]
    public async Task GIVEN_DefaultCoordinator_WHEN_InvokingListCodeActions_THEN_ShouldRejectAsUnavailable()
    {
        using var fixture = await InspectionSampleFixture.CreateAsync();
        var coordinator = WorkspaceCoordinatorFactory.Create(new WorkspaceCoordinatorOptions
        {
            DefaultMaxResults = 100,
            MaxConcurrentQueries = 2,
            MaxResponseBytes = 65536,
        });
        var open = await coordinator.OpenAsync(new WorkspaceOpenRequest
        {
            Path = fixture.ProjectPath,
        }, CancellationToken.None);
        var plugin = new BundledCorePlugin();
        var registry = new PluginRegistry(plugin.Metadata);
        var executor = new ToolExecutor(coordinator);

        plugin.Register(registry);

        var result = await InvokeAsync<CodeActionListData>(executor, registry, "list-code-actions", new Dictionary<string, JsonElement>
        {
            ["location"] = JsonSerializer.SerializeToElement(fixture.GetLocation("StateHolder")),
            ["expectedSnapshot"] = JsonSerializer.SerializeToElement(new SnapshotPrecondition
            {
                WorkspaceEpoch = open.WorkspaceEpoch!.Value,
            }),
        }, expectProtocolSuccess: false);

        result.Outcome.Should().Be(ToolOutcome.Rejected);
        result.Error!.Code.Should().Be("CodeActionsUnavailable");
    }

    [Fact]
    public async Task GIVEN_TestProviders_WHEN_ListingActions_THEN_ShouldReturnDeterministicActionTokens()
    {
        using var fixture = await InspectionSampleFixture.CreateAsync();
        var coordinator = CreateCoordinator();
        var open = await coordinator.OpenAsync(new WorkspaceOpenRequest
        {
            Path = fixture.ProjectPath,
        }, CancellationToken.None);
        var plugin = new BundledCorePlugin();
        var registry = new PluginRegistry(plugin.Metadata);
        var executor = new ToolExecutor(coordinator);

        plugin.Register(registry);

        var result = await InvokeAsync<CodeActionListData>(executor, registry, "list-code-actions", new Dictionary<string, JsonElement>
        {
            ["location"] = JsonSerializer.SerializeToElement(fixture.GetLocation("StateHolder")),
            ["expectedSnapshot"] = JsonSerializer.SerializeToElement(new SnapshotPrecondition
            {
                WorkspaceEpoch = open.WorkspaceEpoch!.Value,
            }),
        });

        result.Data!.Actions.Select(static action => action.Title).Should().ContainInOrder(
        [
            "Apply test refactoring",
            "Change signature test refactoring",
            "Option gathering test refactoring",
            "Retain test state",
            "Unsupported test refactoring",
        ]);
        result.Data.Actions.Should().OnlyContain(static action => !string.IsNullOrWhiteSpace(action.ActionId));
    }

    [Fact]
    public async Task GIVEN_TestProviders_WHEN_ListingActions_THEN_ShouldPublishExecutionMetadata()
    {
        using var fixture = await InspectionSampleFixture.CreateAsync();
        var coordinator = CreateCoordinator();
        var open = await coordinator.OpenAsync(new WorkspaceOpenRequest
        {
            Path = fixture.ProjectPath,
        }, CancellationToken.None);
        var plugin = new BundledCorePlugin();
        var registry = new PluginRegistry(plugin.Metadata);
        var executor = new ToolExecutor(coordinator);

        plugin.Register(registry);

        var result = await InvokeAsync<CodeActionListData>(executor, registry, "list-code-actions", new Dictionary<string, JsonElement>
        {
            ["location"] = JsonSerializer.SerializeToElement(fixture.GetLocation("StateHolder")),
            ["expectedSnapshot"] = JsonSerializer.SerializeToElement(new SnapshotPrecondition
            {
                WorkspaceEpoch = open.WorkspaceEpoch!.Value,
            }),
        });

        var replay = result.Data!.Actions.Single(static action => action.Title == "Apply test refactoring");
        var parameterised = result.Data.Actions.Single(static action => action.Title == "Change signature test refactoring");
        var unsupported = result.Data.Actions.Single(static action => action.Title == "Option gathering test refactoring");

        replay.ExecutionMode.Should().Be(CodeActionExecutionMode.Replay);
        parameterised.ExecutionMode.Should().Be(CodeActionExecutionMode.Parameterised);
        parameterised.ExecutorTool.Should().BeNull();
        parameterised.DescribeTool.Should().Be("describe-code-action");
        unsupported.ExecutionMode.Should().Be(CodeActionExecutionMode.Unsupported);
        unsupported.UnsupportedReasonCode.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task GIVEN_ParameterisedAction_WHEN_Describing_THEN_ShouldReturnDescriptorContext()
    {
        using var fixture = await InspectionSampleFixture.CreateAsync();
        var coordinator = CreateCoordinator();
        var open = await coordinator.OpenAsync(new WorkspaceOpenRequest
        {
            Path = fixture.ProjectPath,
        }, CancellationToken.None);
        var plugin = new BundledCorePlugin();
        var registry = new PluginRegistry(plugin.Metadata);
        var executor = new ToolExecutor(coordinator);

        plugin.Register(registry);

        var list = await InvokeAsync<CodeActionListData>(executor, registry, "list-code-actions", new Dictionary<string, JsonElement>
        {
            ["location"] = JsonSerializer.SerializeToElement(fixture.GetLocation("StateHolder")),
            ["expectedSnapshot"] = JsonSerializer.SerializeToElement(new SnapshotPrecondition
            {
                WorkspaceEpoch = open.WorkspaceEpoch!.Value,
            }),
        });
        var parameterised = list.Data!.Actions.Single(static action => action.Title == "Change signature test refactoring");
        var describe = await InvokeAsync<DescribeCodeActionData>(executor, registry, "describe-code-action", new Dictionary<string, JsonElement>
        {
            ["actionId"] = JsonSerializer.SerializeToElement(parameterised.ActionId),
            ["expectedSnapshot"] = JsonSerializer.SerializeToElement(new SnapshotPrecondition
            {
                WorkspaceEpoch = open.WorkspaceEpoch!.Value,
            }),
        });

        describe.Outcome.Should().Be(ToolOutcome.Succeeded);
        describe.Data!.Descriptor.Title.Should().Be("Change signature test refactoring");
        describe.Data.Context.Kind.Should().Be(CodeActionDescriptorContextKind.SignaturePlan);
    }

    [Fact]
    public async Task GIVEN_BuiltInProviders_WHEN_ListingOverrideAndExtractionActions_THEN_ShouldHideOptionsServiceFamilies()
    {
        using var fixture = await InspectionSampleFixture.CreateAsync();
        var coordinator = CreateBuiltInCoordinator();
        var open = await coordinator.OpenAsync(new WorkspaceOpenRequest
        {
            Path = fixture.ProjectPath,
        }, CancellationToken.None);
        var plugin = new BundledCorePlugin();
        var registry = new PluginRegistry(plugin.Metadata);
        var executor = new ToolExecutor(coordinator);

        plugin.Register(registry);

        var result = await InvokeAsync<CodeActionListData>(executor, registry, "list-code-actions", new Dictionary<string, JsonElement>
        {
            ["location"] = JsonSerializer.SerializeToElement(fixture.GetLocation("OverrideCandidate")),
            ["expectedSnapshot"] = JsonSerializer.SerializeToElement(new SnapshotPrecondition
            {
                WorkspaceEpoch = open.WorkspaceEpoch!.Value,
            }),
        });

        result.Data!.Actions.Should().NotContain(static action => action.Title == "Generate overrides");
        result.Data.Actions.Should().NotContain(static action => action.Title == "Extract interface");
        result.Data.Actions.Should().NotContain(static action => action.Title == "Extract base class");
    }

    [Theory]
    [MemberData(nameof(GetPromotedDraftValidationCandidates))]
    public async Task GIVEN_BuiltInProviders_WHEN_ListingPromotedDraftFamilies_THEN_ShouldExposeVisibleActions(BuiltInCodeActionAuditCase auditCase)
    {
        using var fixture = await auditCase.FixtureFactory();
        var coordinator = CreateBuiltInCoordinator();
        var open = await coordinator.OpenAsync(new WorkspaceOpenRequest
        {
            Path = fixture.ProjectPath,
        }, CancellationToken.None);
        var plugin = new BundledCorePlugin();
        var registry = new PluginRegistry(plugin.Metadata);
        var executor = new ToolExecutor(coordinator);

        plugin.Register(registry);

        var result = await InvokeAsync<CodeActionListData>(executor, registry, "list-code-actions", new Dictionary<string, JsonElement>
        {
            ["location"] = JsonSerializer.SerializeToElement(auditCase.LocationFactory(fixture)),
            ["expectedSnapshot"] = JsonSerializer.SerializeToElement(new SnapshotPrecondition
            {
                WorkspaceEpoch = open.WorkspaceEpoch!.Value,
            }),
            ["includeCodeFixes"] = JsonSerializer.SerializeToElement(auditCase.Kind == BuiltInCodeActionAuditKind.CodeFix),
            ["includeRefactorings"] = JsonSerializer.SerializeToElement(auditCase.Kind == BuiltInCodeActionAuditKind.Refactoring),
        });

        result.Data!.Actions.Should().Contain(action =>
            string.Equals(action.ProviderId, auditCase.ProviderId, StringComparison.Ordinal)
            && MatchesAuditCase(action, auditCase));
    }

    [Fact]
    public void GIVEN_CurrentAuditLedger_WHEN_QueryingDeferredDraftFamilies_THEN_ShouldHaveNoResidualHiddenReplayBacklog()
    {
        BuiltInCodeActionAuditCases.FailedDraftValidationCandidates.Should().BeEmpty();
    }

    [Theory]
    [MemberData(nameof(GetPendingPromotionCandidates))]
    public async Task GIVEN_BuiltInProviders_WHEN_ListingPendingPromotionFamilies_THEN_ShouldExposeVisibleActions(BuiltInCodeActionAuditCase auditCase)
    {
        using var fixture = await auditCase.FixtureFactory();
        var coordinator = CreateBuiltInCoordinator();
        var open = await coordinator.OpenAsync(new WorkspaceOpenRequest
        {
            Path = fixture.ProjectPath,
        }, CancellationToken.None);
        var plugin = new BundledCorePlugin();
        var registry = new PluginRegistry(plugin.Metadata);
        var executor = new ToolExecutor(coordinator);

        plugin.Register(registry);

        var result = await InvokeAsync<CodeActionListData>(executor, registry, "list-code-actions", new Dictionary<string, JsonElement>
        {
            ["location"] = JsonSerializer.SerializeToElement(auditCase.LocationFactory(fixture)),
            ["expectedSnapshot"] = JsonSerializer.SerializeToElement(new SnapshotPrecondition
            {
                WorkspaceEpoch = open.WorkspaceEpoch!.Value,
            }),
            ["includeCodeFixes"] = JsonSerializer.SerializeToElement(auditCase.Kind == BuiltInCodeActionAuditKind.CodeFix),
            ["includeRefactorings"] = JsonSerializer.SerializeToElement(auditCase.Kind == BuiltInCodeActionAuditKind.Refactoring),
        });

        result.Data!.Actions.Should().Contain(action =>
            string.Equals(action.ProviderId, auditCase.ProviderId, StringComparison.Ordinal)
            && MatchesAuditCase(action, auditCase));
    }

    [Fact]
    public async Task GIVEN_ParameterisedAction_WHEN_StagingThroughGenericReplay_THEN_ShouldRejectSafely()
    {
        using var fixture = await InspectionSampleFixture.CreateAsync();
        var coordinator = CreateCoordinator();
        var open = await coordinator.OpenAsync(new WorkspaceOpenRequest
        {
            Path = fixture.ProjectPath,
        }, CancellationToken.None);
        await coordinator.StartTransactionAsync(new TransactionStartRequest(), CancellationToken.None);
        var plugin = new BundledCorePlugin();
        var registry = new PluginRegistry(plugin.Metadata);
        var executor = new ToolExecutor(coordinator);

        plugin.Register(registry);

        var list = await InvokeAsync<CodeActionListData>(executor, registry, "list-code-actions", new Dictionary<string, JsonElement>
        {
            ["location"] = JsonSerializer.SerializeToElement(fixture.GetLocation("StateHolder")),
            ["expectedSnapshot"] = JsonSerializer.SerializeToElement(new SnapshotPrecondition
            {
                WorkspaceEpoch = open.WorkspaceEpoch!.Value,
                TransactionRevision = 0,
            }),
        });
        var parameterised = list.Data!.Actions.Single(static action => action.Title == "Change signature test refactoring");
        var result = await InvokeAsync<MutationData>(executor, registry, "stage-code-action", new Dictionary<string, JsonElement>
        {
            ["actionId"] = JsonSerializer.SerializeToElement(parameterised.ActionId),
            ["expectedSnapshot"] = JsonSerializer.SerializeToElement(new SnapshotPrecondition
            {
                WorkspaceEpoch = open.WorkspaceEpoch!.Value,
                TransactionRevision = 0,
            }),
        }, expectProtocolSuccess: false);

        result.Error!.Code.Should().Be("ActionRequiresParameters");
    }

    [Fact]
    public async Task GIVEN_TestProviders_WHEN_StagingRefactoringAndCodeFix_THEN_ShouldAdvanceTransactionRevisions()
    {
        using var fixture = await InspectionSampleFixture.CreateAsync();
        var coordinator = CreateCoordinator();
        var open = await coordinator.OpenAsync(new WorkspaceOpenRequest
        {
            Path = fixture.ProjectPath,
        }, CancellationToken.None);
        await coordinator.StartTransactionAsync(new TransactionStartRequest(), CancellationToken.None);
        var plugin = new BundledCorePlugin();
        var registry = new PluginRegistry(plugin.Metadata);
        var executor = new ToolExecutor(coordinator);

        plugin.Register(registry);

        var refactorings = await InvokeAsync<CodeActionListData>(executor, registry, "list-code-actions", new Dictionary<string, JsonElement>
        {
            ["location"] = JsonSerializer.SerializeToElement(fixture.GetLocation("StateHolder")),
            ["expectedSnapshot"] = JsonSerializer.SerializeToElement(new SnapshotPrecondition
            {
                WorkspaceEpoch = open.WorkspaceEpoch!.Value,
                TransactionRevision = 0,
            }),
            ["includeCodeFixes"] = JsonSerializer.SerializeToElement(false),
        });
        var stageRefactoring = await InvokeAsync<MutationData>(executor, registry, "stage-code-action", new Dictionary<string, JsonElement>
        {
            ["actionId"] = JsonSerializer.SerializeToElement(refactorings.Data!.Actions.Single(static action => action.Title == "Apply test refactoring").ActionId),
            ["expectedSnapshot"] = JsonSerializer.SerializeToElement(new SnapshotPrecondition
            {
                WorkspaceEpoch = open.WorkspaceEpoch!.Value,
                TransactionRevision = 0,
            }),
        });
        var codeFixes = await InvokeAsync<CodeActionListData>(executor, registry, "list-code-actions", new Dictionary<string, JsonElement>
        {
            ["location"] = JsonSerializer.SerializeToElement(fixture.GetLocation("unused")),
            ["expectedSnapshot"] = JsonSerializer.SerializeToElement(new SnapshotPrecondition
            {
                WorkspaceEpoch = open.WorkspaceEpoch!.Value,
                TransactionRevision = 1,
            }),
            ["includeRefactorings"] = JsonSerializer.SerializeToElement(false),
        });
        var stageCodeFix = await InvokeAsync<MutationData>(executor, registry, "stage-code-fix", new Dictionary<string, JsonElement>
        {
            ["actionId"] = JsonSerializer.SerializeToElement(codeFixes.Data!.Actions.Single(static action => action.Title == "Apply test code fix").ActionId),
            ["expectedSnapshot"] = JsonSerializer.SerializeToElement(new SnapshotPrecondition
            {
                WorkspaceEpoch = open.WorkspaceEpoch!.Value,
                TransactionRevision = 1,
            }),
        });

        stageRefactoring.Outcome.Should().Be(ToolOutcome.Succeeded);
        stageRefactoring.TransactionRevision.Should().Be(1);
        stageCodeFix.Outcome.Should().Be(ToolOutcome.Succeeded);
        stageCodeFix.TransactionRevision.Should().Be(2);
    }

    [Fact]
    public async Task GIVEN_TestProviders_WHEN_StagingFixAll_THEN_ShouldStageAcrossTheRequestedScope()
    {
        using var fixture = await InspectionSampleFixture.CreateAsync();
        var coordinator = CreateCoordinator();
        var open = await coordinator.OpenAsync(new WorkspaceOpenRequest
        {
            Path = fixture.ProjectPath,
        }, CancellationToken.None);
        await coordinator.StartTransactionAsync(new TransactionStartRequest(), CancellationToken.None);
        var plugin = new BundledCorePlugin();
        var registry = new PluginRegistry(plugin.Metadata);
        var executor = new ToolExecutor(coordinator);

        plugin.Register(registry);

        var codeFixes = await InvokeAsync<CodeActionListData>(executor, registry, "list-code-actions", new Dictionary<string, JsonElement>
        {
            ["location"] = JsonSerializer.SerializeToElement(fixture.GetLocation("unused")),
            ["expectedSnapshot"] = JsonSerializer.SerializeToElement(new SnapshotPrecondition
            {
                WorkspaceEpoch = open.WorkspaceEpoch!.Value,
                TransactionRevision = 0,
            }),
            ["includeRefactorings"] = JsonSerializer.SerializeToElement(false),
        });
        var stageFixAll = await InvokeAsync<MutationData>(executor, registry, "stage-fix-all", new Dictionary<string, JsonElement>
        {
            ["actionId"] = JsonSerializer.SerializeToElement(codeFixes.Data!.Actions.Single(static action => action.Title == "Apply test code fix").ActionId),
            ["scope"] = JsonSerializer.SerializeToElement(new ScopeSelector
            {
                Kind = ScopeKind.Solution,
            }),
            ["expectedSnapshot"] = JsonSerializer.SerializeToElement(new SnapshotPrecondition
            {
                WorkspaceEpoch = open.WorkspaceEpoch!.Value,
                TransactionRevision = 0,
            }),
        });
        var preview = await coordinator.PreviewTransactionAsync(new TransactionPreviewRequest(), CancellationToken.None);
        var previewData = preview.Data!;
        var changedDocument = previewData.Documents.Single(change => change.Document!.Path == "Formatting.cs");

        stageFixAll.Outcome.Should().Be(ToolOutcome.Succeeded);
        stageFixAll.TransactionRevision.Should().Be(1);
        stageFixAll.Data!.Summary.Should().Be("Fix all: Apply test code fix");
        previewData.Documents.Should().NotBeEmpty();
        changedDocument.Document!.Path.Should().Be("Formatting.cs");
        changedDocument.Preview!.ChangedLines.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task GIVEN_TestProviders_WHEN_StagingFixAllWithAnInsufficientCap_THEN_ShouldRejectSafely()
    {
        using var fixture = await InspectionSampleFixture.CreateAsync();
        var coordinator = CreateCoordinator();
        var open = await coordinator.OpenAsync(new WorkspaceOpenRequest
        {
            Path = fixture.ProjectPath,
        }, CancellationToken.None);
        await coordinator.StartTransactionAsync(new TransactionStartRequest(), CancellationToken.None);
        var plugin = new BundledCorePlugin();
        var registry = new PluginRegistry(plugin.Metadata);
        var executor = new ToolExecutor(coordinator);

        plugin.Register(registry);

        var codeFixes = await InvokeAsync<CodeActionListData>(executor, registry, "list-code-actions", new Dictionary<string, JsonElement>
        {
            ["location"] = JsonSerializer.SerializeToElement(fixture.GetLocation("unused")),
            ["expectedSnapshot"] = JsonSerializer.SerializeToElement(new SnapshotPrecondition
            {
                WorkspaceEpoch = open.WorkspaceEpoch!.Value,
                TransactionRevision = 0,
            }),
            ["includeRefactorings"] = JsonSerializer.SerializeToElement(false),
        });
        var stageFixAll = await InvokeAsync<MutationData>(executor, registry, "stage-fix-all", new Dictionary<string, JsonElement>
        {
            ["actionId"] = JsonSerializer.SerializeToElement(codeFixes.Data!.Actions.Single(static action => action.Title == "Apply test code fix").ActionId),
            ["scope"] = JsonSerializer.SerializeToElement(new ScopeSelector
            {
                Kind = ScopeKind.Solution,
            }),
            ["maxChanges"] = JsonSerializer.SerializeToElement(0),
            ["expectedSnapshot"] = JsonSerializer.SerializeToElement(new SnapshotPrecondition
            {
                WorkspaceEpoch = open.WorkspaceEpoch!.Value,
                TransactionRevision = 0,
            }),
        }, expectProtocolSuccess: false);

        stageFixAll.Error!.Code.Should().Be("FixAllLimitExceeded");
    }

    [Fact]
    public async Task GIVEN_ActionTokenTamperingOrExpiry_WHEN_Staging_THEN_ShouldRejectWithActionExpired()
    {
        using var fixture = await InspectionSampleFixture.CreateAsync();
        var coordinator = CreateCoordinator(TimeSpan.FromMinutes(-1));
        var open = await coordinator.OpenAsync(new WorkspaceOpenRequest
        {
            Path = fixture.ProjectPath,
        }, CancellationToken.None);
        await coordinator.StartTransactionAsync(new TransactionStartRequest(), CancellationToken.None);
        var plugin = new BundledCorePlugin();
        var registry = new PluginRegistry(plugin.Metadata);
        var executor = new ToolExecutor(coordinator);

        plugin.Register(registry);

        var list = await InvokeAsync<CodeActionListData>(executor, registry, "list-code-actions", new Dictionary<string, JsonElement>
        {
            ["location"] = JsonSerializer.SerializeToElement(fixture.GetLocation("StateHolder")),
            ["expectedSnapshot"] = JsonSerializer.SerializeToElement(new SnapshotPrecondition
            {
                WorkspaceEpoch = open.WorkspaceEpoch!.Value,
                TransactionRevision = 0,
            }),
        });
        var expired = await InvokeAsync<MutationData>(executor, registry, "stage-code-action", new Dictionary<string, JsonElement>
        {
            ["actionId"] = JsonSerializer.SerializeToElement(list.Data!.Actions[0].ActionId),
            ["expectedSnapshot"] = JsonSerializer.SerializeToElement(new SnapshotPrecondition
            {
                WorkspaceEpoch = open.WorkspaceEpoch!.Value,
                TransactionRevision = 0,
            }),
        }, expectProtocolSuccess: false);
        var token = list.Data.Actions[0].ActionId;
        var tampered = await InvokeAsync<MutationData>(executor, registry, "stage-code-action", new Dictionary<string, JsonElement>
        {
            ["actionId"] = JsonSerializer.SerializeToElement(token[..^1] + (token[^1] == 'A' ? "B" : "A")),
            ["expectedSnapshot"] = JsonSerializer.SerializeToElement(new SnapshotPrecondition
            {
                WorkspaceEpoch = open.WorkspaceEpoch!.Value,
                TransactionRevision = 0,
            }),
        }, expectProtocolSuccess: false);

        expired.Error!.Code.Should().Be("ActionExpired");
        tampered.Error!.Code.Should().Be("ActionExpired");
    }

    [Fact]
    public async Task GIVEN_StaleActionTokenOrUnsupportedOperations_WHEN_Staging_THEN_ShouldRejectSafely()
    {
        using var fixture = await InspectionSampleFixture.CreateAsync();
        var coordinator = CreateCoordinator();
        var open = await coordinator.OpenAsync(new WorkspaceOpenRequest
        {
            Path = fixture.ProjectPath,
        }, CancellationToken.None);
        await coordinator.StartTransactionAsync(new TransactionStartRequest(), CancellationToken.None);
        var plugin = new BundledCorePlugin();
        var registry = new PluginRegistry(plugin.Metadata);
        var executor = new ToolExecutor(coordinator);

        plugin.Register(registry);

        var list = await InvokeAsync<CodeActionListData>(executor, registry, "list-code-actions", new Dictionary<string, JsonElement>
        {
            ["location"] = JsonSerializer.SerializeToElement(fixture.GetLocation("StateHolder")),
            ["expectedSnapshot"] = JsonSerializer.SerializeToElement(new SnapshotPrecondition
            {
                WorkspaceEpoch = open.WorkspaceEpoch!.Value,
                TransactionRevision = 0,
            }),
        });
        var applyToken = list.Data!.Actions.Single(static action => action.Title == "Apply test refactoring").ActionId;
        var staleToken = list.Data.Actions.Single(static action => action.Title == "Retain test state").ActionId;

        var applied = await InvokeAsync<MutationData>(executor, registry, "stage-code-action", new Dictionary<string, JsonElement>
        {
            ["actionId"] = JsonSerializer.SerializeToElement(applyToken),
            ["expectedSnapshot"] = JsonSerializer.SerializeToElement(new SnapshotPrecondition
            {
                WorkspaceEpoch = open.WorkspaceEpoch!.Value,
                TransactionRevision = 0,
            }),
        });
        var refreshedList = await InvokeAsync<CodeActionListData>(executor, registry, "list-code-actions", new Dictionary<string, JsonElement>
        {
            ["location"] = JsonSerializer.SerializeToElement(fixture.GetLocation("StateHolder")),
            ["expectedSnapshot"] = JsonSerializer.SerializeToElement(new SnapshotPrecondition
            {
                WorkspaceEpoch = open.WorkspaceEpoch!.Value,
                TransactionRevision = 1,
            }),
        });
        var unsupportedToken = refreshedList.Data!.Actions.Single(static action => action.Title == "Unsupported test refactoring").ActionId;
        var stale = await InvokeAsync<MutationData>(executor, registry, "stage-code-action", new Dictionary<string, JsonElement>
        {
            ["actionId"] = JsonSerializer.SerializeToElement(staleToken),
            ["expectedSnapshot"] = JsonSerializer.SerializeToElement(new SnapshotPrecondition
            {
                WorkspaceEpoch = open.WorkspaceEpoch!.Value,
                TransactionRevision = 1,
            }),
        }, expectProtocolSuccess: false);
        var unsupported = await InvokeAsync<MutationData>(executor, registry, "stage-code-action", new Dictionary<string, JsonElement>
        {
            ["actionId"] = JsonSerializer.SerializeToElement(unsupportedToken),
            ["expectedSnapshot"] = JsonSerializer.SerializeToElement(new SnapshotPrecondition
            {
                WorkspaceEpoch = open.WorkspaceEpoch!.Value,
                TransactionRevision = 1,
            }),
        }, expectProtocolSuccess: false);

        applied.Outcome.Should().Be(ToolOutcome.Succeeded);
        stale.Error!.Code.Should().Be("ActionExpired");
        unsupported.Error!.Code.Should().Be("UnsupportedActionOperation");
    }

    [Fact]
    public async Task GIVEN_SnapshotMismatch_WHEN_ListingOrStagingCodeActions_THEN_ShouldRejectWithSnapshotMismatch()
    {
        using var fixture = await InspectionSampleFixture.CreateAsync();
        var coordinator = CreateCoordinator();
        var open = await coordinator.OpenAsync(new WorkspaceOpenRequest
        {
            Path = fixture.ProjectPath,
        }, CancellationToken.None);
        await coordinator.StartTransactionAsync(new TransactionStartRequest(), CancellationToken.None);
        var plugin = new BundledCorePlugin();
        var registry = new PluginRegistry(plugin.Metadata);
        var executor = new ToolExecutor(coordinator);

        plugin.Register(registry);

        var mismatchedSnapshot = new SnapshotPrecondition
        {
            WorkspaceEpoch = open.WorkspaceEpoch!.Value + 1,
            TransactionRevision = 0,
        };
        var list = await InvokeAsync<CodeActionListData>(executor, registry, "list-code-actions", new Dictionary<string, JsonElement>
        {
            ["location"] = JsonSerializer.SerializeToElement(fixture.GetLocation("StateHolder")),
            ["expectedSnapshot"] = JsonSerializer.SerializeToElement(mismatchedSnapshot),
        }, expectProtocolSuccess: false);
        var validList = await InvokeAsync<CodeActionListData>(executor, registry, "list-code-actions", new Dictionary<string, JsonElement>
        {
            ["location"] = JsonSerializer.SerializeToElement(fixture.GetLocation("StateHolder")),
            ["expectedSnapshot"] = JsonSerializer.SerializeToElement(new SnapshotPrecondition
            {
                WorkspaceEpoch = open.WorkspaceEpoch!.Value,
                TransactionRevision = 0,
            }),
        });
        var stage = await InvokeAsync<MutationData>(executor, registry, "stage-code-action", new Dictionary<string, JsonElement>
        {
            ["actionId"] = JsonSerializer.SerializeToElement(validList.Data!.Actions[0].ActionId),
            ["expectedSnapshot"] = JsonSerializer.SerializeToElement(mismatchedSnapshot),
        }, expectProtocolSuccess: false);

        list.Error!.Code.Should().Be("SnapshotMismatch");
        stage.Error!.Code.Should().Be("SnapshotMismatch");
    }

    [Fact]
    public async Task GIVEN_BuiltInCodeActions_WHEN_InvokingRemoveUnusedUsings_THEN_ShouldReturnStructuredMutation()
    {
        using var fixture = await InspectionSampleFixture.CreateAsync();
        var coordinator = CreateBuiltInCoordinator();
        var open = await coordinator.OpenAsync(new WorkspaceOpenRequest
        {
            Path = fixture.ProjectPath,
        }, CancellationToken.None);
        await coordinator.StartTransactionAsync(new TransactionStartRequest(), CancellationToken.None);
        var plugin = new BundledCorePlugin();
        var registry = new PluginRegistry(plugin.Metadata);
        var executor = new ToolExecutor(coordinator);

        plugin.Register(registry);

        var result = await InvokeAsync<MutationData>(executor, registry, "remove-unused-usings", new Dictionary<string, JsonElement>
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
                WorkspaceEpoch = open.WorkspaceEpoch!.Value,
                TransactionRevision = 0,
            }),
        });

        result.Outcome.Should().Be(ToolOutcome.Succeeded);
        result.TransactionRevision.Should().Be(1);
        result.Data!.Operation.Should().Be("remove-unused-usings");
    }

    [Fact]
    public async Task GIVEN_BuiltInCodeActions_WHEN_InvokingAddMissingUsingsWithGlobalPreference_THEN_ShouldRejectClearly()
    {
        using var fixture = await InspectionSampleFixture.CreateAsync();
        var coordinator = CreateBuiltInCoordinator();
        var open = await coordinator.OpenAsync(new WorkspaceOpenRequest
        {
            Path = fixture.ProjectPath,
        }, CancellationToken.None);
        await coordinator.StartTransactionAsync(new TransactionStartRequest(), CancellationToken.None);
        var plugin = new BundledCorePlugin();
        var registry = new PluginRegistry(plugin.Metadata);
        var executor = new ToolExecutor(coordinator);

        plugin.Register(registry);

        var result = await InvokeAsync<MutationData>(executor, registry, "add-missing-usings", new Dictionary<string, JsonElement>
        {
            ["scope"] = JsonSerializer.SerializeToElement(new ScopeSelector
            {
                Kind = ScopeKind.Document,
                Document = new DocumentSelector
                {
                    Path = "MissingUsings.cs",
                },
            }),
            ["preferGlobalUsings"] = JsonSerializer.SerializeToElement(true),
            ["expectedSnapshot"] = JsonSerializer.SerializeToElement(new SnapshotPrecondition
            {
                WorkspaceEpoch = open.WorkspaceEpoch!.Value,
                TransactionRevision = 0,
            }),
        }, expectProtocolSuccess: false);

        result.Error!.Code.Should().Be("UnsupportedOption");
    }

    private static IWorkspaceCoordinator CreateCoordinator(TimeSpan? tokenLifetime = null)
    {
        var runtime = CodeActionRuntimeFactory.Create(new CodeActionRuntimeOptions
        {
            TokenLifetime = tokenLifetime ?? TimeSpan.FromMinutes(5),
            IncludeBuiltInAssemblies = false,
            AdditionalAssemblies =
            [
                typeof(TestRefactoringProvider).Assembly,
            ],
        });

        return WorkspaceCoordinatorFactory.Create(new WorkspaceCoordinatorOptions
        {
            CodeActionService = runtime.CodeActionService,
            WorkspaceHostServices = runtime.WorkspaceHostServices,
            DefaultMaxResults = 100,
            MaxConcurrentQueries = 2,
            MaxResponseBytes = 65536,
        });
    }

    private static IWorkspaceCoordinator CreateBuiltInCoordinator()
    {
        var runtime = CodeActionRuntimeFactory.Create(new CodeActionRuntimeOptions
        {
            IncludeBuiltInAssemblies = true,
        });

        return WorkspaceCoordinatorFactory.Create(new WorkspaceCoordinatorOptions
        {
            CodeActionService = runtime.CodeActionService,
            WorkspaceHostServices = runtime.WorkspaceHostServices,
            DefaultMaxResults = 100,
            MaxConcurrentQueries = 2,
            MaxResponseBytes = 65536,
        });
    }

    public static TheoryData<BuiltInCodeActionAuditCase> GetPromotedDraftValidationCandidates()
    {
        return CreateTheoryData(BuiltInCodeActionAuditCases.PromotedDraftValidationCandidates);
    }

    public static TheoryData<BuiltInCodeActionAuditCase> GetPendingPromotionCandidates()
    {
        return CreateTheoryData(BuiltInCodeActionAuditCases.PendingPromotionCandidates);
    }

    private static TheoryData<BuiltInCodeActionAuditCase> CreateTheoryData(IReadOnlyList<BuiltInCodeActionAuditCase> auditCases)
    {
        var data = new TheoryData<BuiltInCodeActionAuditCase>();

        foreach (var auditCase in auditCases)
        {
            data.Add(auditCase);
        }

        return data;
    }

    private static bool MatchesAuditCase(CodeActionInfo action, BuiltInCodeActionAuditCase auditCase)
    {
        if (!string.Equals(action.ProviderId, auditCase.ProviderId, StringComparison.Ordinal))
        {
            return false;
        }

        if (!string.IsNullOrWhiteSpace(auditCase.Title))
        {
            return string.Equals(action.Title, auditCase.Title, StringComparison.Ordinal);
        }

        if (!string.IsNullOrWhiteSpace(auditCase.TitlePrefix))
        {
            return action.Title.StartsWith(auditCase.TitlePrefix, StringComparison.Ordinal);
        }

        return true;
    }

    private static async Task<ToolResult<TResponse>> InvokeAsync<TResponse>(
        ToolExecutor executor,
        PluginRegistry registry,
        string toolName,
        IDictionary<string, JsonElement> arguments,
        bool expectProtocolSuccess = true)
    {
        var registeredTool = registry.RegisteredTools.Single(tool => tool.Metadata.Name == toolName);
        var serverTool = new PluginMcpServerTool(registeredTool, executor);
        var result = await serverTool.InvokeAsync(
            new RequestContext<CallToolRequestParams>(
                CreateServer(),
                new JsonRpcRequest
                {
                    Method = "tools/call",
                },
                new CallToolRequestParams
                {
                    Name = toolName,
                    Arguments = arguments,
                }),
            CancellationToken.None);

        result.IsError.Should().Be(!expectProtocolSuccess);

        return JsonSerializer.Deserialize<ToolResult<TResponse>>(result.StructuredContent!.Value.GetRawText(), _serializerOptions)!;
    }

    private static McpServer CreateServer()
    {
        var asyncDisposable = new Mock<IAsyncDisposable>();
        var server = new Mock<McpServer>();

        asyncDisposable.Setup(static disposable => disposable.DisposeAsync()).Returns(ValueTask.CompletedTask);
        server.SetupGet(static value => value.ClientCapabilities).Returns(new ClientCapabilities());
        server.SetupGet(static value => value.ClientInfo).Returns(new Implementation
        {
            Name = "Test Client",
            Version = "1.0.0",
        });
        server.SetupGet(static value => value.ServerOptions).Returns(new McpServerOptions());
        server.SetupGet(static value => value.Services).Returns(Mock.Of<IServiceProvider>());
        server.SetupGet(static value => value.LoggingLevel).Returns((LoggingLevel?)null);
        server.SetupGet(static value => value.SessionId).Returns("session");
        server.SetupGet(static value => value.NegotiatedProtocolVersion).Returns("2025-06-18");
        server.Setup(static value => value.RunAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        server
            .Setup(static value => value.SendRequestAsync(It.IsAny<JsonRpcRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new JsonRpcResponse
            {
                Result = new JsonObject(),
            });
        server
            .Setup(static value => value.SendMessageAsync(It.IsAny<JsonRpcMessage>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        server
            .Setup(static value => value.RegisterNotificationHandler(It.IsAny<string>(), It.IsAny<Func<JsonRpcNotification, CancellationToken, ValueTask>>()))
            .Returns(asyncDisposable.Object);
        server.Setup(static value => value.DisposeAsync()).Returns(ValueTask.CompletedTask);

        return server.Object;
    }
}
