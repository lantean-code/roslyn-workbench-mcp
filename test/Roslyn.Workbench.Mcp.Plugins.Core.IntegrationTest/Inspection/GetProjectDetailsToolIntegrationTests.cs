using System.Text.Json;

using Roslyn.Workbench.Mcp.TestSupport;

namespace Roslyn.Workbench.Mcp.Plugins.Core.Test.Inspection;

[Trait("Category", "Integration")]
public sealed class GetProjectDetailsToolIntegrationTests
{
    [Fact]
    public async Task GIVEN_InspectionWorkspace_WHEN_RequestingProjectDetailsByDefault_THEN_ShouldOmitDocumentInventory()
    {
        using var fixture = await InspectionSampleFixture.CreateAsync();
        var coordinator = WorkspaceCoordinatorFactory.Create(toolExecutionServices: BundledCoreToolExecutionServicesFactory.Create());
        await coordinator.OpenAsync(new WorkspaceOpenRequest
        {
            Path = fixture.ProjectPath,
        }, CancellationToken.None);
        var plugin = new BundledCorePlugin();
        var registry = new PluginRegistry(plugin.Metadata);
        plugin.Register(registry);

        var result = await PluginToolTestHarness.InvokeAsync<ProjectDetailsData>(coordinator, registry, "get-project-details", new Dictionary<string, JsonElement>
        {
            ["project"] = JsonSerializer.SerializeToElement(new ProjectSelector
            {
                Path = "Sample.csproj",
            }),
        });

        result.Outcome.Should().Be(ToolOutcome.Succeeded);
        result.Data!.Documents.Should().BeNull();
    }

    [Fact]
    public async Task GIVEN_InspectionWorkspace_WHEN_RequestingProjectDocumentsExplicitly_THEN_ShouldReturnBoundedDocumentInventory()
    {
        using var fixture = await InspectionSampleFixture.CreateAsync();
        var coordinator = WorkspaceCoordinatorFactory.Create(toolExecutionServices: BundledCoreToolExecutionServicesFactory.Create());
        await coordinator.OpenAsync(new WorkspaceOpenRequest
        {
            Path = fixture.ProjectPath,
        }, CancellationToken.None);
        var plugin = new BundledCorePlugin();
        var registry = new PluginRegistry(plugin.Metadata);
        plugin.Register(registry);

        var result = await PluginToolTestHarness.InvokeAsync<ProjectDetailsData>(coordinator, registry, "get-project-details", new Dictionary<string, JsonElement>
        {
            ["project"] = JsonSerializer.SerializeToElement(new ProjectSelector
            {
                Path = "Sample.csproj",
            }),
            ["includeDocuments"] = JsonSerializer.SerializeToElement(true),
        });

        result.Outcome.Should().Be(ToolOutcome.Succeeded);
        result.Data!.Documents.Should().NotBeNull();
        result.Data.Documents!.Items.Should().NotBeEmpty();
    }
}
