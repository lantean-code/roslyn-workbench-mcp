namespace Roslyn.Workbench.Mcp.Plugins.Core.Test.Refactorings;

public sealed class FormatDocumentToolTests
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
        var request = new FormatDocumentRequest
        {
            ExpectedSnapshot = WorkspaceSnapshotTestFactory.CreatePrecondition(Guid.Parse("11111111-1111-1111-1111-111111111111")),
            Document = new DocumentSelector(),
        };

        var target = new FormatDocumentTool();

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
        using var document = RoslynTestFactory.CreateDocument("class Sample { }");
        var expected = PluginExecutionResult.Conflict<MutationCandidate>(new PluginExecutionError
        {
            Code = "SnapshotMismatch",
            Message = "SnapshotMismatch",
        });

        var contextMocks = MutationContextMockHelper.Create();
        var request = new FormatDocumentRequest
        {
            ExpectedSnapshot = WorkspaceSnapshotTestFactory.CreatePrecondition(Guid.Parse("11111111-1111-1111-1111-111111111111")),
            Document = new DocumentSelector(),
        };

        var target = new FormatDocumentTool();

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
    public async Task GIVEN_DocumentHasNoFormattingChanges_WHEN_CallingExecuteAsync_THEN_ShouldReturnNoChangeResult()
    {
        using var document = RoslynTestFactory.CreateDocument("class Sample\r\n{\r\n}\r\n", "Sample.cs");
        var contextMocks = MutationContextMockHelper.Create();
        var request = new FormatDocumentRequest
        {
            ExpectedSnapshot = WorkspaceSnapshotTestFactory.CreatePrecondition(Guid.Parse("11111111-1111-1111-1111-111111111111")),
            Document = new DocumentSelector(),
        };

        var target = new FormatDocumentTool();

        contextMocks.RequestResolver
            .Setup(item => item.ResolveDocument<MutationCandidate>(request.Document, contextMocks.MutationContext.Object))
            .Returns(ToolResolutionResult.Resolved<Document, MutationCandidate>(document.Document));

        contextMocks.RequestResolver
            .Setup(item => item.ValidateSnapshot<MutationCandidate>(contextMocks.MutationContext.Object, request.ExpectedSnapshot))
            .Returns((PluginExecutionResult<MutationCandidate>?)null);

        var result = await target.ExecuteAsync(request, contextMocks.MutationContext.Object, CancellationToken.None);

        result.Outcome.Should().Be(PluginExecutionOutcome.NoChange);
    }

    [Theory]
    [InlineData(-1, 1)]
    [InlineData(0, -1)]
    [InlineData(11, 1)]
    [InlineData(10, 2)]
    [InlineData(int.MaxValue, 1)]
    public async Task GIVEN_RangeIsOutsideDocument_WHEN_CallingExecuteAsync_THEN_ShouldReturnInvalidRequestResult(int start, int length)
    {
        using var document = RoslynTestFactory.CreateDocument("class C {}", "Sample.cs");
        var contextMocks = MutationContextMockHelper.Create();
        var range = new TextSpanRange
        {
            Start = start,
            Length = length,
        };

        var request = new FormatDocumentRequest
        {
            ExpectedSnapshot = WorkspaceSnapshotTestFactory.CreatePrecondition(Guid.Parse("11111111-1111-1111-1111-111111111111")),
            Document = new DocumentSelector(),
            Range = range,
        };

        var target = new FormatDocumentTool();

        contextMocks.RequestResolver
            .Setup(item => item.ResolveDocument<MutationCandidate>(request.Document, contextMocks.MutationContext.Object))
            .Returns(ToolResolutionResult.Resolved<Document, MutationCandidate>(document.Document));

        contextMocks.RequestResolver
            .Setup(item => item.ValidateSnapshot<MutationCandidate>(contextMocks.MutationContext.Object, request.ExpectedSnapshot))
            .Returns((PluginExecutionResult<MutationCandidate>?)null);

        var result = await target.ExecuteAsync(request, contextMocks.MutationContext.Object, CancellationToken.None);
        var expectedError = new PluginExecutionError
        {
            Code = "InvalidRequest",
            Message = "The range must identify a span within the selected document.",
        };

        result.Outcome.Should().Be(PluginExecutionOutcome.Rejected);
        result.Error.Should().BeEquivalentTo(expectedError);
    }

    [Fact]
    public async Task GIVEN_RangeFormattingChangesDocument_WHEN_CallingExecuteAsync_THEN_ShouldReturnMutationCandidate()
    {
        const string source = "class Sample{void Execute(){var value=1;}}";
        using var document = RoslynTestFactory.CreateDocument(source, "Sample.cs");
        var contextMocks = MutationContextMockHelper.Create();
        var range = new TextSpanRange
        {
            Start = 0,
            Length = source.Length,
        };

        var request = new FormatDocumentRequest
        {
            ExpectedSnapshot = WorkspaceSnapshotTestFactory.CreatePrecondition(Guid.Parse("11111111-1111-1111-1111-111111111111")),
            Document = new DocumentSelector(),
            Range = range,
        };

        var target = new FormatDocumentTool();

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
        result.Data.Summary.Should().Be("Format 'Sample.cs'.");
    }
}
