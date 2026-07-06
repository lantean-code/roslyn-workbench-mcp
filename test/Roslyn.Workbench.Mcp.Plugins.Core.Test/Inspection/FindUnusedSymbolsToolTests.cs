namespace Roslyn.Workbench.Mcp.Plugins.Core.Test.Inspection;

public sealed class FindUnusedSymbolsToolTests
{
    [Fact]
    public async Task GIVEN_UnusedDiagnosticFromCompilerService_WHEN_CallingExecute_THEN_ShouldReturnCandidate()
    {
        using var workspace = MiniWorkspaceFactory.CreateCSharp("""
            namespace Sample;

            public sealed class GreetingFormatter
            {
                public void Run()
                {
                    var unused = 42;
                }
            }
            """);
        var workspaceIdentity = workspace.CreateWorkspaceIdentity();
        var resolver = workspace.CreateResolver(workspaceIdentity);
        var document = workspace.Solution.Projects.Single().Documents.Single();
        var project = workspace.Solution.Projects.Single();
        var compilation = await project.GetCompilationAsync(TestContext.Current.CancellationToken);
        var diagnostic = compilation!.GetDiagnostics(TestContext.Current.CancellationToken).Single(static item => item.Id == "CS0219");
        var compilerDiagnosticService = new Mock<ICompilerDiagnosticService>();
        var services = new ToolExecutionServicesBuilder()
            .WithCompilerDiagnosticService(compilerDiagnosticService.Object)
            .Build();
        var context = new QueryContextBuilder()
            .WithCurrentSolution(workspace.Solution)
            .WithResolver(resolver)
            .WithWorkspaceIdentity(workspaceIdentity)
            .WithToolExecutionServices(services)
            .Build();
        var target = new FindUnusedSymbolsTool();

        compilerDiagnosticService
            .Setup(service => service.GetCompilerDiagnosticsAsync(
                It.IsAny<IReadOnlyList<Document>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync([diagnostic]);

        var result = await target.ExecuteAsync(new FindUnusedSymbolsRequest
        {
            Scope = new ScopeSelector
            {
                Kind = ScopeKind.Document,
                Document = new DocumentSelector
                {
                    Path = "Sample.cs",
                },
            },
        }, context, CancellationToken.None);

        result.Outcome.Should().Be(ToolOutcome.Succeeded);
        result.Data!.Candidates.Should().ContainSingle(static candidate => candidate.Symbol!.DisplayName.Contains("unused", StringComparison.Ordinal));
        compilerDiagnosticService.Verify(service => service.GetCompilerDiagnosticsAsync(
            It.Is<IReadOnlyList<Document>>(documents => documents.Count == 1 && documents[0] == document),
            CancellationToken.None), Times.Once);
    }

    [Fact]
    public async Task GIVEN_InspectionWorkspace_WHEN_ExecutingTool_THEN_ShouldReturnUnusedCandidates()
    {
        using var fixture = await InspectionSampleFixture.CreateAsync();
        var coordinator = BundledCoreToolTestHarness.CreateInspectionCoordinator();
        await coordinator.OpenAsync(new WorkspaceOpenRequest
        {
            Path = fixture.ProjectPath,
        }, CancellationToken.None);
        var target = new FindUnusedSymbolsTool();

        var result = await BundledCoreToolTestHarness.ExecuteQueryAsync(coordinator, "find-unused-symbols", target, new FindUnusedSymbolsRequest
        {
            Scope = new ScopeSelector
            {
                Kind = ScopeKind.Document,
                Document = new DocumentSelector
                {
                    Path = "RemoveUnusedVariable.cs",
                },
            },
        });

        result.Outcome.Should().Be(ToolOutcome.Succeeded);
        result.Data!.Candidates.Should().Contain(static candidate => candidate.Symbol!.DisplayName.Contains("unused", StringComparison.Ordinal));
        result.Data.Candidates.Should().Contain(static candidate => candidate.Reasons.Any(reason => reason.Contains("CS0219", StringComparison.Ordinal)));
    }
}
