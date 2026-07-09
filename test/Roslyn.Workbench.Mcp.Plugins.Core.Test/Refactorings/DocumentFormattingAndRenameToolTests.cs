namespace Roslyn.Workbench.Mcp.Plugins.Core.Test.Refactorings;

public sealed class FormatDocumentToolTests
{
    [Fact]
    public async Task GIVEN_RequestResolverRejectsDocument_WHEN_CallingExecuteAsync_THEN_ShouldReturnResolverRejection()
    {
        var rejection = PluginExecutionResult<MutationProposal>.Rejected(new ToolError
        {
            Code = "DocumentNotFound",
            Message = "DocumentNotFound",
        });
        var requestResolver = new Mock<IToolRequestResolver>();
        requestResolver
            .Setup(item => item.ResolveDocument<MutationProposal>(It.IsAny<DocumentSelector?>(), It.IsAny<IToolExecutionContext>()))
            .Returns(new ToolResolutionResult<Document, MutationProposal>
            {
                Rejection = rejection,
            });
        var target = new FormatDocumentTool();
        var context = new MutationContextBuilder()
            .WithToolExecutionServices(new ToolExecutionServicesBuilder().WithRequestResolver(requestResolver.Object).Build())
            .Build();

        var result = await target.ExecuteAsync(new FormatDocumentRequest
        {
            Document = new DocumentSelector
            {
                Path = "Sample.cs",
            },
        }, context, CancellationToken.None);

        result.Should().BeEquivalentTo(rejection);
    }

    [Fact]
    public async Task GIVEN_RequestResolverRejectsSnapshot_WHEN_CallingExecuteAsync_THEN_ShouldReturnSnapshotConflict()
    {
        using var workspace = MiniWorkspaceFactory.CreateCSharp("namespace Sample;");
        var document = workspace.Solution.Projects.Single().Documents.Single();
        var rejection = PluginExecutionResult<MutationProposal>.Conflict(new ToolError
        {
            Code = "SnapshotMismatch",
            Message = "SnapshotMismatch",
        });
        var requestResolver = new Mock<IToolRequestResolver>();
        requestResolver
            .Setup(item => item.ResolveDocument<MutationProposal>(It.IsAny<DocumentSelector?>(), It.IsAny<IToolExecutionContext>()))
            .Returns(new ToolResolutionResult<Document, MutationProposal>
            {
                Value = document,
            });
        requestResolver
            .Setup(item => item.ValidateSnapshot<MutationProposal>(It.IsAny<IToolExecutionContext>(), It.IsAny<SnapshotPrecondition?>()))
            .Returns(rejection);
        var target = new FormatDocumentTool();
        var context = new MutationContextBuilder()
            .WithCurrentSolution(workspace.Solution)
            .WithToolExecutionServices(new ToolExecutionServicesBuilder().WithRequestResolver(requestResolver.Object).Build())
            .Build();

        var result = await target.ExecuteAsync(new FormatDocumentRequest
        {
            Document = new DocumentSelector
            {
                Path = "Sample.cs",
            },
            ExpectedSnapshot = new SnapshotPrecondition
            {
                WorkspaceEpoch = 1,
            },
        }, context, CancellationToken.None);

        result.Should().BeEquivalentTo(rejection);
    }

