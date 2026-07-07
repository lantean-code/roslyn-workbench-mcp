namespace Roslyn.Workbench.Mcp.Plugins.Core.Test.Inspection;

public sealed class FindDerivedTypesToolTests
{
    [Fact]
    public async Task GIVEN_InspectionWorkspace_WHEN_ExecutingTool_THEN_ShouldReturnDerivedTypes()
    {
        using var fixture = await InspectionSampleFixture.CreateAsync();
        var coordinator = BundledCoreToolTestHarness.CreateInspectionCoordinator();
        await coordinator.OpenAsync(new WorkspaceOpenRequest
        {
            Path = fixture.ProjectPath,
        }, CancellationToken.None);
        var target = new FindDerivedTypesTool();

        var result = await BundledCoreToolTestHarness.ExecuteQueryAsync(coordinator, "find-derived-types", target, new FindDerivedTypesRequest
        {
            Symbol = new SymbolSelector
            {
                DocumentationCommentId = "T:Sample.FormatterBase",
            },
        });

        result.Data!.DerivedTypes.Items.Should().Contain(static node => node.Type!.DisplayName.Contains("DerivedGreetingFormatter", StringComparison.Ordinal));
    }
}
