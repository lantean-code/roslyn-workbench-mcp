namespace Roslyn.Workbench.Mcp.Plugins.Core.Test.Inspection;

public sealed class GetOperationTreeToolTests
{
    [Fact]
    public async Task GIVEN_InspectionWorkspace_WHEN_ExecutingTool_THEN_ShouldReturnOperationTree()
    {
        using var fixture = await InspectionSampleFixture.CreateAsync();
        var coordinator = BundledCoreToolTestHarness.CreateInspectionCoordinator();
        var openResult = await coordinator.OpenAsync(new WorkspaceOpenRequest
        {
            Path = fixture.ProjectPath,
        }, CancellationToken.None);
        var target = new GetOperationTreeTool();

        var result = await BundledCoreToolTestHarness.ExecuteSingletonQueryAsync(coordinator, "get-operation-tree", target, new GetOperationTreeRequest
        {
            Location = fixture.GetLocation("formatter.Format(\"hi\")"),
            ExpectedSnapshot = BundledCoreToolTestHarness.CreateSnapshot(openResult),
        });

        result.Outcome.Should().Be(ToolOutcome.Succeeded);
        result.Data!.Root!.Kind.Should().Contain("Invocation");
    }
}
