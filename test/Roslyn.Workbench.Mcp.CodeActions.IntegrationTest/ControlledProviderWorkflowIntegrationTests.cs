using System.Text.Json;

namespace Roslyn.Workbench.Mcp.CodeActions.Test;

public sealed class ControlledProviderWorkflowIntegrationTests
{
    [Fact]
    public async Task GIVEN_ControlledProviderActions_WHEN_ListingDescribingAndStagingParameterisedAction_THEN_ShouldPreserveWorkflowContracts()
    {
        using var fixture = await InspectionSampleFixture.CreateAsync();
        var coordinator = BundledCoreToolTestHarness.CreateTestCodeActionCoordinator();
        var open = await coordinator.OpenAsync(new WorkspaceOpenRequest
        {
            Path = fixture.ProjectPath,
        }, CancellationToken.None);
        await coordinator.StartTransactionAsync(new TransactionStartRequest(), CancellationToken.None);
        var snapshot = BundledCoreToolTestHarness.CreateSnapshot(open, 0);

        var listed = await InvokeAsync<CodeActionListData>(coordinator, "list-code-actions", new Dictionary<string, JsonElement>
        {
            ["location"] = JsonSerializer.SerializeToElement(fixture.GetLocation("StateHolder")),
            ["expectedSnapshot"] = JsonSerializer.SerializeToElement(snapshot),
        });
        var parameterisedAction = listed.Data!.Actions.Single(static action => action.Title == "Change signature test refactoring");
        var described = await InvokeAsync<DescribeCodeActionData>(coordinator, "describe-code-action", new Dictionary<string, JsonElement>
        {
            ["actionId"] = JsonSerializer.SerializeToElement(parameterisedAction.ActionId),
            ["expectedSnapshot"] = JsonSerializer.SerializeToElement(snapshot),
        });
        var staged = await InvokeAsync<MutationData>(coordinator, "stage-code-action", new Dictionary<string, JsonElement>
        {
            ["actionId"] = JsonSerializer.SerializeToElement(parameterisedAction.ActionId),
            ["expectedSnapshot"] = JsonSerializer.SerializeToElement(snapshot),
        }, expectProtocolSuccess: false);

        listed.Data.Actions.Should().OnlyContain(static action => !string.IsNullOrWhiteSpace(action.ActionId));
        described.Outcome.Should().Be(ToolOutcome.Succeeded);
        described.Data!.Descriptor.Title.Should().Be("Change signature test refactoring");
        described.Data.Context.Kind.Should().Be(CodeActionDescriptorContextKind.SignaturePlan);
        staged.Outcome.Should().Be(ToolOutcome.Rejected);
        staged.Error!.Code.Should().Be("ActionRequiresParameters");
    }

    [Fact]
    public async Task GIVEN_ControlledRefactoringAndCodeFix_WHEN_StagingBoth_THEN_ShouldAdvanceRevisionsAndPreviewChanges()
    {
        using var fixture = await InspectionSampleFixture.CreateAsync();
        var coordinator = BundledCoreToolTestHarness.CreateTestCodeActionCoordinator();
        var open = await coordinator.OpenAsync(new WorkspaceOpenRequest
        {
            Path = fixture.ProjectPath,
        }, CancellationToken.None);
        await coordinator.StartTransactionAsync(new TransactionStartRequest(), CancellationToken.None);

        var refactorings = await ListActionsAsync(coordinator, fixture.GetLocation("StateHolder"), open, 0, includeCodeFixes: false);
        var stagedRefactoring = await InvokeAsync<MutationData>(coordinator, "stage-code-action", new Dictionary<string, JsonElement>
        {
            ["actionId"] = JsonSerializer.SerializeToElement(refactorings.Data!.Actions.Single(static action => action.Title == "Apply test refactoring").ActionId),
            ["expectedSnapshot"] = JsonSerializer.SerializeToElement(BundledCoreToolTestHarness.CreateSnapshot(open, 0)),
        });
        var codeFixes = await ListActionsAsync(coordinator, fixture.GetLocation("unused"), open, 1, includeRefactorings: false);
        var stagedCodeFix = await InvokeAsync<MutationData>(coordinator, "stage-code-fix", new Dictionary<string, JsonElement>
        {
            ["actionId"] = JsonSerializer.SerializeToElement(codeFixes.Data!.Actions.Single(static action => action.Title == "Apply test code fix").ActionId),
            ["expectedSnapshot"] = JsonSerializer.SerializeToElement(BundledCoreToolTestHarness.CreateSnapshot(open, 1)),
        });
        var preview = await coordinator.PreviewTransactionAsync(new TransactionPreviewRequest(), CancellationToken.None);

        stagedRefactoring.Data!.Transaction!.Revision.Should().Be(1);
        stagedCodeFix.Data!.Transaction!.Revision.Should().Be(2);
        preview.Data!.Transaction!.Revision.Should().Be(2);
        preview.Data.Documents.Should().ContainSingle(static change => change.Document!.Path == "Formatting.cs");
    }

