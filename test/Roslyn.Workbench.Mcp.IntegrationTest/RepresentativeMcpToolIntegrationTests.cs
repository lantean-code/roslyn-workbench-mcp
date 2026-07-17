using System.Text.Json;

namespace Roslyn.Workbench.Mcp.Test;

public sealed class RepresentativeMcpToolIntegrationTests
{
    [Fact]
    public async Task GIVEN_InspectionWorkspace_WHEN_InvokingRepresentativeQueryThroughMcp_THEN_ShouldReturnStructuredResult()
    {
        await using var fixture = await InspectionSampleFixture.CreateAsync();
        await using var coordinator = WorkspaceCoordinatorFactory.Create(toolExecutionServices: BundledCoreToolExecutionServicesFactory.Create());
        var openResult = await coordinator.OpenAsync(new WorkspaceOpenRequest
        {
            Path = fixture.ProjectPath,
        }, TestContext.Current.CancellationToken);
        var registry = BundledPluginCatalogueFactory.CreateCatalogue();

        var result = await McpIntegrationTestHost.InvokePluginToolAsync<DiagnosticsData>(
            coordinator,
            TestContext.Current.CancellationToken,
            registry,
            "get-diagnostics",
            new Dictionary<string, JsonElement>
            {
                ["expectedSnapshot"] = JsonSerializer.SerializeToElement(new SnapshotPrecondition
                {
                    WorkspaceEpoch = openResult.WorkspaceEpoch!.Value,
                }),
            });

        result.Outcome.Should().Be(ToolOutcome.Succeeded);
        result.Data!.Diagnostics.Items.Should().Contain(static diagnostic => diagnostic.Id == "CS0219");
    }

    [Fact]
    public async Task GIVEN_ControlledCodeActionProvider_WHEN_ListingAndStagingThroughMcp_THEN_ShouldStageRepresentativeCodeAction()
    {
        await using var fixture = await InspectionSampleFixture.CreateAsync();
        var codeActionProviderCatalog = CodeActionProviderCatalogFactory.Create(new CodeActionCompositionOptions
        {
            IncludeBuiltInAssemblies = false,
            AdditionalAssemblies =
                [
                    typeof(TestRefactoringProvider).Assembly,
                ],
        });
        await using var coordinator = WorkspaceCoordinatorFactory.CreateWithCodeActionProviderCatalog(
            codeActionProviderCatalog,
            BundledCoreToolExecutionServicesFactory.Create());
        var openResult = await coordinator.OpenAsync(new WorkspaceOpenRequest
        {
            Path = fixture.ProjectPath,
        }, TestContext.Current.CancellationToken);
        await coordinator.StartTransactionAsync(new TransactionStartRequest(), TestContext.Current.CancellationToken);
        var snapshot = new SnapshotPrecondition
        {
            WorkspaceEpoch = openResult.WorkspaceEpoch!.Value,
            TransactionRevision = 0,
        };

        var listed = await CodeActionToolTestHarness.InvokeAsync<CodeActionListData>(
            coordinator,
            TestContext.Current.CancellationToken,
            "list-code-actions",
            new Dictionary<string, JsonElement>
            {
                ["location"] = JsonSerializer.SerializeToElement(fixture.GetLocation("StateHolder")),
                ["expectedSnapshot"] = JsonSerializer.SerializeToElement(snapshot),
                ["includeCodeFixes"] = JsonSerializer.SerializeToElement(false),
            });
        var action = listed.Data!.Actions.Single(static candidate => candidate.Title == "Apply test refactoring");
        var staged = await CodeActionToolTestHarness.InvokeAsync<MutationData>(
            coordinator,
            TestContext.Current.CancellationToken,
            "stage-code-action",
            new Dictionary<string, JsonElement>
            {
                ["actionId"] = JsonSerializer.SerializeToElement(action.ActionId),
                ["expectedSnapshot"] = JsonSerializer.SerializeToElement(snapshot),
            });
        var preview = await coordinator.PreviewTransactionAsync(new TransactionPreviewRequest(), TestContext.Current.CancellationToken);

        listed.Outcome.Should().Be(ToolOutcome.Succeeded);
        staged.Outcome.Should().Be(ToolOutcome.Succeeded);
        staged.Data!.Transaction!.Revision.Should().Be(1);
        preview.Data!.Documents.Should().ContainSingle(static document => document.Document!.Path == "Formatting.cs");
    }
}
