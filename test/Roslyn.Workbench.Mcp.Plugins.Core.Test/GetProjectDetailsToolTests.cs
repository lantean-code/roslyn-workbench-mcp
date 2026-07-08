using System.Text.Json;

using Roslyn.Workbench.Mcp.TestSupport;

namespace Roslyn.Workbench.Mcp.Plugins.Core.Test.Inspection;

public sealed class GetProjectDetailsToolTests
{
    [Fact]
    public async Task GIVEN_ProjectStructureServiceFrameworks_WHEN_CallingExecute_THEN_ShouldUseConfiguredTargetFrameworks()
    {
        using var workspace = MiniWorkspaceFactory.CreateCSharp("""
            namespace Sample;

            public sealed class GreetingFormatter
            {
            }
            """);
        var workspaceIdentity = workspace.CreateWorkspaceIdentity();
        var resolver = workspace.CreateResolver(workspaceIdentity);
        var project = workspace.Solution.Projects.Single();
        var projectStructureService = new Mock<IProjectStructureService>();
        var services = new ToolExecutionServicesBuilder()
            .WithProjectStructureService(projectStructureService.Object)
            .Build();
        var context = new QueryContextBuilder()
            .WithCurrentSolution(workspace.Solution)
            .WithResolver(resolver)
            .WithWorkspaceIdentity(workspaceIdentity)
            .WithToolExecutionServices(services)
            .Build();
        var target = new GetProjectDetailsTool();

        projectStructureService
            .Setup(service => service.GetTargetFrameworks(project))
            .Returns(["net10.0", "net9.0"]);

        var result = await target.ExecuteAsync(new GetProjectDetailsRequest
        {
            Project = new ProjectSelector
            {
                Path = "Sample.csproj",
            },
        }, context, CancellationToken.None);

        result.Outcome.Should().Be(ToolOutcome.Succeeded);
        result.Data!.Project!.TargetFrameworks.Should().Equal("net10.0", "net9.0");
    }

    [Fact]
    [Trait("Category", "Integration")]
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
    [Trait("Category", "Integration")]
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
