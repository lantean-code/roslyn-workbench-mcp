namespace Roslyn.Workbench.Mcp.CodeActions.Test;

public sealed class CodeActionToolRegistryTests
{
    [Fact]
    public void GIVEN_QueryAndMutationTools_WHEN_Registering_THEN_ShouldRetainClosedGenericRegistrations()
    {
        var target = new CodeActionToolRegistry();

        target.RegisterQueryTool<TestQueryHandler, TestRequest, TestResponse>(CreateMetadata("query"));
        target.RegisterMutationTool<TestMutationHandler, TestRequest>(CreateMetadata("mutation"));

        target.Tools.Should().HaveCount(2);
        target.Tools[0].Should().BeOfType<CodeActionQueryRegistration<TestQueryHandler, TestRequest, TestResponse>>();
        target.Tools[1].Should().BeOfType<CodeActionMutationRegistration<TestMutationHandler, TestRequest>>();
    }

    [Fact]
    public void GIVEN_QueryRegistration_WHEN_AcceptingVisitor_THEN_ShouldDispatchClosedGenericRegistration()
    {
        var target = new CodeActionToolRegistry();
        var visitor = new Mock<ICodeActionToolRegistrationVisitor<bool>>();
        target.RegisterQueryTool<TestQueryHandler, TestRequest, TestResponse>(CreateMetadata("query"));
        var registration = (CodeActionQueryRegistration<TestQueryHandler, TestRequest, TestResponse>)target.Tools.Single();
        visitor
            .Setup(item => item.VisitQuery(registration))
            .Returns(true);

        var result = registration.Accept(visitor.Object);

        result.Should().BeTrue();
        visitor.Verify(item => item.VisitQuery(registration), Times.Once);
    }

    [Fact]
    public void GIVEN_DuplicateName_WHEN_RegisteringSecondTool_THEN_ShouldThrowInvalidOperationException()
    {
        var target = new CodeActionToolRegistry();
        target.RegisterQueryTool<TestQueryHandler, TestRequest, TestResponse>(CreateMetadata("tool"));

        var action = () => target.RegisterMutationTool<TestMutationHandler, TestRequest>(CreateMetadata("tool"));

        action.Should().Throw<InvalidOperationException>();
    }

    private static CodeActionToolMetadata CreateMetadata(string name)
    {
        return new CodeActionToolMetadata
        {
            Name = name,
            Title = "Title",
            Description = "Description",
        };
    }

    public sealed record TestRequest : WorkspaceBoundRequest;

    public sealed record TestResponse;

    private sealed class TestQueryHandler : CodeActionQueryToolHandler<TestRequest, TestResponse>
    {
        protected override ValueTask<CodeActionExecutionResult<TestResponse>> ExecuteCoreAsync(
            TestRequest request,
            ICodeActionQueryContext context,
            CancellationToken cancellationToken)
        {
            return ValueTask.FromResult(CodeActionExecutionResult<TestResponse>.Success(new TestResponse()));
        }
    }

    private sealed class TestMutationHandler : CodeActionMutationToolHandler<TestRequest>
    {
        protected override ValueTask<CodeActionExecutionResult<WorkspaceMutationCandidate>> ExecuteCoreAsync(
            TestRequest request,
            ICodeActionMutationContext context,
            CancellationToken cancellationToken)
        {
            return ValueTask.FromResult(CodeActionExecutionResult<WorkspaceMutationCandidate>.Success(MutationCandidateTestData.CreateWorkspaceCandidate()));
        }
    }
}
