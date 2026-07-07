using Microsoft.CodeAnalysis.Text;

namespace Roslyn.Workbench.Mcp.Plugins.Core.Test.Inspection;

public sealed class AnalyzeNullabilityToolTests
{
    [Fact]
    public async Task GIVEN_MixedCompilerDiagnostics_WHEN_CallingExecute_THEN_ShouldFilterToNullabilityFindings()
    {
        using var workspace = MiniWorkspaceFactory.CreateCSharp("""
            namespace Sample;

            public sealed class GreetingFormatter
            {
                public string Format(string? value)
                {
                    return value.ToString();
                }
            }
            """);
        var workspaceIdentity = workspace.CreateWorkspaceIdentity();
        var resolver = workspace.CreateResolver(workspaceIdentity);
        var document = workspace.Solution.Projects.Single().Documents.Single();
        var syntaxTree = await document.GetSyntaxTreeAsync(TestContext.Current.CancellationToken);
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
        var target = new AnalyzeNullabilityTool();

        compilerDiagnosticService
            .Setup(service => service.GetCompilerDiagnosticsAsync(
                It.IsAny<IReadOnlyList<Document>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(
            [
                CreateDiagnostic("CS8602", syntaxTree!, workspace.GetLocationSelector("value").Span!.Start, workspace.GetLocationSelector("value").Span!.Length),
                CreateDiagnostic("CS0219", syntaxTree!, workspace.GetLocationSelector("Format").Span!.Start, workspace.GetLocationSelector("Format").Span!.Length),
            ]);

        var result = await target.ExecuteAsync(new AnalyzeNullabilityRequest
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
        result.Data!.Findings.Items.Should().ContainSingle(static finding => finding.Diagnostic!.Id == "CS8602");
        compilerDiagnosticService.Verify(service => service.GetCompilerDiagnosticsAsync(
            It.Is<IReadOnlyList<Document>>(documents => documents.Count == 1 && documents[0] == document),
            CancellationToken.None), Times.Once);
    }

    [Fact]
    public async Task GIVEN_InspectionWorkspace_WHEN_ExecutingTool_THEN_ShouldReturnNullabilityFindings()
    {
        using var fixture = await InspectionSampleFixture.CreateAsync();
        var coordinator = BundledCoreToolTestHarness.CreateInspectionCoordinator();
        await coordinator.OpenAsync(new WorkspaceOpenRequest
        {
            Path = fixture.ProjectPath,
        }, CancellationToken.None);
        var target = new AnalyzeNullabilityTool();

        var result = await BundledCoreToolTestHarness.ExecuteQueryAsync(coordinator, "analyze-nullability", target, new AnalyzeNullabilityRequest
        {
            Scope = new ScopeSelector
            {
                Kind = ScopeKind.Document,
                Document = new DocumentSelector
                {
                    Path = "EnableNullable.cs",
                },
            },
        });

        result.Data!.Findings.Items.Select(static finding => finding.Diagnostic!.Id).Should().Contain("CS8602");
    }

    private static Diagnostic CreateDiagnostic(string id, SyntaxTree syntaxTree, int start, int length)
    {
        var textSpan = new TextSpan(start, length);

        return Diagnostic.Create(
            new DiagnosticDescriptor(id, id, "Message", "Category", Microsoft.CodeAnalysis.DiagnosticSeverity.Warning, isEnabledByDefault: true),
            syntaxTree.GetLocation(textSpan));
    }
}
