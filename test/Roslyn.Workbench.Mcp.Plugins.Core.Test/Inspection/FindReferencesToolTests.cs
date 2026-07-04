namespace Roslyn.Workbench.Mcp.Plugins.Core.Test.Inspection;

public sealed class FindReferencesToolTests
{
    [Fact]
    public async Task GIVEN_PropertyReferences_WHEN_ExecutingTool_THEN_ShouldClassifyWrites()
    {
        using var fixture = await InspectionSampleFixture.CreateAsync();
        var coordinator = BundledCoreToolTestHarness.CreateInspectionCoordinator();
        await coordinator.OpenAsync(new WorkspaceOpenRequest
        {
            Path = fixture.ProjectPath,
        }, CancellationToken.None);
        var target = new FindReferencesTool();

        var result = await BundledCoreToolTestHarness.ExecuteQueryAsync(coordinator, "find-references", target, new FindReferencesRequest
        {
            Symbol = new SymbolSelector
            {
                DocumentationCommentId = "P:Sample.StateHolder.Current",
            },
            IncludeDefinitions = false,
            IncludeContext = true,
        });

        result.Outcome.Should().Be(ToolOutcome.Succeeded);
        result.Data!.References.Should().Contain(static reference => reference.IsWrite && reference.Context == "Current = value;");
        result.Data.References.Should().Contain(static reference => !reference.IsWrite && reference.Context == "return Current;");
    }
}
