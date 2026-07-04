namespace Roslyn.Workbench.Mcp.Plugins.Core.Test.Inspection;

public sealed class GetDocumentOptionsToolTests
{
    [Fact]
    public async Task GIVEN_InspectionWorkspace_WHEN_ExecutingTool_THEN_ShouldReturnDocumentOptions()
    {
        using var fixture = await InspectionSampleFixture.CreateAsync();
        var coordinator = BundledCoreToolTestHarness.CreateInspectionCoordinator();
        await coordinator.OpenAsync(new WorkspaceOpenRequest
        {
            Path = fixture.ProjectPath,
        }, CancellationToken.None);
        var target = new GetDocumentOptionsTool();

        var result = await BundledCoreToolTestHarness.ExecuteQueryAsync(coordinator, "get-document-options", target, new GetDocumentOptionsRequest
        {
            Document = new DocumentSelector
            {
                Path = "Formatting.cs",
            },
        });

        result.Outcome.Should().Be(ToolOutcome.Succeeded);
        result.Data!.LanguageVersion.Should().NotBeNullOrWhiteSpace();
        result.Data.AnalyzerConfig!.EditorConfigPaths.Should().Contain(static path => path.EndsWith(".editorconfig", StringComparison.Ordinal));
    }
}