    [Theory]
    [InlineData(ScopeKind.Document)]
    [InlineData(ScopeKind.Project)]
    [InlineData(ScopeKind.Solution)]
    public async Task GIVEN_ControlledCodeFix_WHEN_StagingFixAllAtSupportedScope_THEN_ShouldStageRequestedScope(ScopeKind scopeKind)
    {
        using var fixture = await InspectionSampleFixture.CreateAsync();
        var coordinator = BundledCoreToolTestHarness.CreateTestCodeActionCoordinator();
        var open = await coordinator.OpenAsync(new WorkspaceOpenRequest
        {
            Path = fixture.ProjectPath,
        }, CancellationToken.None);
        await coordinator.StartTransactionAsync(new TransactionStartRequest(), CancellationToken.None);
        var codeFixes = await ListActionsAsync(coordinator, fixture.GetLocation("unused"), open, 0, includeRefactorings: false);

        var result = await InvokeAsync<MutationData>(coordinator, "stage-fix-all", new Dictionary<string, JsonElement>
        {
            ["actionId"] = JsonSerializer.SerializeToElement(codeFixes.Data!.Actions.Single(static action => action.Title == "Apply test code fix").ActionId),
            ["scope"] = JsonSerializer.SerializeToElement(CreateScope(scopeKind)),
            ["expectedSnapshot"] = JsonSerializer.SerializeToElement(BundledCoreToolTestHarness.CreateSnapshot(open, 0)),
        });

        result.Outcome.Should().Be(ToolOutcome.Succeeded);
        result.Data!.Summary.Should().Be("Fix all: Apply test code fix");
        result.Data.Transaction!.Revision.Should().Be(1);
    }

    [Fact]
    public async Task GIVEN_TamperedExpiredOrStaleActionTokens_WHEN_Staging_THEN_ShouldRejectEachToken()
    {
        using var tamperedFixture = await InspectionSampleFixture.CreateAsync();
        var tamperedCoordinator = BundledCoreToolTestHarness.CreateTestCodeActionCoordinator();
        var tamperedOpen = await tamperedCoordinator.OpenAsync(new WorkspaceOpenRequest
        {
            Path = tamperedFixture.ProjectPath,
        }, CancellationToken.None);
        await tamperedCoordinator.StartTransactionAsync(new TransactionStartRequest(), CancellationToken.None);
        var tamperedActions = await ListActionsAsync(tamperedCoordinator, tamperedFixture.GetLocation("StateHolder"), tamperedOpen, 0, includeCodeFixes: false);
        var actionId = tamperedActions.Data!.Actions.Single(static action => action.Title == "Apply test refactoring").ActionId;

        var tampered = await StageCodeActionAsync(tamperedCoordinator, string.Concat(actionId, "tampered"), tamperedOpen, 0, expectProtocolSuccess: false);

        using var expiredFixture = await InspectionSampleFixture.CreateAsync();
        var expiredCoordinator = BundledCoreToolTestHarness.CreateTestCodeActionCoordinator(TimeSpan.FromMinutes(-1));
        var expiredOpen = await expiredCoordinator.OpenAsync(new WorkspaceOpenRequest
        {
            Path = expiredFixture.ProjectPath,
        }, CancellationToken.None);
        await expiredCoordinator.StartTransactionAsync(new TransactionStartRequest(), CancellationToken.None);
        var expiredActions = await ListActionsAsync(expiredCoordinator, expiredFixture.GetLocation("StateHolder"), expiredOpen, 0, includeCodeFixes: false);
        var expiredActionId = expiredActions.Data!.Actions.Single(static action => action.Title == "Apply test refactoring").ActionId;

        var expired = await StageCodeActionAsync(expiredCoordinator, expiredActionId, expiredOpen, 0, expectProtocolSuccess: false);

        using var staleFixture = await InspectionSampleFixture.CreateAsync();
        var staleCoordinator = BundledCoreToolTestHarness.CreateTestCodeActionCoordinator();
        var staleOpen = await staleCoordinator.OpenAsync(new WorkspaceOpenRequest
        {
            Path = staleFixture.ProjectPath,
        }, CancellationToken.None);
        await staleCoordinator.StartTransactionAsync(new TransactionStartRequest(), CancellationToken.None);
        var staleActions = await ListActionsAsync(staleCoordinator, staleFixture.GetLocation("StateHolder"), staleOpen, 0, includeCodeFixes: false);
        var staleActionId = staleActions.Data!.Actions.Single(static action => action.Title == "Apply test refactoring").ActionId;
        await StageCodeActionAsync(staleCoordinator, staleActionId, staleOpen, 0);

        var stale = await StageCodeActionAsync(staleCoordinator, staleActionId, staleOpen, 1, expectProtocolSuccess: false);

        tampered.Error!.Code.Should().Be("ActionExpired");
        expired.Error!.Code.Should().Be("ActionExpired");
        stale.Error!.Code.Should().Be("ActionExpired");
    }

