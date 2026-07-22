namespace Roslyn.Workbench.Mcp.CodeActions.Test.Execution.Contexts;

public sealed class CodeActionExecutionContextTests
{
    [Fact]
    public void GIVEN_WorkspaceContext_WHEN_CreatingQueryContext_THEN_ShouldExposeOnlyWorkspaceExecutionState()
    {
        using var roslyn = RoslynTestFactory.CreateDocument("class C { }");
        var resolver = new Mock<IWorkspaceResolver>();
        var workspaceContext = CreateWorkspaceContext(roslyn.Solution, resolver.Object);

        var target = new CodeActionQueryContext(workspaceContext);

        target.CurrentSolution.Should().BeSameAs(roslyn.Solution);
        target.WorkspaceIdentity.Should().BeSameAs(workspaceContext.WorkspaceIdentity);
        target.TransactionRevision.Should().Be(2);
        target.DefaultMaxResults.Should().Be(100);
        target.WorkspaceResolver.Should().BeSameAs(resolver.Object);
        ((object)target).Should().NotBeAssignableTo<IWorkspaceMutationStager>();
    }

    [Fact]
    public void GIVEN_WorkspaceContext_WHEN_CreatingMutationContext_THEN_ShouldExposeOnlyWorkspaceExecutionState()
    {
        using var roslyn = RoslynTestFactory.CreateDocument("class C { }");
        var resolver = new Mock<IWorkspaceResolver>();
        var workspaceContext = CreateWorkspaceContext(roslyn.Solution, resolver.Object);

        var target = new CodeActionMutationContext(workspaceContext);

        target.CurrentSolution.Should().BeSameAs(roslyn.Solution);
        target.WorkspaceIdentity.Should().BeSameAs(workspaceContext.WorkspaceIdentity);
        target.TransactionRevision.Should().Be(2);
        target.DefaultMaxResults.Should().Be(100);
        target.WorkspaceResolver.Should().BeSameAs(resolver.Object);
        ((object)target).Should().NotBeAssignableTo<IWorkspaceMutationStager>();
    }

    private static WorkspaceExecutionContext CreateWorkspaceContext(
        Solution solution,
        IWorkspaceResolver resolver)
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
            resolver);
    }
}
