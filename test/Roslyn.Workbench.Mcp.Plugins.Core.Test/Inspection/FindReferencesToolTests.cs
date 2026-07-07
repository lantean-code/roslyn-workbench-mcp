using Microsoft.CodeAnalysis.Text;

namespace Roslyn.Workbench.Mcp.Plugins.Core.Test.Inspection;

public sealed class FindReferencesToolTests
{
    [Fact]
    public async Task GIVEN_ContextRequested_WHEN_CallingExecute_THEN_ShouldUseInspectionContextService()
    {
        using var workspace = MiniWorkspaceFactory.CreateCSharp("""
            namespace Sample;

            public sealed class StateHolder
            {
                public int Current { get; set; }

                public int Read()
                {
                    return Current;
                }

                public void Write(int value)
                {
                    Current = value;
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
        var target = new FindReferencesTool();

        inspectionContextService
            .Setup(service => service.TryCreateContainingSymbolAsync(
                It.IsAny<Document>(),
                It.IsAny<int>(),
                It.IsAny<Solution>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((ISymbol?)null);
        inspectionContextService
            .Setup(service => service.ReadContextAsync(
                It.IsAny<Document?>(),
                It.IsAny<TextSpan>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync("Context");

        var result = await target.ExecuteAsync(new FindReferencesRequest
        {
            Symbol = new SymbolSelector
            {
                DocumentationCommentId = "P:Sample.StateHolder.Current",
            },
            IncludeContext = true,
        }, context, CancellationToken.None);

        result.Outcome.Should().Be(ToolOutcome.Succeeded);
        result.Data!.References.Items.Should().NotBeEmpty();
        result.Data.References.Items.Where(static reference => !reference.IsDefinition).Should().OnlyContain(static reference => reference.Context == "Context");
        inspectionContextService.Verify(service => service.ReadContextAsync(
            It.IsAny<Document?>(),
            It.IsAny<TextSpan>(),
            CancellationToken.None), Times.AtLeastOnce);
    }

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
        result.Data!.References.Items.Should().Contain(static reference => reference.IsWrite && reference.Context == "Current = value;");
        result.Data.References.Items.Should().Contain(static reference => !reference.IsWrite && reference.Context == "return Current;");
    }
}
