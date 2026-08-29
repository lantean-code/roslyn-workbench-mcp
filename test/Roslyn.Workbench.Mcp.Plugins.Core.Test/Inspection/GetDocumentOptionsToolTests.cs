using Microsoft.CodeAnalysis.Text;

namespace Roslyn.Workbench.Mcp.Plugins.Core.Test.Inspection;

public sealed class GetDocumentOptionsToolTests
{
    [Fact]
    public async Task GIVEN_ResolveDocumentHasRejection_WHEN_CallingExecuteAsync_THEN_ShouldReturnRejectionResult()
    {
        var target = new GetDocumentOptionsTool();
        var queryContextMocks = QueryContextMockHelper.Create();
        var expected = PluginExecutionResult.Rejected<DocumentOptionsData>(new PluginExecutionError
        {
            Code = "DocumentNotFound",
            Message = "DocumentNotFound",
        });

        queryContextMocks.RequestResolver
            .Setup(item => item.ResolveDocument<DocumentOptionsData>(
                It.IsAny<DocumentSelector?>(),
                queryContextMocks.QueryContext.Object))
            .Returns(ToolResolutionResult.Rejected<Document, DocumentOptionsData>(expected));

        var result = await target.ExecuteAsync(new GetDocumentOptionsRequest
        {
            Document = new DocumentSelector(),
        }, queryContextMocks.QueryContext.Object, TestContext.Current.CancellationToken);

        result.Should().BeEquivalentTo(expected);
    }

    [Fact]
    public async Task GIVEN_CSharpDocumentWithoutDetailedOptions_WHEN_CallingExecuteAsync_THEN_ShouldReturnConciseOptions()
    {
        using var document = RoslynTestFactory.CreateDocument("""
            #nullable enable
            class Formatter
            {
            }
            """);

        var target = new GetDocumentOptionsTool();
        var queryContextMocks = QueryContextMockHelper.Create();

        queryContextMocks.RequestResolver
            .Setup(item => item.ResolveDocument<DocumentOptionsData>(
                It.IsAny<DocumentSelector?>(),
                queryContextMocks.QueryContext.Object))
            .Returns(ToolResolutionResult.Resolved<Document, DocumentOptionsData>(document.Document));

        queryContextMocks.WorkspaceResolver
            .Setup(item => item.CreateDocumentReference(It.IsAny<Document>()))
            .Returns<Document>(item => new DocumentReference
            {
                DocumentId = item.Id.Id.ToString(),
                ProjectId = item.Project.Id.Id.ToString(),
                Path = item.Name,
            });

        var result = await target.ExecuteAsync(new GetDocumentOptionsRequest
        {
            Document = new DocumentSelector(),
        }, queryContextMocks.QueryContext.Object, TestContext.Current.CancellationToken);

        result.Outcome.Should().Be(PluginExecutionOutcome.Succeeded);
        result.Data!.Document!.Path.Should().Be("Code.cs");
        result.Data.LanguageVersion.Should().NotBeNullOrWhiteSpace();
        result.Data.NullableContext.Should().NotBeNullOrWhiteSpace();
        result.Data.ParseOptions.Should().BeNull();
        result.Data.AnalyzerConfig.Should().BeNull();
    }

