namespace Roslyn.Workbench.Mcp.Plugins.Core.Test.Inspection;

public sealed class GetCodeContextToolTests
{
    [Fact]
    public async Task GIVEN_InspectionWorkspace_WHEN_ExecutingTool_THEN_ShouldReturnContextAndDiagnostics()
    {
        using var fixture = await InspectionSampleFixture.CreateAsync();
        var coordinator = BundledCoreToolTestHarness.CreateInspectionCoordinator();
        var openResult = await coordinator.OpenAsync(new WorkspaceOpenRequest
        {
            Path = fixture.ProjectPath,
        }, CancellationToken.None);
        var target = new GetCodeContextTool();

        var result = await BundledCoreToolTestHarness.ExecuteQueryAsync(coordinator, "get-code-context", target, new GetCodeContextRequest
        {
            Location = fixture.GetLocation("var unused = 42;"),
            IncludeDiagnostics = true,
            IncludeEnclosingSymbols = true,
            ExpectedSnapshot = BundledCoreToolTestHarness.CreateSnapshot(openResult),
        });

        result.Outcome.Should().Be(ToolOutcome.Succeeded);
        result.Data!.Text.Should().Contain("var unused = 42;");
        result.Data.Diagnostics.Should().Contain(static diagnostic => diagnostic.Id == "CS0219");
        result.Data.EnclosingSymbols.Should().Contain(static symbol => symbol.DisplayName.Contains("GreetingFormatter.Format", StringComparison.Ordinal));
    }
}