    [Fact]
    public async Task GIVEN_CancellationIsRequested_WHEN_CallingExecuteAsync_THEN_ShouldThrowOperationCanceledException()
    {
        var target = new FormatDocumentTool();
        using var cancellationTokenSource = new CancellationTokenSource();
        cancellationTokenSource.Cancel();

        var act = () => target.ExecuteAsync(new FormatDocumentRequest(), new MutationContextBuilder().Build(), cancellationTokenSource.Token).AsTask();

        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task GIVEN_FormatProducesNoChanges_WHEN_CallingExecuteAsync_THEN_ShouldReturnNoChange()
    {
        using var workspace = MiniWorkspaceFactory.CreateCSharp("""
            namespace Sample;

            public sealed class Formatter
            {
                public int Value { get; }
            }
            """);
        var target = new FormatDocumentTool();
        var context = DocumentFormattingAndRenameToolTestHelpers.CreateWorkspaceMutationContext(workspace);

        var result = await target.ExecuteAsync(new FormatDocumentRequest
        {
            Document = new DocumentSelector
            {
                Path = "Sample.cs",
            },
            ExpectedSnapshot = new SnapshotPrecondition
            {
                WorkspaceEpoch = 1,
            },
        }, context, CancellationToken.None);

        result.Outcome.Should().Be(ToolOutcome.NoChange);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task GIVEN_RangeIsSpecifiedAndFormattingChangesDocument_WHEN_CallingExecuteAsync_THEN_ShouldReturnMutationProposal()
    {
        using var workspace = MiniWorkspaceFactory.CreateCSharp("""
            namespace Sample;

            public sealed class Formatter
            {
                public void Execute()
                {
                    var value=1;
                }
            }
            """);
        var target = new FormatDocumentTool();
        var context = DocumentFormattingAndRenameToolTestHelpers.CreateWorkspaceMutationContext(workspace);

        var result = await target.ExecuteAsync(new FormatDocumentRequest
        {
            Document = new DocumentSelector
            {
                Path = "Sample.cs",
            },
            Range = new TextSpanSelector
            {
                Document = new DocumentSelector
                {
                    Path = "Sample.cs",
                },
                Start = 0,
                Length = 200,
            },
            ExpectedSnapshot = new SnapshotPrecondition
            {
                WorkspaceEpoch = 1,
            },
        }, context, CancellationToken.None);

        result.Outcome.Should().Be(ToolOutcome.Succeeded);
        var proposal = result.Data;
        proposal.Should().NotBeNull();
        proposal!.Summary.Should().Be("Format 'Sample.cs'.");
        var candidateSolution = proposal.CandidateSolution;
        candidateSolution.Should().NotBeNull();
        var formattedText = await candidateSolution!.Projects.Single().Documents.Single().GetTextAsync(TestContext.Current.CancellationToken);
        formattedText.ToString().Should().Contain("var value = 1;");
    }
}

public sealed class RenameSymbolToolTests
{
    [Fact]
    public async Task GIVEN_SymbolResolutionHasRejection_WHEN_CallingExecuteAsync_THEN_ShouldReturnResolverRejection()
    {
        var rejection = PluginExecutionResult<MutationProposal>.Rejected(new ToolError
        {
            Code = "SymbolNotFound",
            Message = "SymbolNotFound",
        });
        var requestResolver = new Mock<IToolRequestResolver>();
        requestResolver
            .Setup(item => item.ResolveSymbolAsync<MutationProposal>(
                It.IsAny<SymbolSelector?>(),
                It.IsAny<SnapshotPrecondition?>(),
                It.IsAny<IToolExecutionContext>(),
                It.IsAny<CancellationToken>()))
            .Returns(new ValueTask<ToolResolutionResult<ISymbol, MutationProposal>>(new ToolResolutionResult<ISymbol, MutationProposal>
            {
                Rejection = rejection,
            }));
        var target = new RenameSymbolTool();
        var context = new MutationContextBuilder()
            .WithToolExecutionServices(new ToolExecutionServicesBuilder().WithRequestResolver(requestResolver.Object).Build())
            .Build();

        var result = await target.ExecuteAsync(new RenameSymbolRequest
        {
            Symbol = new SymbolSelector(),
            NewName = "NewName",
        }, context, CancellationToken.None);

        result.Should().BeEquivalentTo(rejection);
    }

    [Fact]
    public async Task GIVEN_NewNameIsWhitespace_WHEN_CallingExecuteAsync_THEN_ShouldReturnInvalidRequest()
    {
        var requestResolver = new Mock<IToolRequestResolver>();
        requestResolver
            .Setup(item => item.ResolveSymbolAsync<MutationProposal>(
                It.IsAny<SymbolSelector?>(),
                It.IsAny<SnapshotPrecondition?>(),
                It.IsAny<IToolExecutionContext>(),
                It.IsAny<CancellationToken>()))
            .Returns(new ValueTask<ToolResolutionResult<ISymbol, MutationProposal>>(new ToolResolutionResult<ISymbol, MutationProposal>
            {
                Value = Mock.Of<ISymbol>(),
            }));
        var target = new RenameSymbolTool();
        var context = new MutationContextBuilder()
            .WithToolExecutionServices(new ToolExecutionServicesBuilder().WithRequestResolver(requestResolver.Object).Build())
            .Build();

        var result = await target.ExecuteAsync(new RenameSymbolRequest
        {
            Symbol = new SymbolSelector(),
            NewName = " ",
        }, context, CancellationToken.None);

        result.Outcome.Should().Be(ToolOutcome.Rejected);
        result.Error!.Code.Should().Be("InvalidRequest");
    }

    [Fact]
    public async Task GIVEN_CancellationIsRequested_WHEN_CallingExecuteAsync_THEN_ShouldThrowOperationCanceledException()
    {
        var target = new RenameSymbolTool();
        using var cancellationTokenSource = new CancellationTokenSource();
        cancellationTokenSource.Cancel();

        var act = () => target.ExecuteAsync(new RenameSymbolRequest(), new MutationContextBuilder().Build(), cancellationTokenSource.Token).AsTask();

        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task GIVEN_NewNameMatchesExistingName_WHEN_CallingExecuteAsync_THEN_ShouldReturnSucceededResult()
    {
        using var workspace = MiniWorkspaceFactory.CreateCSharp("""
            namespace Sample;

            public sealed class ExistingName
            {
            }
            """);
        var target = new RenameSymbolTool();
        var context = DocumentFormattingAndRenameToolTestHelpers.CreateWorkspaceMutationContext(workspace);

        var result = await target.ExecuteAsync(new RenameSymbolRequest
        {
            Symbol = new SymbolSelector
            {
                Location = workspace.GetLocationSelector("ExistingName"),
            },
            NewName = "ExistingName",
            ExpectedSnapshot = new SnapshotPrecondition
            {
                WorkspaceEpoch = 1,
            },
        }, context, CancellationToken.None);

        result.Outcome.Should().Be(ToolOutcome.Succeeded);
        var proposal = result.Data;
        proposal.Should().NotBeNull();
        proposal!.Summary.Should().Be("Rename 'ExistingName' to 'ExistingName'.");
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task GIVEN_RenameChangesSolution_WHEN_CallingExecuteAsync_THEN_ShouldReturnMutationProposal()
    {
        using var workspace = MiniWorkspaceFactory.CreateCSharp("""
            namespace Sample;

            public sealed class ExistingName
            {
            }
            """);
        var target = new RenameSymbolTool();
        var context = DocumentFormattingAndRenameToolTestHelpers.CreateWorkspaceMutationContext(workspace);

        var result = await target.ExecuteAsync(new RenameSymbolRequest
        {
            Symbol = new SymbolSelector
            {
                Location = workspace.GetLocationSelector("ExistingName"),
            },
            NewName = "UpdatedName",
            ExpectedSnapshot = new SnapshotPrecondition
            {
                WorkspaceEpoch = 1,
            },
        }, context, CancellationToken.None);

        result.Outcome.Should().Be(ToolOutcome.Succeeded);
        var proposal = result.Data;
        proposal.Should().NotBeNull();
        proposal!.Summary.Should().Be("Rename 'ExistingName' to 'UpdatedName'.");
        var candidateSolution = proposal.CandidateSolution;
        candidateSolution.Should().NotBeNull();
        var updatedText = await candidateSolution!.Projects.Single().Documents.Single().GetTextAsync(TestContext.Current.CancellationToken);
        updatedText.ToString().Should().Contain("UpdatedName");
    }
}

internal static class DocumentFormattingAndRenameToolTestHelpers
{
    public static IMutationContext CreateWorkspaceMutationContext(MiniWorkspace workspace)
    {
        var workspaceIdentity = workspace.CreateWorkspaceIdentity();
        return new MutationContextBuilder()
            .WithCurrentSolution(workspace.Solution)
            .WithResolver(workspace.CreateResolver(workspaceIdentity))
            .WithWorkspaceIdentity(workspaceIdentity)
            .Build();
    }
}
