using Microsoft.CodeAnalysis.Text;

namespace Roslyn.Workbench.Mcp.Plugins.Core.Test.Inspection;

public sealed class GetDiagnosticsToolTests
{
    [Fact]
    public async Task GIVEN_ResolveDocumentsHasRejection_WHEN_CallingExecuteAsync_THEN_ShouldReturnRejectionResult()
    {
        var expected = PluginExecutionResult<DiagnosticsData>.Rejected(new ToolError
        {
            Code = "DocumentNotFound",
            Message = "DocumentNotFound",
        });
        var requestResolver = new Mock<IToolRequestResolver>();
        var context = CreateContext(requestResolver: requestResolver.Object);
        var target = new GetDiagnosticsTool();

        requestResolver
            .Setup(resolver => resolver.ResolveDocuments<DiagnosticsData>(
                It.IsAny<ScopeSelector?>(),
                It.IsAny<IToolExecutionContext>()))
            .Returns(new ToolResolutionResult<IReadOnlyList<Document>, DiagnosticsData>
            {
                Rejection = expected,
            });

        var result = await target.ExecuteAsync(new GetDiagnosticsRequest(), context, CancellationToken.None);

        result.Should().BeEquivalentTo(expected);
    }

    [Fact]
    public async Task GIVEN_ProjectCompilationIsNull_WHEN_CallingExecuteAsync_THEN_ShouldReturnEmptyDiagnostics()
    {
        using var workspace = new AdhocWorkspace();
        var solution = CreateUnsupportedLanguageSolution(workspace);
        var document = solution.Projects.Single().Documents.Single();
        var requestResolver = new Mock<IToolRequestResolver>();
        var context = new QueryContextBuilder()
            .WithCurrentSolution(solution)
            .WithToolExecutionServices(new ToolExecutionServicesBuilder().WithRequestResolver(requestResolver.Object).Build())
            .Build();
        var target = new GetDiagnosticsTool();

        requestResolver
            .Setup(resolver => resolver.ResolveDocuments<DiagnosticsData>(
                It.IsAny<ScopeSelector?>(),
                It.IsAny<IToolExecutionContext>()))
            .Returns(new ToolResolutionResult<IReadOnlyList<Document>, DiagnosticsData>
            {
                Value = [document],
            });

        var result = await target.ExecuteAsync(new GetDiagnosticsRequest
        {
            Scope = new ScopeSelector
            {
                Kind = ScopeKind.Document,
            },
        }, context, CancellationToken.None);

        result.Outcome.Should().Be(ToolOutcome.Succeeded);
        result.Data!.Diagnostics.Items.Should().BeEmpty();
    }

    [Fact]
    public async Task GIVEN_DocumentScopeAndDiagnosticFilters_WHEN_CallingExecuteAsync_THEN_ShouldReturnOnlyMatchingSelectedDocumentDiagnostics()
    {
        using var workspace = MiniWorkspaceFactory.CreateCSharp(
        [
            ("First.cs", """
                #nullable enable
                namespace Sample;

                public sealed class First
                {
                    public string Format(string? value)
                    {
                        var unused = 0;
                        return value.ToString();
                    }
                }
                """),
            ("Second.cs", """
                #nullable enable
                namespace Sample;

                public sealed class Second
                {
                    public string Format(string? value)
                    {
                        return value.ToString();
                    }
                }
                """),
        ]);
        var workspaceIdentity = workspace.CreateWorkspaceIdentity();
        var context = new QueryContextBuilder()
            .WithCurrentSolution(workspace.Solution)
            .WithResolver(workspace.CreateResolver(workspaceIdentity))
            .WithWorkspaceIdentity(workspaceIdentity)
            .Build();
        var target = new GetDiagnosticsTool();

        var result = await target.ExecuteAsync(new GetDiagnosticsRequest
        {
            Scope = new ScopeSelector
            {
                Kind = ScopeKind.Document,
                Document = new DocumentSelector
                {
                    Path = "First.cs",
                },
            },
            Ids = ["CS8602"],
            Severities = ["warning"],
        }, context, CancellationToken.None);

        result.Outcome.Should().Be(ToolOutcome.Succeeded);
        result.Data!.Diagnostics.Items.Should().ContainSingle();
        result.Data.Diagnostics.Items[0].Id.Should().Be("CS8602");
        result.Data.Diagnostics.Items[0].Location!.Document!.Path.Should().Be("First.cs");
    }

    private static IQueryContext CreateContext(MiniWorkspace? workspace = null, IToolRequestResolver? requestResolver = null)
    {
        var currentWorkspace = workspace ?? MiniWorkspaceFactory.CreateCSharp("namespace Sample;");
        var workspaceIdentity = currentWorkspace.CreateWorkspaceIdentity();
        var services = new ToolExecutionServicesBuilder()
            .WithRequestResolver(requestResolver ?? Mock.Of<IToolRequestResolver>())
            .Build();

        return new QueryContextBuilder()
            .WithCurrentSolution(currentWorkspace.Solution)
            .WithResolver(currentWorkspace.CreateResolver(workspaceIdentity))
            .WithWorkspaceIdentity(workspaceIdentity)
            .WithToolExecutionServices(services)
            .Build();
    }

