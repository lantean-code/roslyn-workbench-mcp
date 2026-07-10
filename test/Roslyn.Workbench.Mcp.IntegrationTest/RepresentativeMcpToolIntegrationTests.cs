using System.Text.Json;
namespace Roslyn.Workbench.Mcp.Test;

public sealed class RepresentativeMcpToolIntegrationTests
{
    [Fact]
    public async Task GIVEN_InspectionWorkspace_WHEN_InvokingRepresentativeQueryThroughMcp_THEN_ShouldReturnStructuredResult()
    {
        using var fixture = await InspectionSampleFixture.CreateAsync();
        var coordinator = WorkspaceCoordinatorFactory.Create(toolExecutionServices: BundledCoreToolExecutionServicesFactory.Create());
        var openResult = await coordinator.OpenAsync(new WorkspaceOpenRequest
        {
            Path = fixture.ProjectPath,
        }, CancellationToken.None);
        var registry = BundledPluginRegistryFactory.CreateRegistry();

        var result = await McpIntegrationTestHost.InvokePluginToolAsync<DiagnosticsData>(
            coordinator,
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
        using var fixture = await InspectionSampleFixture.CreateAsync();
        var codeActionRuntime = new CodeActionRuntimeComposer()
            .Compose(new CodeActionRuntimeOptions
            {
                IncludeBuiltInAssemblies = false,
                AdditionalAssemblies =
                [
                    typeof(TestRefactoringProvider).Assembly,
                ],
            });
        var coordinator = WorkspaceCoordinatorFactory.CreateWithCodeActionRuntime(
            codeActionRuntime,
            BundledCoreToolExecutionServicesFactory.Create());
        var openResult = await coordinator.OpenAsync(new WorkspaceOpenRequest
        {
            Path = fixture.ProjectPath,
        }, CancellationToken.None);
        await coordinator.StartTransactionAsync(new TransactionStartRequest(), CancellationToken.None);
        var registry = BundledPluginRegistryFactory.CreateRegistry();
        var snapshot = new SnapshotPrecondition
        {
            WorkspaceEpoch = openResult.WorkspaceEpoch!.Value,
            TransactionRevision = 0,
        };

        var listed = await McpIntegrationTestHost.InvokePluginToolAsync<CodeActionListData>(
            coordinator,
            registry,
            "list-code-actions",
            new Dictionary<string, JsonElement>
            {
                ["location"] = JsonSerializer.SerializeToElement(fixture.GetLocation("StateHolder")),
                ["expectedSnapshot"] = JsonSerializer.SerializeToElement(snapshot),
                ["includeCodeFixes"] = JsonSerializer.SerializeToElement(false),
            });
        var action = listed.Data!.Actions.Single(static candidate => candidate.Title == "Apply test refactoring");
        var staged = await McpIntegrationTestHost.InvokePluginToolAsync<MutationData>(
            coordinator,
            registry,
            "stage-code-action",
            new Dictionary<string, JsonElement>
            {
                ["actionId"] = JsonSerializer.SerializeToElement(action.ActionId),
                ["expectedSnapshot"] = JsonSerializer.SerializeToElement(snapshot),
            });
        var preview = await coordinator.PreviewTransactionAsync(new TransactionPreviewRequest(), CancellationToken.None);

        listed.Outcome.Should().Be(ToolOutcome.Succeeded);
        staged.Outcome.Should().Be(ToolOutcome.Succeeded);
        staged.Data!.Transaction!.Revision.Should().Be(1);
        preview.Data!.Documents.Should().ContainSingle(static document => document.Document!.Path == "Formatting.cs");
    }
}
