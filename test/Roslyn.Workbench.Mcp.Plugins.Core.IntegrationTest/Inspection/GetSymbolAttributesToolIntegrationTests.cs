namespace Roslyn.Workbench.Mcp.Plugins.Core.Test.Inspection;

[Trait("Category", "Integration")]
public sealed class GetSymbolAttributesToolIntegrationTests
{
    [Fact]
    public async Task GIVEN_InspectionWorkspace_WHEN_ExecutingTool_THEN_ShouldReturnAttributes()
    {
        using var fixture = await InspectionSampleFixture.CreateAsync();
        var coordinator = BundledCoreToolTestHarness.CreateInspectionCoordinator();
        await coordinator.OpenAsync(new WorkspaceOpenRequest
        {
            Path = fixture.ProjectPath,
        }, CancellationToken.None);
        var target = new GetSymbolAttributesTool();

        var result = await BundledCoreToolTestHarness.ExecuteQueryAsync(coordinator, "get-symbol-attributes", target, new GetSymbolAttributesRequest
        {
            Symbol = new SymbolSelector
            {
                DocumentationCommentId = "T:Sample.GreetingFormatter",
            },
            IncludeInherited = true,
        });

        result.Outcome.Should().Be(ToolOutcome.Succeeded);
        result.Data!.Attributes.Items.Should().Contain(static attribute => attribute.Name.Contains("Serializable", StringComparison.Ordinal));
        result.Data.Attributes.Items.Should().Contain(static attribute => attribute.Name.Contains("Obsolete", StringComparison.Ordinal));
    }
}