    internal static Solution CreateUnsupportedLanguageSolution(AdhocWorkspace workspace)
    {
        var projectId = ProjectId.CreateNewId();
        var versionStamp = VersionStamp.Create();
        var solution = workspace.CurrentSolution.AddProject(Microsoft.CodeAnalysis.ProjectInfo.Create(
            projectId,
            versionStamp,
            "Sample",
            "Sample",
            "NoLanguage",
            filePath: "/workspace/Sample.proj"));
        solution = solution.AddDocument(DocumentInfo.Create(
            DocumentId.CreateNewId(projectId),
            "Sample.txt",
            filePath: "/workspace/Sample.txt",
            loader: TextLoader.From(TextAndVersion.Create(SourceText.From("content"), versionStamp))));
        workspace.TryApplyChanges(solution);

        return workspace.CurrentSolution;
    }
}

public sealed class AnalyzeAsyncToolTests
{
    [Fact]
    public async Task GIVEN_ResolveDocumentsHasRejection_WHEN_CallingExecuteAsync_THEN_ShouldReturnRejectionResult()
    {
        var expected = PluginExecutionResult<AsyncAnalysisData>.Rejected(new ToolError
        {
            Code = "DocumentNotFound",
            Message = "DocumentNotFound",
        });
        var requestResolver = new Mock<IToolRequestResolver>();
        var context = CreateContext(requestResolver: requestResolver.Object);
        var target = new AnalyzeAsyncTool();

        requestResolver
            .Setup(resolver => resolver.ResolveDocuments<AsyncAnalysisData>(
                It.IsAny<ScopeSelector?>(),
                It.IsAny<IToolExecutionContext>()))
            .Returns(new ToolResolutionResult<IReadOnlyList<Document>, AsyncAnalysisData>
            {
                Rejection = expected,
            });

        var result = await target.ExecuteAsync(new AnalyzeAsyncRequest(), context, CancellationToken.None);

        result.Should().BeEquivalentTo(expected);
    }

    [Fact]
    public async Task GIVEN_DocumentWithoutSyntaxOrSemanticModel_WHEN_CallingExecuteAsync_THEN_ShouldReturnEmptyFindings()
    {
        using var workspace = new AdhocWorkspace();
        var solution = GetDiagnosticsToolTests.CreateUnsupportedLanguageSolution(workspace);
        var document = solution.Projects.Single().Documents.Single();
        var requestResolver = new Mock<IToolRequestResolver>();
        var context = new QueryContextBuilder()
            .WithCurrentSolution(solution)
            .WithToolExecutionServices(new ToolExecutionServicesBuilder().WithRequestResolver(requestResolver.Object).Build())
            .Build();
        var target = new AnalyzeAsyncTool();

        requestResolver
            .Setup(resolver => resolver.ResolveDocuments<AsyncAnalysisData>(
                It.IsAny<ScopeSelector?>(),
                It.IsAny<IToolExecutionContext>()))
            .Returns(new ToolResolutionResult<IReadOnlyList<Document>, AsyncAnalysisData>
            {
                Value = [document],
            });

        var result = await target.ExecuteAsync(new AnalyzeAsyncRequest(), context, CancellationToken.None);

        result.Outcome.Should().Be(ToolOutcome.Succeeded);
        result.Data!.Findings.Items.Should().BeEmpty();
    }

    [Fact]
    public async Task GIVEN_AsyncMethodWithoutAwaitAndUnawaitedInvocation_WHEN_CallingExecuteAsync_THEN_ShouldReturnBothFindingKinds()
    {
        using var workspace = MiniWorkspaceFactory.CreateCSharp("""
            using System.Threading.Tasks;

            namespace Sample;

            public sealed class Formatter
            {
                public async Task NoAwaitAsync()
                {
                }

                public async Task CallerAsync()
                {
                    ReturnTask();
                    await ReturnTask();
                }

                private Task ReturnTask()
                {
                    return Task.CompletedTask;
                }
            }
            """);
        var workspaceIdentity = workspace.CreateWorkspaceIdentity();
        var context = new QueryContextBuilder()
            .WithCurrentSolution(workspace.Solution)
            .WithResolver(workspace.CreateResolver(workspaceIdentity))
            .WithWorkspaceIdentity(workspaceIdentity)
            .Build();
        var target = new AnalyzeAsyncTool();

        var result = await target.ExecuteAsync(new AnalyzeAsyncRequest
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
        result.Data!.Findings.Items.Should().Contain(finding => finding.Kind == "AsyncWithoutAwait");
        result.Data.Findings.Items.Should().Contain(finding => finding.Kind == "UnawaitedTask");
    }

    private static IQueryContext CreateContext(MiniWorkspace? workspace = null, IToolRequestResolver? requestResolver = null)
    {
        var currentWorkspace = workspace ?? MiniWorkspaceFactory.CreateCSharp("namespace Sample;");
        var workspaceIdentity = currentWorkspace.CreateWorkspaceIdentity();
        var services = new ToolExecutionServicesBuilder()
            .WithRequestResolver(requestResolver ?? Mock.Of<IToolRequestResolver>())
            .Build();

        return new QueryContextBuilder()
            .WithCurrentSolution(currentWorkspace.Solution)
            .WithResolver(currentWorkspace.CreateResolver(workspaceIdentity))
            .WithWorkspaceIdentity(workspaceIdentity)
            .WithToolExecutionServices(services)
            .Build();
    }
}
