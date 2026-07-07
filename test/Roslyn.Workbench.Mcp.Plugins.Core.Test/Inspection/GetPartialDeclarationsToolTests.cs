namespace Roslyn.Workbench.Mcp.Plugins.Core.Test.Inspection;

public sealed class GetPartialDeclarationsToolTests
{
    [Fact]
    public async Task GIVEN_InspectionWorkspace_WHEN_ExecutingTool_THEN_ShouldReturnDeclarations()
    {
        using var fixture = await InspectionSampleFixture.CreateAsync();
        var coordinator = BundledCoreToolTestHarness.CreateInspectionCoordinator();
        await coordinator.OpenAsync(new WorkspaceOpenRequest
        {
            Path = fixture.ProjectPath,
        }, CancellationToken.None);
        var target = new GetPartialDeclarationsTool();

        var result = await BundledCoreToolTestHarness.ExecuteQueryAsync(coordinator, "get-partial-declarations", target, new GetPartialDeclarationsRequest
        {
            Symbol = new SymbolSelector
            {
                DocumentationCommentId = "T:Sample.PartialFormatter",
            },
        });

        result.Outcome.Should().Be(ToolOutcome.Succeeded);
        result.Data!.Declarations.Items.Should().HaveCount(2);
    }
}
