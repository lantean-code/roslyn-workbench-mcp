using Microsoft.CodeAnalysis.Text;

namespace Roslyn.Workbench.Mcp.Plugins.Core.Test.Inspection;

public sealed class FindCallersToolTests
{
    [Fact]
    public async Task GIVEN_ContextRequested_WHEN_CallingExecute_THEN_ShouldUseInspectionContextService()
    {
        using var workspace = MiniWorkspaceFactory.CreateCSharp("""
            namespace Sample;

            public sealed class GreetingFormatter
            {
                public string Format(string value)
                {
                    return value.Trim();
                }
            }

            public sealed class FormatterCaller
            {
                public string Call(GreetingFormatter formatter)
                {
                    return formatter.Format("hi");
                }
            }
            """);
        var workspaceIdentity = workspace.CreateWorkspaceIdentity();
        var resolver = workspace.CreateResolver(workspaceIdentity);
        var inspectionContextService = new Mock<IInspectionContextService>();
        var services = new ToolExecutionServicesBuilder()
            .WithInspectionContextService(inspectionContextService.Object)
            .Build();
        var context = new QueryContextBuilder()
            .WithCurrentSolution(workspace.Solution)
            .WithResolver(resolver)
            .WithWorkspaceIdentity(workspaceIdentity)
            .WithToolExecutionServices(services)
            .Build();
        var target = new FindCallersTool();

        inspectionContextService
            .Setup(service => service.ReadContextAsync(
                It.IsAny<Document?>(),
                It.IsAny<TextSpan>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync("Context");

        var result = await target.ExecuteAsync(new FindCallersRequest
        {
            Symbol = new SymbolSelector
            {
                DocumentationCommentId = "M:Sample.GreetingFormatter.Format(System.String)",
            },
            IncludeContext = true,
        }, context, CancellationToken.None);

        result.Outcome.Should().Be(ToolOutcome.Succeeded);
        result.Data!.Callers.Items.Should().ContainSingle(static caller => caller.Contexts.Contains("Context"));
        inspectionContextService.Verify(service => service.ReadContextAsync(
            It.IsAny<Document?>(),
            It.IsAny<TextSpan>(),
            CancellationToken.None), Times.AtLeastOnce);
    }

    [Fact]
    public async Task GIVEN_InspectionWorkspace_WHEN_ExecutingTool_THEN_ShouldReturnCallers()
    {
        using var fixture = await InspectionSampleFixture.CreateAsync();
        var coordinator = BundledCoreToolTestHarness.CreateInspectionCoordinator();
        await coordinator.OpenAsync(new WorkspaceOpenRequest
        {
            Path = fixture.ProjectPath,
        }, CancellationToken.None);
        var target = new FindCallersTool();

        var result = await BundledCoreToolTestHarness.ExecuteQueryAsync(coordinator, "find-callers", target, new FindCallersRequest
        {
            Symbol = new SymbolSelector
            {
                DocumentationCommentId = "M:Sample.GreetingFormatter.Format(System.String)",
            },
        });

        result.Data!.Callers.Items.Should().Contain(static caller => caller.Caller!.DisplayName.Contains("Call", StringComparison.Ordinal));
    }
}
