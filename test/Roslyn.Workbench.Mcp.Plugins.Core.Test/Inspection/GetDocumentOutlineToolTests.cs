namespace Roslyn.Workbench.Mcp.Plugins.Core.Test.Inspection;

public sealed class GetDocumentOutlineToolTests
{
    [Fact]
    public async Task GIVEN_ResolveDocumentHasRejection_WHEN_CallingExecuteAsync_THEN_ShouldReturnRejectionResult()
    {
        var target = new GetDocumentOutlineTool();
        var queryContextMocks = QueryContextMockHelper.Create();
        var expected = PluginExecutionResult<DocumentOutlineData>.Rejected(new PluginExecutionError
        {
            Code = "DocumentNotFound",
            Message = "DocumentNotFound",
        });

        queryContextMocks.RequestResolver
            .Setup(item => item.ResolveDocument<DocumentOutlineData>(
                It.IsAny<DocumentSelector?>(),
                queryContextMocks.QueryContext.Object))
            .Returns(ToolResolutionResult<Document, DocumentOutlineData>.Rejected(expected));

        var result = await target.ExecuteAsync(new GetDocumentOutlineRequest
        {
            Document = new DocumentSelector(),
        }, queryContextMocks.QueryContext.Object, TestContext.Current.CancellationToken);

        result.Should().BeEquivalentTo(expected);
    }

    [Fact]
    public async Task GIVEN_DocumentWithoutSyntaxOrSemanticModel_WHEN_CallingExecuteAsync_THEN_ShouldReturnNullRoot()
    {
        using var document = RoslynTestFactory.CreateUnsupportedDocument();

        var target = new GetDocumentOutlineTool();
        var queryContextMocks = QueryContextMockHelper.Create();

        queryContextMocks.RequestResolver
            .Setup(item => item.ResolveDocument<DocumentOutlineData>(
                It.IsAny<DocumentSelector?>(),
                queryContextMocks.QueryContext.Object))
            .Returns(ToolResolutionResult<Document, DocumentOutlineData>.Resolved(document.Document));

        queryContextMocks.WorkspaceResolver
            .Setup(item => item.CreateDocumentReference(It.IsAny<Document>()))
            .Returns<Document>(item => new DocumentReference
            {
                DocumentId = item.Id.Id.ToString(),
                ProjectId = item.Project.Id.Id.ToString(),
                Path = item.Name,
            });

        var result = await target.ExecuteAsync(new GetDocumentOutlineRequest
        {
            Document = new DocumentSelector(),
        }, queryContextMocks.QueryContext.Object, TestContext.Current.CancellationToken);

        result.Outcome.Should().Be(PluginExecutionOutcome.Succeeded);
        result.Data!.Root.Should().BeNull();
    }

    [Fact]
    public async Task GIVEN_IncludeMembersIsFalse_WHEN_CallingExecuteAsync_THEN_ShouldReturnOutlineWithoutTypeMembers()
    {
        using var document = RoslynTestFactory.CreateDocument("""
            using System;

            namespace Sample;

            class Formatter
            {
                void Run()
                {
                }
            }
            """);

        var target = new GetDocumentOutlineTool();
        var queryContextMocks = QueryContextMockHelper.Create();

        queryContextMocks.RequestResolver
            .Setup(item => item.ResolveDocument<DocumentOutlineData>(
                It.IsAny<DocumentSelector?>(),
                queryContextMocks.QueryContext.Object))
            .Returns(ToolResolutionResult<Document, DocumentOutlineData>.Resolved(document.Document));

        queryContextMocks.WorkspaceResolver
            .Setup(item => item.CreateDocumentReference(It.IsAny<Document>()))
            .Returns<Document>(item => new DocumentReference
            {
                DocumentId = item.Id.Id.ToString(),
                ProjectId = item.Project.Id.Id.ToString(),
                Path = item.Name,
            });

        queryContextMocks.WorkspaceResolver
            .Setup(item => item.CreateResolvedLocation(It.IsAny<Location>()))
            .Returns<Location>(item => SelectorTestFactory.CreateResolvedLocation(item, document.Document.Name));

        var result = await target.ExecuteAsync(new GetDocumentOutlineRequest
        {
            Document = new DocumentSelector(),
            IncludeMembers = false,
        }, queryContextMocks.QueryContext.Object, TestContext.Current.CancellationToken);

        result.Outcome.Should().Be(PluginExecutionOutcome.Succeeded);
        result.Data!.Root!.Name.Should().Be("Code.cs");
        result.Data.Root.Children.Should().ContainSingle(item => item.Name == "Sample");
        result.Data.Root.Children[0].Children.Should().ContainSingle(item => item.Name == "Formatter");
        result.Data.Root.Children[0].Children[0].Children.Should().BeEmpty();
    }

    [Fact]
    public async Task GIVEN_IncludeMembersIsTrue_WHEN_CallingExecuteAsync_THEN_ShouldReturnOutlineWithMembers()
    {
        using var document = RoslynTestFactory.CreateDocument("""
            namespace Sample;

            class Formatter
            {
                Formatter()
                {
                }

                int Value
                {
                    get;
                }

                event System.EventHandler Changed
                {
                    add
                    {
                    }
                    remove
                    {
                    }
                }

                void Run()
                {
                }
            }
            """);

        var target = new GetDocumentOutlineTool();
        var queryContextMocks = QueryContextMockHelper.Create();

        queryContextMocks.RequestResolver
            .Setup(item => item.ResolveDocument<DocumentOutlineData>(
                It.IsAny<DocumentSelector?>(),
                queryContextMocks.QueryContext.Object))
            .Returns(ToolResolutionResult<Document, DocumentOutlineData>.Resolved(document.Document));

        queryContextMocks.WorkspaceResolver
            .Setup(item => item.CreateDocumentReference(It.IsAny<Document>()))
            .Returns<Document>(item => new DocumentReference
            {
                DocumentId = item.Id.Id.ToString(),
                ProjectId = item.Project.Id.Id.ToString(),
                Path = item.Name,
            });

        queryContextMocks.WorkspaceResolver
            .Setup(item => item.CreateResolvedLocation(It.IsAny<Location>()))
            .Returns<Location>(item => SelectorTestFactory.CreateResolvedLocation(item, document.Document.Name));

        var result = await target.ExecuteAsync(new GetDocumentOutlineRequest
        {
            Document = new DocumentSelector(),
            IncludeMembers = true,
        }, queryContextMocks.QueryContext.Object, TestContext.Current.CancellationToken);

        result.Outcome.Should().Be(PluginExecutionOutcome.Succeeded);
        result.Data!.Root!.Children[0].Children[0].Children.Select(item => item.Name).Should().Contain(".ctor");
        result.Data.Root.Children[0].Children[0].Children.Select(item => item.Name).Should().Contain("Value");
        result.Data.Root.Children[0].Children[0].Children.Select(item => item.Name).Should().Contain("Changed");
        result.Data.Root.Children[0].Children[0].Children.Select(item => item.Name).Should().Contain("Run");
    }
}
