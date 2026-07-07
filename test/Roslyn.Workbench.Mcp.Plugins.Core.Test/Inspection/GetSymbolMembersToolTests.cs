namespace Roslyn.Workbench.Mcp.Plugins.Core.Test.Inspection;

public sealed class GetSymbolMembersToolTests
{
    [Fact]
    public async Task GIVEN_InspectionWorkspace_WHEN_ExecutingTool_THEN_ShouldReturnMembers()
    {
        using var fixture = await InspectionSampleFixture.CreateAsync();
        var coordinator = BundledCoreToolTestHarness.CreateInspectionCoordinator();
        await coordinator.OpenAsync(new WorkspaceOpenRequest
        {
            Path = fixture.ProjectPath,
        }, CancellationToken.None);
        var target = new GetSymbolMembersTool();

        var result = await BundledCoreToolTestHarness.ExecuteQueryAsync(coordinator, "get-symbol-members", target, new GetSymbolMembersRequest
        {
            Symbol = new SymbolSelector
            {
                DocumentationCommentId = "T:Sample.GreetingFormatter",
            },
            IncludeInherited = true,
        });

        result.Outcome.Should().Be(ToolOutcome.Succeeded);
        result.Data!.Members.Items.Should().Contain(static symbol => symbol.DisplayName.Contains("Decorate", StringComparison.Ordinal));
        result.Data.Members.Items.Should().Contain(static symbol => symbol.DisplayName.Contains("Prefix", StringComparison.Ordinal));
    }
}