    [Fact]
    public async Task GIVEN_CSharpDocumentWithDetailedOptions_WHEN_CallingExecuteAsync_THEN_ShouldReturnParseAndAnalyzerConfigOptions()
    {
        using var document = RoslynTestFactory.CreateDocument("class Formatter { }");
        var project = document.Document.Project;
        var updatedSolution = document.Solution.AddAnalyzerConfigDocument(
            DocumentId.CreateNewId(project.Id, ".editorconfig"),
            ".editorconfig",
            SourceText.From("[*.cs]\nselected_option = Selected\nother_option = Other"),
            filePath: "/workspace/Project/.editorconfig");
        document.Workspace.TryApplyChanges(updatedSolution).Should().BeTrue();
        var currentDocument = document.Workspace.CurrentSolution.GetDocument(document.Document.Id)!;
        var target = new GetDocumentOptionsTool();
        var queryContextMocks = QueryContextMockHelper.Create();

        queryContextMocks.RequestResolver
            .Setup(item => item.ResolveDocument<DocumentOptionsData>(
                It.IsAny<DocumentSelector?>(),
                queryContextMocks.QueryContext.Object))
            .Returns(ToolResolutionResult.Resolved<Document, DocumentOptionsData>(currentDocument));

        queryContextMocks.WorkspaceResolver
            .Setup(item => item.CreateDocumentReference(It.IsAny<Document>()))
            .Returns<Document>(item => new DocumentReference
            {
                DocumentId = item.Id.Id.ToString(),
                ProjectId = item.Project.Id.Id.ToString(),
                Path = item.Name,
            });

        var result = await target.ExecuteAsync(new GetDocumentOptionsRequest
        {
            Document = new DocumentSelector(),
            IncludeParseOptions = true,
            IncludeAnalyzerConfig = true,
        }, queryContextMocks.QueryContext.Object, TestContext.Current.CancellationToken);

        result.Outcome.Should().Be(PluginExecutionOutcome.Succeeded);
        result.Data!.ParseOptions!.Language.Should().Be(LanguageNames.CSharp);
        result.Data.AnalyzerConfig!.Options.Should().ContainKey("selected_option").WhoseValue.Should().Be("Selected");
        result.Data.AnalyzerConfig.Options.Should().ContainKey("other_option").WhoseValue.Should().Be("Other");
    }

    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public async Task GIVEN_OneDetailedProjectionIsRequested_WHEN_CallingExecuteAsync_THEN_ShouldReturnOnlyRequestedProjection(
        bool includeParseOptions,
        bool includeAnalyzerConfig)
    {
        using var document = RoslynTestFactory.CreateDocument("class Formatter { }");
        var target = new GetDocumentOptionsTool();
        var queryContextMocks = QueryContextMockHelper.Create();

        queryContextMocks.RequestResolver
            .Setup(item => item.ResolveDocument<DocumentOptionsData>(
                It.IsAny<DocumentSelector?>(),
                queryContextMocks.QueryContext.Object))
            .Returns(ToolResolutionResult.Resolved<Document, DocumentOptionsData>(document.Document));

        queryContextMocks.WorkspaceResolver
            .Setup(item => item.CreateDocumentReference(It.IsAny<Document>()))
            .Returns<Document>(item => new DocumentReference
            {
                DocumentId = item.Id.Id.ToString(),
                ProjectId = item.Project.Id.Id.ToString(),
                Path = item.Name,
            });

        var result = await target.ExecuteAsync(new GetDocumentOptionsRequest
        {
            Document = new DocumentSelector(),
            IncludeParseOptions = includeParseOptions,
            IncludeAnalyzerConfig = includeAnalyzerConfig,
        }, queryContextMocks.QueryContext.Object, TestContext.Current.CancellationToken);

        result.Outcome.Should().Be(PluginExecutionOutcome.Succeeded);
        (result.Data!.ParseOptions is not null).Should().Be(includeParseOptions);
        (result.Data.AnalyzerConfig is not null).Should().Be(includeAnalyzerConfig);
    }

    [Fact]
    public async Task GIVEN_DocumentWithoutLanguageServices_WHEN_CallingExecuteAsync_THEN_ShouldReturnFallbackOptions()
    {
        using var document = RoslynTestFactory.CreateUnsupportedDocument();

        var target = new GetDocumentOptionsTool();
        var queryContextMocks = QueryContextMockHelper.Create();

        queryContextMocks.RequestResolver
            .Setup(item => item.ResolveDocument<DocumentOptionsData>(
                It.IsAny<DocumentSelector?>(),
                queryContextMocks.QueryContext.Object))
            .Returns(ToolResolutionResult.Resolved<Document, DocumentOptionsData>(document.Document));

        queryContextMocks.WorkspaceResolver
            .Setup(item => item.CreateDocumentReference(It.IsAny<Document>()))
            .Returns<Document>(item => new DocumentReference
            {
                DocumentId = item.Id.Id.ToString(),
                ProjectId = item.Project.Id.Id.ToString(),
                Path = item.Name,
            });

        var result = await target.ExecuteAsync(new GetDocumentOptionsRequest
        {
            Document = new DocumentSelector(),
        }, queryContextMocks.QueryContext.Object, TestContext.Current.CancellationToken);

        result.Outcome.Should().Be(PluginExecutionOutcome.Succeeded);
        result.Data!.LanguageVersion.Should().BeEmpty();
        result.Data.NullableContext.Should().BeEmpty();
        result.Data.ParseOptions.Should().BeNull();
        result.Data.AnalyzerConfig.Should().BeNull();
    }
}
