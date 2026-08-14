namespace Roslyn.Workbench.Mcp.Plugins.Core.Test.Inspection;

public sealed class GetDocumentOutlineToolTests
{
    [Fact]
    public async Task GIVEN_ResolveDocumentHasRejection_WHEN_CallingExecuteAsync_THEN_ShouldReturnRejectionResult()
    {
        var target = new GetDocumentOutlineTool();
        var queryContextMocks = QueryContextMockHelper.Create();
        var expected = PluginExecutionResult.Rejected<DocumentOutlineData>(new PluginExecutionError
        {
            Code = "DocumentNotFound",
            Message = "DocumentNotFound",
        });

        queryContextMocks.RequestResolver
            .Setup(item => item.ResolveDocument<DocumentOutlineData>(
                It.IsAny<DocumentSelector?>(),
                queryContextMocks.QueryContext.Object))
            .Returns(ToolResolutionResult.Rejected<Document, DocumentOutlineData>(expected));

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
            .Returns(ToolResolutionResult.Resolved<Document, DocumentOutlineData>(document.Document));

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
            .Returns(ToolResolutionResult.Resolved<Document, DocumentOutlineData>(document.Document));

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
                int First, Second;

                Formatter()
                {
                }

                int Value
                {
                    get;
                }

                int this[int index]
                {
                    get
                    {
                        return index;
                    }
                }

                event System.EventHandler? FieldChanged;

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
            .Returns(ToolResolutionResult.Resolved<Document, DocumentOutlineData>(document.Document));

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
        result.Data.Root.Children[0].Children[0].Children.Select(item => item.Name).Should().Contain("FieldChanged");
        result.Data.Root.Children[0].Children[0].Children.Select(item => item.Name).Should().Contain("First");
        result.Data.Root.Children[0].Children[0].Children.Select(item => item.Name).Should().Contain("Run");
        result.Data.Root.Children[0].Children[0].Children.Select(item => item.Name).Should().Contain("Second");
        result.Data.Root.Children[0].Children[0].Children.Select(item => item.Name).Should().Contain("this[]");
        result.Data.Truncated.Should().BeFalse();
    }

