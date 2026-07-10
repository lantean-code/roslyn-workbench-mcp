namespace Roslyn.Workbench.Mcp.Plugins.Core.Test.Refactorings;

public sealed class SortUsingsToolTests
{
    [Fact]
    public void GIVEN_PluginRegistry_WHEN_CallingRegister_THEN_ShouldRegisterMutationTool()
    {
        var registry = new Mock<IPluginRegistry>();

        SortUsingsTool.Register(registry.Object);

        registry.Verify(item => item.RegisterMutationTool<SortUsingsRequest>(
            It.Is<ToolRegistrationMetadata>(metadata =>
                metadata.Name == "sort-usings"
                && metadata.Title == "Sort Usings"
                && metadata.Description == "Stages an ordered set of using directives for one document."
                && metadata.Behavior.Destructive),
            It.IsAny<IMutationToolHandler<SortUsingsRequest>>()), Times.Once);
    }

    [Fact]
    public async Task GIVEN_ResolveDocumentHasRejection_WHEN_CallingExecuteAsync_THEN_ShouldReturnRejectionResult()
    {
        var expected = PluginExecutionResult<MutationProposal>.Rejected(new PluginExecutionError
        {
            Code = "DocumentNotFound",
            Message = "DocumentNotFound",
        });
        var contextMocks = MutationContextMockHelper.Create();
        var request = new SortUsingsRequest
        {
            Document = new DocumentSelector(),
        };
        var target = new SortUsingsTool();

        contextMocks.RequestResolver
            .Setup(item => item.ResolveDocument<MutationProposal>(request.Document, contextMocks.MutationContext.Object))
            .Returns(new ToolResolutionResult<Document, MutationProposal>
            {
                Rejection = expected,
            });

        var result = await target.ExecuteAsync(request, contextMocks.MutationContext.Object, CancellationToken.None);

        result.Should().BeEquivalentTo(expected);
        contextMocks.RequestResolver.Verify(item => item.ValidateSnapshot<MutationProposal>(
            It.IsAny<IToolExecutionContext>(),
            It.IsAny<SnapshotPrecondition?>()), Times.Never);
    }

    [Fact]
    public async Task GIVEN_ValidateSnapshotHasRejection_WHEN_CallingExecuteAsync_THEN_ShouldReturnRejectionResult()
    {
        using var document = RoslynTestFactory.CreateDocument("using System;");
        var expected = PluginExecutionResult<MutationProposal>.Conflict(new PluginExecutionError
        {
            Code = "SnapshotMismatch",
            Message = "SnapshotMismatch",
        });
        var contextMocks = MutationContextMockHelper.Create();
        var request = new SortUsingsRequest
        {
            Document = new DocumentSelector(),
            ExpectedSnapshot = new SnapshotPrecondition
            {
                WorkspaceEpoch = 1,
            },
        };
        var target = new SortUsingsTool();

        contextMocks.RequestResolver
            .Setup(item => item.ResolveDocument<MutationProposal>(request.Document, contextMocks.MutationContext.Object))
            .Returns(new ToolResolutionResult<Document, MutationProposal>
            {
                Value = document.Document,
            });
        contextMocks.RequestResolver
            .Setup(item => item.ValidateSnapshot<MutationProposal>(contextMocks.MutationContext.Object, request.ExpectedSnapshot))
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
            Document = new DocumentSelector(),
        };
        var target = new SortUsingsTool();

        contextMocks.RequestResolver
            .Setup(item => item.ResolveDocument<MutationProposal>(request.Document, contextMocks.MutationContext.Object))
            .Returns(new ToolResolutionResult<Document, MutationProposal>
            {
                Value = document.Document,
            });
        contextMocks.RequestResolver
            .Setup(item => item.ValidateSnapshot<MutationProposal>(contextMocks.MutationContext.Object, request.ExpectedSnapshot))
            .Returns((PluginExecutionResult<MutationProposal>?)null);

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
            Document = new DocumentSelector(),
            SystemFirst = false,
        };
        var target = new SortUsingsTool();

        contextMocks.RequestResolver
            .Setup(item => item.ResolveDocument<MutationProposal>(request.Document, contextMocks.MutationContext.Object))
            .Returns(new ToolResolutionResult<Document, MutationProposal>
            {
                Value = document.Document,
            });
        contextMocks.RequestResolver
            .Setup(item => item.ValidateSnapshot<MutationProposal>(contextMocks.MutationContext.Object, request.ExpectedSnapshot))
            .Returns((PluginExecutionResult<MutationProposal>?)null);

        var result = await target.ExecuteAsync(request, contextMocks.MutationContext.Object, CancellationToken.None);

        result.Outcome.Should().Be(PluginExecutionOutcome.NoChange);
    }

    [Fact]
    public async Task GIVEN_SystemFirstReordersNamedAndAliasedUsings_WHEN_CallingExecuteAsync_THEN_ShouldReturnMutationProposal()
    {
        using var document = RoslynTestFactory.CreateDocument("using Z = Zeta;\r\nusing Alpha;\r\nusing System;\r\n", "Sample.cs");
        var contextMocks = MutationContextMockHelper.Create();
        var request = new SortUsingsRequest
        {
            Document = new DocumentSelector(),
            SystemFirst = true,
        };
        var target = new SortUsingsTool();

        contextMocks.RequestResolver
            .Setup(item => item.ResolveDocument<MutationProposal>(request.Document, contextMocks.MutationContext.Object))
            .Returns(new ToolResolutionResult<Document, MutationProposal>
            {
                Value = document.Document,
            });
        contextMocks.RequestResolver
            .Setup(item => item.ValidateSnapshot<MutationProposal>(contextMocks.MutationContext.Object, request.ExpectedSnapshot))
            .Returns((PluginExecutionResult<MutationProposal>?)null);

        var result = await target.ExecuteAsync(request, contextMocks.MutationContext.Object, CancellationToken.None);

        result.Outcome.Should().Be(PluginExecutionOutcome.Succeeded);
        result.Data.Should().NotBeNull();
        result.Data!.CandidateSolution.Should().NotBeSameAs(document.Solution);
        result.Data.Summary.Should().Be("Sort using directives in 'Sample.cs'.");
    }

    [Fact]
    public async Task GIVEN_UsingDirectiveHasNoName_WHEN_CallingExecuteAsync_THEN_ShouldSortUsingEmptyName()
    {
        using var document = RoslynTestFactory.CreateDocument("using Zeta;\r\nusing ;\r\n");
        var contextMocks = MutationContextMockHelper.Create();
        var request = new SortUsingsRequest
        {
            Document = new DocumentSelector(),
            SystemFirst = true,
        };
        var target = new SortUsingsTool();

        contextMocks.RequestResolver
            .Setup(item => item.ResolveDocument<MutationProposal>(request.Document, contextMocks.MutationContext.Object))
            .Returns(new ToolResolutionResult<Document, MutationProposal>
            {
                Value = document.Document,
            });
        contextMocks.RequestResolver
            .Setup(item => item.ValidateSnapshot<MutationProposal>(contextMocks.MutationContext.Object, request.ExpectedSnapshot))
            .Returns((PluginExecutionResult<MutationProposal>?)null);

        var result = await target.ExecuteAsync(request, contextMocks.MutationContext.Object, CancellationToken.None);

        result.Outcome.Should().Be(PluginExecutionOutcome.Succeeded);
    }
}
