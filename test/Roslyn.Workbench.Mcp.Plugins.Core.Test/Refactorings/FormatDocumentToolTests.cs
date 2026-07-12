namespace Roslyn.Workbench.Mcp.Plugins.Core.Test.Refactorings;

public sealed class FormatDocumentToolTests
{
    [Fact]
    public void GIVEN_PluginRegistry_WHEN_CallingRegister_THEN_ShouldRegisterMutationTool()
    {
        var registry = new Mock<IPluginRegistry>();

        FormatDocumentTool.Register(registry.Object);

        registry.Verify(item => item.RegisterMutationTool<FormatDocumentRequest>(
            It.Is<ToolRegistrationMetadata>(metadata =>
                metadata.Name == "format-document"
                && metadata.Title == "Format Document"
                && metadata.Description == "Stages Roslyn formatting for one document or one selected range."
                && metadata.Behavior.Destructive),
            It.IsAny<IMutationToolHandler<FormatDocumentRequest>>()), Times.Once);
    }

    [Fact]
    public async Task GIVEN_ResolveDocumentHasRejection_WHEN_CallingExecuteAsync_THEN_ShouldReturnRejectionResult()
    {
        var expected = PluginExecutionResult<MutationCandidate>.Rejected(new PluginExecutionError
        {
            Code = "DocumentNotFound",
            Message = "DocumentNotFound",
        });
        var contextMocks = MutationContextMockHelper.Create();
        var request = new FormatDocumentRequest
        {
            Document = new DocumentSelector(),
        };
        var target = new FormatDocumentTool();

        contextMocks.RequestResolver
            .Setup(item => item.ResolveDocument<MutationCandidate>(request.Document, contextMocks.MutationContext.Object))
            .Returns(new ToolResolutionResult<Document, MutationCandidate>
            {
                Rejection = expected,
            });

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
        var expected = PluginExecutionResult<MutationCandidate>.Conflict(new PluginExecutionError
        {
            Code = "SnapshotMismatch",
            Message = "SnapshotMismatch",
        });
        var contextMocks = MutationContextMockHelper.Create();
        var request = new FormatDocumentRequest
        {
            Document = new DocumentSelector(),
            ExpectedSnapshot = new SnapshotPrecondition
            {
                WorkspaceEpoch = 1,
            },
        };
        var target = new FormatDocumentTool();

        contextMocks.RequestResolver
            .Setup(item => item.ResolveDocument<MutationCandidate>(request.Document, contextMocks.MutationContext.Object))
            .Returns(new ToolResolutionResult<Document, MutationCandidate>
            {
                Value = document.Document,
            });
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
            Document = new DocumentSelector(),
        };
        var target = new FormatDocumentTool();

        contextMocks.RequestResolver
            .Setup(item => item.ResolveDocument<MutationCandidate>(request.Document, contextMocks.MutationContext.Object))
            .Returns(new ToolResolutionResult<Document, MutationCandidate>
            {
                Value = document.Document,
            });
        contextMocks.RequestResolver
            .Setup(item => item.ValidateSnapshot<MutationCandidate>(contextMocks.MutationContext.Object, request.ExpectedSnapshot))
            .Returns((PluginExecutionResult<MutationCandidate>?)null);

        var result = await target.ExecuteAsync(request, contextMocks.MutationContext.Object, CancellationToken.None);

        result.Outcome.Should().Be(PluginExecutionOutcome.NoChange);
    }

    [Fact]
    public async Task GIVEN_RangeFormattingChangesDocument_WHEN_CallingExecuteAsync_THEN_ShouldReturnMutationCandidate()
    {
        const string source = "class Sample{void Execute(){var value=1;}}";
        using var document = RoslynTestFactory.CreateDocument(source, "Sample.cs");
        var contextMocks = MutationContextMockHelper.Create();
        var request = new FormatDocumentRequest
        {
            Document = new DocumentSelector(),
            Range = new TextSpanSelector
            {
                Start = 0,
                Length = source.Length,
            },
        };
        var target = new FormatDocumentTool();

        contextMocks.RequestResolver
            .Setup(item => item.ResolveDocument<MutationCandidate>(request.Document, contextMocks.MutationContext.Object))
            .Returns(new ToolResolutionResult<Document, MutationCandidate>
            {
                Value = document.Document,
            });
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
