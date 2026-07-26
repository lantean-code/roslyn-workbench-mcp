namespace Roslyn.Workbench.Mcp.Plugins.Core.Test.Refactorings;

public sealed class SortUsingsToolTests
{
    [Fact]
    public async Task GIVEN_ResolveDocumentHasRejection_WHEN_CallingExecuteAsync_THEN_ShouldReturnRejectionResult()
    {
        var expected = PluginExecutionResult.Rejected<MutationCandidate>(new PluginExecutionError
        {
            Code = "DocumentNotFound",
            Message = "DocumentNotFound",
        });

        var contextMocks = MutationContextMockHelper.Create();
        var request = new SortUsingsRequest
        {
            ExpectedSnapshot = new SnapshotPrecondition(),
            Document = new DocumentSelector(),
        };

        var target = new SortUsingsTool();

        contextMocks.RequestResolver
            .Setup(item => item.ResolveDocument<MutationCandidate>(request.Document, contextMocks.MutationContext.Object))
            .Returns(ToolResolutionResult.Rejected<Document, MutationCandidate>(expected));

        var result = await target.ExecuteAsync(request, contextMocks.MutationContext.Object, CancellationToken.None);

        result.Should().BeEquivalentTo(expected);
        contextMocks.RequestResolver.Verify(item => item.ValidateSnapshot<MutationCandidate>(
            It.IsAny<IToolExecutionContext>(),
            It.IsAny<SnapshotPrecondition?>()), Times.Never);
    }

    [Fact]
    public async Task GIVEN_ValidateSnapshotHasRejection_WHEN_CallingExecuteAsync_THEN_ShouldReturnRejectionResult()
    {
        using var document = RoslynTestFactory.CreateDocument("using System;");
        var expected = PluginExecutionResult.Conflict<MutationCandidate>(new PluginExecutionError
        {
            Code = "SnapshotMismatch",
            Message = "SnapshotMismatch",
        });

        var contextMocks = MutationContextMockHelper.Create();
        var request = new SortUsingsRequest
        {
            ExpectedSnapshot = new SnapshotPrecondition(),
            Document = new DocumentSelector(),
        };

        var target = new SortUsingsTool();

        contextMocks.RequestResolver
            .Setup(item => item.ResolveDocument<MutationCandidate>(request.Document, contextMocks.MutationContext.Object))
            .Returns(ToolResolutionResult.Resolved<Document, MutationCandidate>(document.Document));

        contextMocks.RequestResolver
            .Setup(item => item.ValidateSnapshot<MutationCandidate>(contextMocks.MutationContext.Object, request.ExpectedSnapshot))
            .Returns(expected);

        var result = await target.ExecuteAsync(request, contextMocks.MutationContext.Object, CancellationToken.None);

        result.Should().BeEquivalentTo(expected);
    }

    [Fact]
    public async Task GIVEN_DocumentHasNoCompilationUnitRoot_WHEN_CallingExecuteAsync_THEN_ShouldReturnInvalidRequestResult()
    {
        using var document = RoslynTestFactory.CreateUnsupportedDocument();
        var contextMocks = MutationContextMockHelper.Create();
        var request = new SortUsingsRequest
        {
            ExpectedSnapshot = new SnapshotPrecondition(),
            Document = new DocumentSelector(),
        };

        var target = new SortUsingsTool();

        contextMocks.RequestResolver
            .Setup(item => item.ResolveDocument<MutationCandidate>(request.Document, contextMocks.MutationContext.Object))
            .Returns(ToolResolutionResult.Resolved<Document, MutationCandidate>(document.Document));

        contextMocks.RequestResolver
            .Setup(item => item.ValidateSnapshot<MutationCandidate>(contextMocks.MutationContext.Object, request.ExpectedSnapshot))
            .Returns((PluginExecutionResult<MutationCandidate>?)null);

        var result = await target.ExecuteAsync(request, contextMocks.MutationContext.Object, CancellationToken.None);

        result.Outcome.Should().Be(PluginExecutionOutcome.Rejected);
        result.Error.Should().BeEquivalentTo(new PluginExecutionError
        {
            Code = "InvalidRequest",
            Message = "Sort usings requires a compilation unit root.",
        });
    }

    [Fact]
    public async Task GIVEN_UsingDirectivesAreAlreadyOrdered_WHEN_CallingExecuteAsync_THEN_ShouldReturnNoChangeResult()
    {
        using var document = RoslynTestFactory.CreateDocument("using Alpha;\r\nusing System;\r\n");
        var contextMocks = MutationContextMockHelper.Create();
        var request = new SortUsingsRequest
        {
            ExpectedSnapshot = new SnapshotPrecondition(),
            Document = new DocumentSelector(),
            SystemFirst = false,
        };

        var target = new SortUsingsTool();

        contextMocks.RequestResolver
            .Setup(item => item.ResolveDocument<MutationCandidate>(request.Document, contextMocks.MutationContext.Object))
            .Returns(ToolResolutionResult.Resolved<Document, MutationCandidate>(document.Document));

        contextMocks.RequestResolver
            .Setup(item => item.ValidateSnapshot<MutationCandidate>(contextMocks.MutationContext.Object, request.ExpectedSnapshot))
            .Returns((PluginExecutionResult<MutationCandidate>?)null);

        var result = await target.ExecuteAsync(request, contextMocks.MutationContext.Object, CancellationToken.None);

        result.Outcome.Should().Be(PluginExecutionOutcome.NoChange);
    }

