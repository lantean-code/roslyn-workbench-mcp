namespace Roslyn.Workbench.Mcp.CodeActions.Test.Refactorings;

public sealed class ConvertExpressionBodyToolTests
{
    [Fact]
    public void GIVEN_PluginRegistry_WHEN_CallingRegister_THEN_ShouldRegisterMutationTool()
    {
        var registry = new Mock<IPluginRegistry>();

        ConvertExpressionBodyTool.Register(registry.Object);

        registry.Verify(item => item.RegisterMutationTool<LocationRefactoringRequest>(
            It.Is<ToolRegistrationMetadata>(metadata =>
                metadata.Name == "convert-expression-body"
                && metadata.Title == "Convert Expression Body"
                && metadata.Description == "Stages a supported Roslyn block-body or expression-body conversion at the selected declaration."
                && metadata.Behavior.Destructive),
            It.IsAny<IMutationToolHandler<LocationRefactoringRequest>>()), Times.Once);
    }

    [Fact]
    public async Task GIVEN_PrimaryProviderReturnsNonUnavailableResult_WHEN_CallingExecuteAsync_THEN_ShouldReturnPrimaryResult()
    {
        var expected = PluginExecutionResult<MutationProposal>.Success(new MutationProposal());
        var context = new Mock<ICodeActionMutationContext>();
        var request = new LocationRefactoringRequest
        {
            Selection = new LocationSelector(),
            ExpectedSnapshot = new SnapshotPrecondition
            {
                WorkspaceEpoch = 1,
            },
        };
        var target = new ConvertExpressionBodyTool();

        context
            .Setup(item => item.StageReplaySelectionAsync(
                request.Selection,
                request.ExpectedSnapshot,
                CancellationToken.None,
                "Microsoft.CodeAnalysis.CSharp.UseExpressionBody.UseExpressionBodyCodeRefactoringProvider",
                null,
                null,
                null,
                null,
                null))
            .ReturnsAsync(expected);

        var result = await target.ExecuteAsync(request, context.Object, CancellationToken.None);

        result.Should().BeEquivalentTo(expected);
        context.Verify(item => item.StageReplaySelectionAsync(
            request.Selection,
            request.ExpectedSnapshot,
            CancellationToken.None,
            "Microsoft.CodeAnalysis.CSharp.UseExpressionBody.UseExpressionBodyCodeRefactoringProvider",
            null,
            null,
            null,
            null,
            null), Times.Once);
        context.Verify(item => item.StageReplaySelectionAsync(
            request.Selection,
            request.ExpectedSnapshot,
            CancellationToken.None,
            "Microsoft.CodeAnalysis.CSharp.UseExpressionBodyForLambda.UseExpressionBodyForLambdaCodeRefactoringProvider",
            null,
            null,
            null,
            null,
            null), Times.Never);
    }

    [Fact]
    public async Task GIVEN_PrimaryProviderReturnsDifferentRejection_WHEN_CallingExecuteAsync_THEN_ShouldReturnPrimaryRejection()
    {
        var expected = PluginExecutionResult<MutationProposal>.Rejected(new ToolError
        {
            Code = "InvalidRequest",
            Message = "InvalidRequest",
        });
        var context = new Mock<ICodeActionMutationContext>();
        var request = new LocationRefactoringRequest
        {
            Selection = new LocationSelector(),
            ExpectedSnapshot = new SnapshotPrecondition
            {
                WorkspaceEpoch = 1,
            },
        };
        var target = new ConvertExpressionBodyTool();

        context
            .Setup(item => item.StageReplaySelectionAsync(
                request.Selection,
                request.ExpectedSnapshot,
                CancellationToken.None,
                "Microsoft.CodeAnalysis.CSharp.UseExpressionBody.UseExpressionBodyCodeRefactoringProvider",
                null,
                null,
                null,
                null,
                null))
            .ReturnsAsync(expected);

        var result = await target.ExecuteAsync(request, context.Object, CancellationToken.None);

        result.Should().BeEquivalentTo(expected);
        context.Verify(item => item.StageReplaySelectionAsync(
            request.Selection,
            request.ExpectedSnapshot,
            CancellationToken.None,
            "Microsoft.CodeAnalysis.CSharp.UseExpressionBody.UseExpressionBodyCodeRefactoringProvider",
            null,
            null,
            null,
            null,
            null), Times.Once);
        context.Verify(item => item.StageReplaySelectionAsync(
            request.Selection,
            request.ExpectedSnapshot,
            CancellationToken.None,
            "Microsoft.CodeAnalysis.CSharp.UseExpressionBodyForLambda.UseExpressionBodyForLambdaCodeRefactoringProvider",
            null,
            null,
            null,
            null,
            null), Times.Never);
    }