    [Fact]
    public async Task GIVEN_OutlineExceedsNodeLimit_WHEN_CallingExecuteAsync_THEN_ShouldReturnBoundedHierarchy()
    {
        using var document = RoslynTestFactory.CreateDocument("""
            namespace Sample;

            class Formatter
            {
                void First()
                {
                }

                void Second()
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
            .Returns(ToolResolutionResult.Resolved<Document, DocumentOutlineData>(document.Document));

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
            NodesLimit = 2,
        }, queryContextMocks.QueryContext.Object, TestContext.Current.CancellationToken);

        result.Outcome.Should().Be(PluginExecutionOutcome.Succeeded);
        result.Data!.Root!.Children.Should().ContainSingle(item => item.Name == "Sample");
        result.Data.Root.Children[0].Children.Should().ContainSingle(item => item.Name == "Formatter");
        result.Data.Root.Children[0].Children[0].Children.Should().BeEmpty();
        result.Data.Truncated.Should().BeTrue();
        queryContextMocks.WorkspaceResolver.Verify(item => item.CreateResolvedLocation(It.IsAny<Location>()), Times.Exactly(2));
    }

    [Fact]
    public async Task GIVEN_MaxDepthExcludesNestedDeclarations_WHEN_CallingExecuteAsync_THEN_ShouldMarkHierarchyTruncated()
    {
        using var document = RoslynTestFactory.CreateDocument("""
            namespace Sample;

            class Formatter
            {
            }
            """);

        var target = new GetDocumentOutlineTool();
        var queryContextMocks = QueryContextMockHelper.Create();

        queryContextMocks.RequestResolver
            .Setup(item => item.ResolveDocument<DocumentOutlineData>(
                It.IsAny<DocumentSelector?>(),
                queryContextMocks.QueryContext.Object))
            .Returns(ToolResolutionResult.Resolved<Document, DocumentOutlineData>(document.Document));

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
            MaxDepth = 1,
        }, queryContextMocks.QueryContext.Object, TestContext.Current.CancellationToken);

        result.Outcome.Should().Be(PluginExecutionOutcome.Succeeded);
        result.Data!.Root!.Children.Should().ContainSingle(item => item.Name == "Sample");
        result.Data.Root.Children[0].Children.Should().BeEmpty();
        result.Data.Truncated.Should().BeTrue();
    }

    [Fact]
    public async Task GIVEN_NodeLimitIsZero_WHEN_CallingExecuteAsync_THEN_ShouldReturnOnlyDocumentRootAndTruncation()
    {
        using var document = RoslynTestFactory.CreateDocument("class Formatter { }");

        var target = new GetDocumentOutlineTool();
        var queryContextMocks = QueryContextMockHelper.Create();

        queryContextMocks.RequestResolver
            .Setup(item => item.ResolveDocument<DocumentOutlineData>(
                It.IsAny<DocumentSelector?>(),
                queryContextMocks.QueryContext.Object))
            .Returns(ToolResolutionResult.Resolved<Document, DocumentOutlineData>(document.Document));

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
            NodesLimit = 0,
        }, queryContextMocks.QueryContext.Object, TestContext.Current.CancellationToken);

        result.Outcome.Should().Be(PluginExecutionOutcome.Succeeded);
        result.Data!.Root!.Children.Should().BeEmpty();
        result.Data.Truncated.Should().BeTrue();
        queryContextMocks.WorkspaceResolver.Verify(item => item.CreateResolvedLocation(It.IsAny<Location>()), Times.Never);
    }

    [Fact]
    public async Task GIVEN_FieldDelegateAndEnumDeclarations_WHEN_ApplyingNodeAndDepthBounds_THEN_ShouldProjectEverySupportedShapeAndTruncationBranch()
    {
        using var document = RoslynTestFactory.CreateDocument("""
            class Fields
            {
                int First, Second;
            }

            delegate void Callback();

            enum State
            {
                Ready,
            }
            """);

        var target = new GetDocumentOutlineTool();
        var queryContextMocks = QueryContextMockHelper.Create();

        queryContextMocks.RequestResolver
            .Setup(item => item.ResolveDocument<DocumentOutlineData>(
                It.IsAny<DocumentSelector?>(),
                queryContextMocks.QueryContext.Object))
            .Returns(ToolResolutionResult.Resolved<Document, DocumentOutlineData>(document.Document));

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

        var completeResult = await target.ExecuteAsync(new GetDocumentOutlineRequest
        {
            Document = new DocumentSelector(),
        }, queryContextMocks.QueryContext.Object, TestContext.Current.CancellationToken);

        completeResult.Data!.Root!.Children.Select(item => item.Name).Should().Equal("Fields", "Callback", "State");
        completeResult.Data.Root.Children[0].Children.Select(item => item.Name).Should().Equal("First", "Second");
        completeResult.Data.Root.Children[2].Children.Should().ContainSingle(item => item.Name == "Ready");
        completeResult.Data.Truncated.Should().BeFalse();

        var nodeLimitedResult = await target.ExecuteAsync(new GetDocumentOutlineRequest
        {
            Document = new DocumentSelector(),
            NodesLimit = 1,
        }, queryContextMocks.QueryContext.Object, TestContext.Current.CancellationToken);

        nodeLimitedResult.Data!.Root!.Children.Should().ContainSingle(item => item.Name == "Fields");
        nodeLimitedResult.Data.Root.Children[0].Children.Should().BeEmpty();
        nodeLimitedResult.Data.Truncated.Should().BeTrue();

        var depthLimitedResult = await target.ExecuteAsync(new GetDocumentOutlineRequest
        {
            Document = new DocumentSelector(),
            MaxDepth = 1,
        }, queryContextMocks.QueryContext.Object, TestContext.Current.CancellationToken);

        depthLimitedResult.Data!.Root!.Children.Select(item => item.Name).Should().Equal("Fields", "Callback", "State");
        depthLimitedResult.Data.Root.Children.Should().OnlyContain(static item => item.Children.Count == 0);
        depthLimitedResult.Data.Truncated.Should().BeTrue();

        var zeroDepthResult = await target.ExecuteAsync(new GetDocumentOutlineRequest
        {
            Document = new DocumentSelector(),
            MaxDepth = 0,
        }, queryContextMocks.QueryContext.Object, TestContext.Current.CancellationToken);

        zeroDepthResult.Data!.Root!.Children.Should().BeEmpty();
        zeroDepthResult.Data.Truncated.Should().BeTrue();
    }
}
