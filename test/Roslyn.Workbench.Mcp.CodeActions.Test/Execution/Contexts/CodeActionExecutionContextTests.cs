namespace Roslyn.Workbench.Mcp.CodeActions.Test.Execution.Contexts;

public sealed class CodeActionExecutionContextTests
{
    [Fact]
    public void GIVEN_WorkspaceContext_WHEN_CreatingQueryContext_THEN_ShouldExposeOnlyWorkspaceExecutionState()
    {
        using var roslyn = RoslynTestFactory.CreateDocument("class C { }");
        var resolver = new Mock<IWorkspaceResolver>();
        var workspacePathService = new Mock<IWorkspacePathService>();
        var workspaceContext = CreateWorkspaceContext(roslyn.Solution, workspacePathService.Object, resolver.Object);

        var target = new CodeActionQueryContext(workspaceContext);

        target.CurrentSolution.Should().BeSameAs(roslyn.Solution);
        target.WorkspaceIdentity.Should().BeSameAs(workspaceContext.WorkspaceIdentity);
        target.SnapshotIdentity.Should().Be(workspaceContext.SnapshotIdentity);
        target.TransactionRevision.Should().Be(2);
        target.DefaultMaxResults.Should().Be(100);
        target.WorkspacePathService.Should().BeSameAs(workspacePathService.Object);
        target.WorkspaceResolver.Should().BeSameAs(resolver.Object);
        ((object)target).Should().NotBeAssignableTo<IWorkspaceMutationStager>();
    }

    [Fact]
    public void GIVEN_WorkspaceContext_WHEN_CreatingMutationContext_THEN_ShouldExposeOnlyWorkspaceExecutionState()
    {
        using var roslyn = RoslynTestFactory.CreateDocument("class C { }");
        var resolver = new Mock<IWorkspaceResolver>();
        var workspacePathService = new Mock<IWorkspacePathService>();
        var workspaceContext = CreateWorkspaceContext(roslyn.Solution, workspacePathService.Object, resolver.Object);

        var target = new CodeActionMutationContext(workspaceContext);

        target.CurrentSolution.Should().BeSameAs(roslyn.Solution);
        target.WorkspaceIdentity.Should().BeSameAs(workspaceContext.WorkspaceIdentity);
        target.SnapshotIdentity.Should().Be(workspaceContext.SnapshotIdentity);
        target.TransactionRevision.Should().Be(2);
        target.DefaultMaxResults.Should().Be(100);
        target.WorkspacePathService.Should().BeSameAs(workspacePathService.Object);
        target.WorkspaceResolver.Should().BeSameAs(resolver.Object);
        ((object)target).Should().NotBeAssignableTo<IWorkspaceMutationStager>();
    }

    private static WorkspaceExecutionContext CreateWorkspaceContext(
        Solution solution,
        IWorkspacePathService workspacePathService,
        IWorkspaceResolver resolver)
    {
        var workspaceId = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var workspaceIdentity = new WorkspaceIdentity
        {
            WorkspaceId = workspaceId,
            WorkspaceEpoch = 1,
        };
        var snapshotIdentity = new WorkspaceSnapshotIdentity(
            workspaceId,
            1,
            WorkspaceSnapshotTestFactory.CreateId(1),
            new WorkspaceTransactionId(1));
        var snapshot = WorkspaceSnapshotTestFactory.CreatePrecondition(snapshotIdentity, transactionRevision: 2);

        return new WorkspaceExecutionContext(
            solution,
            workspaceIdentity,
            snapshotIdentity,
            snapshot,
            transactionRevision: 2,
            defaultMaxResults: 100,
            workspacePathService,
            resolver);
    }
}