    [Fact]
    public async Task GIVEN_StaleSnapshot_WHEN_ListingControlledActions_THEN_ShouldRejectSnapshotMismatch()
    {
        using var fixture = await InspectionSampleFixture.CreateAsync();
        var coordinator = BundledCoreToolTestHarness.CreateTestCodeActionCoordinator();
        var open = await coordinator.OpenAsync(new WorkspaceOpenRequest
        {
            Path = fixture.ProjectPath,
        }, CancellationToken.None);

        var result = await InvokeAsync<CodeActionListData>(coordinator, "list-code-actions", new Dictionary<string, JsonElement>
        {
            ["location"] = JsonSerializer.SerializeToElement(fixture.GetLocation("StateHolder")),
            ["expectedSnapshot"] = JsonSerializer.SerializeToElement(new SnapshotPrecondition
            {
                WorkspaceEpoch = open.WorkspaceEpoch!.Value + 1,
            }),
        }, expectProtocolSuccess: false);

        result.Outcome.Should().Be(ToolOutcome.Rejected);
        result.Error!.Code.Should().Be("SnapshotMismatch");
    }

    private static ScopeSelector CreateScope(ScopeKind scopeKind)
    {
        return new ScopeSelector
        {
            Kind = scopeKind,
            Document = scopeKind == ScopeKind.Document
                ? new DocumentSelector
                {
                    Path = "Formatting.cs",
                }
                : null,
            Project = scopeKind == ScopeKind.Project
                ? new ProjectSelector
                {
                    Path = "Sample.csproj",
                }
                : null,
        };
    }

    private static async Task<ToolResult<CodeActionListData>> ListActionsAsync(
        IWorkspaceRuntime coordinator,
        LocationSelector location,
        ToolResult<WorkspaceOpenData> open,
        int transactionRevision,
        bool includeRefactorings = true,
        bool includeCodeFixes = true)
    {
        return await InvokeAsync<CodeActionListData>(coordinator, "list-code-actions", new Dictionary<string, JsonElement>
        {
            ["location"] = JsonSerializer.SerializeToElement(location),
            ["includeRefactorings"] = JsonSerializer.SerializeToElement(includeRefactorings),
            ["includeCodeFixes"] = JsonSerializer.SerializeToElement(includeCodeFixes),
            ["expectedSnapshot"] = JsonSerializer.SerializeToElement(BundledCoreToolTestHarness.CreateSnapshot(open, transactionRevision)),
        });
    }

    private static async Task<ToolResult<MutationData>> StageCodeActionAsync(
        IWorkspaceRuntime coordinator,
        string actionId,
        ToolResult<WorkspaceOpenData> open,
        int transactionRevision,
        bool expectProtocolSuccess = true)
    {
        return await InvokeAsync<MutationData>(coordinator, "stage-code-action", new Dictionary<string, JsonElement>
        {
            ["actionId"] = JsonSerializer.SerializeToElement(actionId),
            ["expectedSnapshot"] = JsonSerializer.SerializeToElement(BundledCoreToolTestHarness.CreateSnapshot(open, transactionRevision)),
        }, expectProtocolSuccess);
    }

    private static async Task<ToolResult<TResponse>> InvokeAsync<TResponse>(
        IWorkspaceRuntime coordinator,
        string toolName,
        IDictionary<string, JsonElement> arguments,
        bool expectProtocolSuccess = true)
    {
        return await CodeActionToolTestHarness.InvokeAsync<TResponse>(coordinator, toolName, arguments, expectProtocolSuccess);
    }
}