    [Fact]
    public async Task GIVEN_PrimaryProviderReturnsRejectionWithoutError_WHEN_CallingExecuteAsync_THEN_ShouldReturnPrimaryRejection()
    {
        var expected = new PluginExecutionResult<MutationProposal>
        {
            Outcome = ToolOutcome.Rejected,
        };
        var context = new Mock<ICodeActionMutationContext>();
        var request = new LocationRefactoringRequest
        {
            Selection = new LocationSelector(),
            ExpectedSnapshot = new SnapshotPrecondition
            {
                WorkspaceEpoch = 1,
            },
        };
        var target = new ConvertExpressionBodyTool();

        context
            .Setup(item => item.StageReplaySelectionAsync(
                request.Selection,
                request.ExpectedSnapshot,
                CancellationToken.None,
                "Microsoft.CodeAnalysis.CSharp.UseExpressionBody.UseExpressionBodyCodeRefactoringProvider",
                null,
                null,
                null,
                null,
                null))
            .ReturnsAsync(expected);

        var result = await target.ExecuteAsync(request, context.Object, CancellationToken.None);

        result.Should().BeEquivalentTo(expected);
        context.Verify(item => item.StageReplaySelectionAsync(
            request.Selection,
            request.ExpectedSnapshot,
            CancellationToken.None,
            "Microsoft.CodeAnalysis.CSharp.UseExpressionBody.UseExpressionBodyCodeRefactoringProvider",
            null,
            null,
            null,
            null,
            null), Times.Once);
        context.Verify(item => item.StageReplaySelectionAsync(
            request.Selection,
            request.ExpectedSnapshot,
            CancellationToken.None,
            "Microsoft.CodeAnalysis.CSharp.UseExpressionBodyForLambda.UseExpressionBodyForLambdaCodeRefactoringProvider",
            null,
            null,
            null,
            null,
            null), Times.Never);
    }

    [Fact]
    public async Task GIVEN_PrimaryProviderReturnsUnavailableRejection_WHEN_CallingExecuteAsync_THEN_ShouldReturnLambdaProviderResult()
    {
        var expected = PluginExecutionResult<MutationProposal>.Success(new MutationProposal());
        var context = new Mock<ICodeActionMutationContext>();
        var request = new LocationRefactoringRequest
        {
            Selection = new LocationSelector(),
            ExpectedSnapshot = new SnapshotPrecondition
            {
                WorkspaceEpoch = 1,
            },
        };
        var target = new ConvertExpressionBodyTool();

        context
            .Setup(item => item.StageReplaySelectionAsync(
                request.Selection,
                request.ExpectedSnapshot,
                CancellationToken.None,
                "Microsoft.CodeAnalysis.CSharp.UseExpressionBody.UseExpressionBodyCodeRefactoringProvider",
                null,
                null,
                null,
                null,
                null))
            .ReturnsAsync(PluginExecutionResult<MutationProposal>.Rejected(new ToolError
            {
                Code = "CodeActionUnavailable",
                Message = "CodeActionUnavailable",
            }));
        context
            .Setup(item => item.StageReplaySelectionAsync(
                request.Selection,
                request.ExpectedSnapshot,
                CancellationToken.None,
                "Microsoft.CodeAnalysis.CSharp.UseExpressionBodyForLambda.UseExpressionBodyForLambdaCodeRefactoringProvider",
                null,
                null,
                null,
                null,
                null))
            .ReturnsAsync(expected);

        var result = await target.ExecuteAsync(request, context.Object, CancellationToken.None);

        result.Should().BeEquivalentTo(expected);
        context.Verify(item => item.StageReplaySelectionAsync(
            request.Selection,
            request.ExpectedSnapshot,
            CancellationToken.None,
            "Microsoft.CodeAnalysis.CSharp.UseExpressionBody.UseExpressionBodyCodeRefactoringProvider",
            null,
            null,
            null,
            null,
            null), Times.Once);
        context.Verify(item => item.StageReplaySelectionAsync(
            request.Selection,
            request.ExpectedSnapshot,
            CancellationToken.None,
            "Microsoft.CodeAnalysis.CSharp.UseExpressionBodyForLambda.UseExpressionBodyForLambdaCodeRefactoringProvider",
            null,
            null,
            null,
            null,
            null), Times.Once);
    }
}
