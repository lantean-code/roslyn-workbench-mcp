namespace Roslyn.Workbench.Mcp.Plugins.Core.Test.Inspection;

public sealed class GetDocumentOutlineToolTests
{
    [Fact]
    public async Task GIVEN_InspectionWorkspace_WHEN_ExecutingTool_THEN_ShouldReturnOutline()
    {
        using var fixture = await InspectionSampleFixture.CreateAsync();
        var coordinator = BundledCoreToolTestHarness.CreateInspectionCoordinator();
        await coordinator.OpenAsync(new WorkspaceOpenRequest
        {
            Path = fixture.ProjectPath,
        }, CancellationToken.None);
        var target = new GetDocumentOutlineTool();

        var result = await BundledCoreToolTestHarness.ExecuteQueryAsync(coordinator, "get-document-outline", target, new GetDocumentOutlineRequest
        {
            Document = new DocumentSelector
            {
                Path = "Formatting.cs",
            },
        });

        result.Outcome.Should().Be(ToolOutcome.Succeeded);
        Enumerate(result.Data!.Root!).Should().Contain(static node => node.Name == "GreetingFormatter");
    }

    private static IEnumerable<OutlineNode> Enumerate(OutlineNode root)
    {
        yield return root;

        foreach (var child in root.Children)
        {
            foreach (var descendant in Enumerate(child))
            {
                yield return descendant;
            }
        }
    }
}
