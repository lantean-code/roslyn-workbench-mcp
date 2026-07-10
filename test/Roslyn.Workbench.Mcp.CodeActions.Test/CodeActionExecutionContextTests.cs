namespace Roslyn.Workbench.Mcp.CodeActions.Test;

public sealed class CodeActionExecutionContextTests
{
    [Fact]
    public async Task GIVEN_QueryContext_WHEN_ListingAndDescribing_THEN_ShouldDelegateToWorkflowWithNarrowContext()
    {
        using var roslyn = RoslynTestFactory.CreateDocument("class C { }");
        var workflow = new Mock<ICodeActionQueryWorkflow>();
        var workspaceContext = CreateWorkspaceContext(roslyn.Solution);
        var listRequest = new ListCodeActionsRequest();
        var describeRequest = new DescribeCodeActionRequest();
        var listResult = CodeActionExecutionResult<CodeActionListData>.Success(new CodeActionListData());
        var describeResult = CodeActionExecutionResult<DescribeCodeActionData>.Success(new DescribeCodeActionData());
        var target = new CodeActionQueryContext(workspaceContext, workflow.Object);
        workflow.Setup(item => item.ListCodeActionsAsync(listRequest, target, CancellationToken.None)).ReturnsAsync(listResult);
        workflow.Setup(item => item.DescribeCodeActionAsync(describeRequest, target, CancellationToken.None)).ReturnsAsync(describeResult);

        var listed = await target.ListCodeActionsAsync(listRequest, CancellationToken.None);
        var described = await target.DescribeCodeActionAsync(describeRequest, CancellationToken.None);

        listed.Should().BeSameAs(listResult);
        described.Should().BeSameAs(describeResult);
        ((object)target).Should().NotBeAssignableTo<IWorkspaceMutationStager>();
        workflow.Verify(item => item.ListCodeActionsAsync(listRequest, target, CancellationToken.None), Times.Once);
        workflow.Verify(item => item.DescribeCodeActionAsync(describeRequest, target, CancellationToken.None), Times.Once);
    }

    [Fact]
    public async Task GIVEN_MutationContextWithoutSelection_WHEN_StagingReplaySelection_THEN_ShouldRejectWithoutCallingWorkflow()
    {
        using var roslyn = RoslynTestFactory.CreateDocument("class C { }");
        var workflow = new Mock<ICodeActionMutationWorkflow>();
        var target = new CodeActionMutationContext(CreateWorkspaceContext(roslyn.Solution), workflow.Object);

        var result = await target.StageReplaySelectionAsync(
            null,
            null,
            CancellationToken.None,
            "ProviderId");

        result.Outcome.Should().Be(CodeActionExecutionOutcome.Rejected);
        result.Error!.Code.Should().Be("InvalidRequest");
        workflow.Verify(
            item => item.StageReplayCodeActionAsync(It.IsAny<ReplayCodeActionRequest>(), It.IsAny<ICodeActionMutationContext>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task GIVEN_MutationContextWithSelection_WHEN_StagingReplaySelection_THEN_ShouldMapAndDelegateRequest()
    {
        using var roslyn = RoslynTestFactory.CreateDocument("class C { }");
        var workflow = new Mock<ICodeActionMutationWorkflow>();
        var selection = new LocationSelector();
        var expected = CodeActionExecutionResult<WorkspaceMutationProposal>.Success(new WorkspaceMutationProposal());
        var target = new CodeActionMutationContext(CreateWorkspaceContext(roslyn.Solution), workflow.Object);
        workflow
            .Setup(item => item.StageReplayCodeActionAsync(
                It.Is<ReplayCodeActionRequest>(request => request.Location == selection && request.ProviderId == "ProviderId" && request.Title == "Title"),
                target,
                CancellationToken.None))
            .ReturnsAsync(expected);

        var result = await target.StageReplaySelectionAsync(
            selection,
            null,
            CancellationToken.None,
            "ProviderId",
            title: "Title");

        result.Should().BeSameAs(expected);
        workflow.Verify(item => item.StageReplayCodeActionAsync(It.IsAny<ReplayCodeActionRequest>(), target, CancellationToken.None), Times.Once);
    }

    private static WorkspaceExecutionContext CreateWorkspaceContext(Solution solution)
    {
        return new WorkspaceExecutionContext(
            solution,
            new WorkspaceIdentity
            {
                WorkspaceId = "WorkspaceId",
                WorkspaceEpoch = 1,
            },
            transactionRevision: 2,
            defaultMaxResults: 100,
            Mock.Of<IWorkspaceResolver>());
    }
}