    [Fact]
    public async Task GIVEN_SystemFirstReordersNamedAndAliasedUsings_WHEN_CallingExecuteAsync_THEN_ShouldReturnMutationCandidate()
    {
        using var document = RoslynTestFactory.CreateDocument("using Z = Zeta;\r\nusing Alpha;\r\nusing System;\r\n", "Sample.cs");
        var contextMocks = MutationContextMockHelper.Create();
        var request = new SortUsingsRequest
        {
            ExpectedSnapshot = new SnapshotPrecondition(),
            Document = new DocumentSelector(),
            SystemFirst = true,
        };

        var target = new SortUsingsTool();

        contextMocks.RequestResolver
            .Setup(item => item.ResolveDocument<MutationCandidate>(request.Document, contextMocks.MutationContext.Object))
            .Returns(ToolResolutionResult.Resolved<Document, MutationCandidate>(document.Document));

        contextMocks.RequestResolver
            .Setup(item => item.ValidateSnapshot<MutationCandidate>(contextMocks.MutationContext.Object, request.ExpectedSnapshot))
            .Returns((PluginExecutionResult<MutationCandidate>?)null);

        var result = await target.ExecuteAsync(request, contextMocks.MutationContext.Object, CancellationToken.None);

        result.Outcome.Should().Be(PluginExecutionOutcome.Succeeded);
        result.Data.Should().NotBeNull();
        result.Data!.CandidateSolution.Should().NotBeSameAs(document.Solution);
        result.Data.Summary.Should().Be("Sort using directives in 'Sample.cs'.");
    }

    [Fact]
    public async Task GIVEN_SystemFirstAndNamespaceOnlyStartsWithSystem_WHEN_CallingExecuteAsync_THEN_ShouldNotClassifyItAsSystemNamespace()
    {
        using var document = RoslynTestFactory.CreateDocument(
            "using Systematic;\r\nusing Zebra;\r\nusing System.Text;\r\nusing global::System;\r\n",
            "Sample.cs");

        var contextMocks = MutationContextMockHelper.Create();
        var request = new SortUsingsRequest
        {
            ExpectedSnapshot = new SnapshotPrecondition(),
            Document = new DocumentSelector(),
            SystemFirst = true,
        };

        var target = new SortUsingsTool();

        contextMocks.RequestResolver
            .Setup(item => item.ResolveDocument<MutationCandidate>(request.Document, contextMocks.MutationContext.Object))
            .Returns(ToolResolutionResult.Resolved<Document, MutationCandidate>(document.Document));

        contextMocks.RequestResolver
            .Setup(item => item.ValidateSnapshot<MutationCandidate>(contextMocks.MutationContext.Object, request.ExpectedSnapshot))
            .Returns((PluginExecutionResult<MutationCandidate>?)null);

        var result = await target.ExecuteAsync(request, contextMocks.MutationContext.Object, CancellationToken.None);

        var candidateDocument = result.Data!.CandidateSolution.GetDocument(document.Document.Id);
        var candidateText = await candidateDocument!.GetTextAsync(CancellationToken.None);

        candidateText.ToString().Should().Be(
            "using global::System;\r\nusing System.Text;\r\nusing Systematic;\r\nusing Zebra;\r\n");
    }

    [Fact]
    public async Task GIVEN_UsingDirectiveHasNoName_WHEN_CallingExecuteAsync_THEN_ShouldSortUsingEmptyName()
    {
        using var document = RoslynTestFactory.CreateDocument("using Zeta;\r\nusing ;\r\n");
        var contextMocks = MutationContextMockHelper.Create();
        var request = new SortUsingsRequest
        {
            ExpectedSnapshot = new SnapshotPrecondition(),
            Document = new DocumentSelector(),
            SystemFirst = true,
        };

        var target = new SortUsingsTool();

        contextMocks.RequestResolver
            .Setup(item => item.ResolveDocument<MutationCandidate>(request.Document, contextMocks.MutationContext.Object))
            .Returns(ToolResolutionResult.Resolved<Document, MutationCandidate>(document.Document));

        contextMocks.RequestResolver
            .Setup(item => item.ValidateSnapshot<MutationCandidate>(contextMocks.MutationContext.Object, request.ExpectedSnapshot))
            .Returns((PluginExecutionResult<MutationCandidate>?)null);

        var result = await target.ExecuteAsync(request, contextMocks.MutationContext.Object, CancellationToken.None);

        result.Outcome.Should().Be(PluginExecutionOutcome.Succeeded);
    }
}
