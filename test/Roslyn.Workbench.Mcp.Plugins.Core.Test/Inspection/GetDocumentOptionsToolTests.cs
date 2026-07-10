namespace Roslyn.Workbench.Mcp.Plugins.Core.Test.Inspection;

public sealed class GetDocumentOptionsToolTests
{
    [Fact]
    public void GIVEN_PluginRegistry_WHEN_CallingRegister_THEN_ShouldRegisterQueryTool()
    {
        var registry = new Mock<IPluginRegistry>();

        GetDocumentOptionsTool.Register(registry.Object);

        registry.Verify(item => item.RegisterQueryTool<GetDocumentOptionsRequest, DocumentOptionsData>(
            It.Is<ToolRegistrationMetadata>(metadata =>
                metadata.Name == "get-document-options"
                && metadata.Title == "Get Document Options"
                && metadata.Description == "Returns language, parse and analyzer-config options for a document."),
            It.IsAny<IQueryToolHandler<GetDocumentOptionsRequest, DocumentOptionsData>>()), Times.Once);
    }

    [Fact]
    public async Task GIVEN_ResolveDocumentHasRejection_WHEN_CallingExecuteAsync_THEN_ShouldReturnRejectionResult()
    {
        var target = new GetDocumentOptionsTool();
        var queryContextMocks = QueryContextMockHelper.Create();
        var expected = PluginExecutionResult<DocumentOptionsData>.Rejected(new PluginExecutionError
        {
            Code = "DocumentNotFound",
            Message = "DocumentNotFound",
        });

        queryContextMocks.RequestResolver
            .Setup(item => item.ResolveDocument<DocumentOptionsData>(
                It.IsAny<DocumentSelector?>(),
                queryContextMocks.QueryContext.Object))
            .Returns(new ToolResolutionResult<Document, DocumentOptionsData>
            {
                Rejection = expected,
            });

        var result = await target.ExecuteAsync(new GetDocumentOptionsRequest(), queryContextMocks.QueryContext.Object, TestContext.Current.CancellationToken);

        result.Should().BeEquivalentTo(expected);
    }

    [Fact]
    public async Task GIVEN_CSharpDocument_WHEN_CallingExecuteAsync_THEN_ShouldReturnCSharpOptionsAndAnalyzerConfig()
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
            .Returns(new ToolResolutionResult<Document, DocumentOptionsData>
            {
                Value = document.Document,
            });
        queryContextMocks.WorkspaceResolver
            .Setup(item => item.CreateDocumentReference(It.IsAny<Document>()))
            .Returns<Document>(item => new DocumentReference
            {
                DocumentId = item.Id.Id.ToString(),
                ProjectId = item.Project.Id.Id.ToString(),
                Path = item.Name,
            });

        var result = await target.ExecuteAsync(new GetDocumentOptionsRequest(), queryContextMocks.QueryContext.Object, TestContext.Current.CancellationToken);

        result.Outcome.Should().Be(PluginExecutionOutcome.Succeeded);
        result.Data!.Document!.Path.Should().Be("Code.cs");
        result.Data.LanguageVersion.Should().NotBeNullOrWhiteSpace();
        result.Data.NullableContext.Should().NotBeNullOrWhiteSpace();
        result.Data.ParseOptions!.Language.Should().Be(LanguageNames.CSharp);
        result.Data.AnalyzerConfig!.Options.Should().BeEmpty();
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
            .Returns(new ToolResolutionResult<Document, DocumentOptionsData>
            {
                Value = document.Document,
            });
        queryContextMocks.WorkspaceResolver
            .Setup(item => item.CreateDocumentReference(It.IsAny<Document>()))
            .Returns<Document>(item => new DocumentReference
            {
                DocumentId = item.Id.Id.ToString(),
                ProjectId = item.Project.Id.Id.ToString(),
                Path = item.Name,
            });

        var result = await target.ExecuteAsync(new GetDocumentOptionsRequest(), queryContextMocks.QueryContext.Object, TestContext.Current.CancellationToken);

        result.Outcome.Should().Be(PluginExecutionOutcome.Succeeded);
        result.Data!.LanguageVersion.Should().BeEmpty();
        result.Data.NullableContext.Should().BeEmpty();
        result.Data.ParseOptions.Should().BeNull();
        result.Data.AnalyzerConfig!.Options.Should().BeEmpty();
    }
}
