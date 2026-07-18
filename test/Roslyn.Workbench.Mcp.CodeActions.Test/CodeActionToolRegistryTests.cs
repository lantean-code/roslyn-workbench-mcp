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
        var query = target.Tools[0].Should().BeOfType<CodeActionQueryRegistration<TestQueryHandler, TestRequest, TestResponse>>().Subject;
        query.Metadata.Should().BeSameAs(target.Tools[0].Metadata);
        query.Metadata.Name.Should().Be("query");
        query.Kind.Should().Be(CodeActionToolKind.Query);
        query.RequestType.Should().Be(typeof(TestRequest));
        query.ResponseType.Should().Be(typeof(TestResponse));
        var mutation = target.Tools[1].Should().BeOfType<CodeActionMutationRegistration<TestMutationHandler, TestRequest>>().Subject;
        mutation.Metadata.Should().BeSameAs(target.Tools[1].Metadata);
        mutation.Metadata.Name.Should().Be("mutation");
        mutation.Kind.Should().Be(CodeActionToolKind.Mutation);
        mutation.RequestType.Should().Be(typeof(TestRequest));
        mutation.ResponseType.Should().Be(typeof(MutationData));
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

    [Fact]
    public void GIVEN_MutationRegistration_WHEN_AcceptingVisitor_THEN_ShouldDispatchClosedGenericRegistration()
    {
        var target = new CodeActionToolRegistry();
        var visitor = new Mock<ICodeActionToolRegistrationVisitor<bool>>();
        target.RegisterMutationTool<TestMutationHandler, TestRequest>(CreateMetadata("mutation"));
        var registration = (CodeActionMutationRegistration<TestMutationHandler, TestRequest>)target.Tools.Single();
        visitor
            .Setup(item => item.VisitMutation(registration))
            .Returns(true);

        var result = registration.Accept(visitor.Object);

        result.Should().BeTrue();
        visitor.Verify(item => item.VisitMutation(registration), Times.Once);
    }

    [Theory]
    [InlineData("", "Title", "Description")]
    [InlineData("Name", " ", "Description")]
    [InlineData("Name", "Title", "")]
    public void GIVEN_BlankRequiredMetadata_WHEN_Registering_THEN_ShouldThrowInvalidOperationException(
        string name,
        string title,
        string description)
    {
        var target = new CodeActionToolRegistry();
        var metadata = new CodeActionToolMetadata
        {
            Name = name,
            Title = title,
            Description = description,
        };

        var action = () => target.RegisterQueryTool<TestQueryHandler, TestRequest, TestResponse>(metadata);

        action.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void GIVEN_MinimumMetadata_WHEN_Registering_THEN_ShouldRetainOptionalDefaults()
    {
        var target = new CodeActionToolRegistry();
        var metadata = CreateMetadata("query");

        target.RegisterQueryTool<TestQueryHandler, TestRequest, TestResponse>(metadata);

        metadata.ResultSummary.Should().BeNull();
        metadata.Behavior.Should().BeEquivalentTo(new CodeActionToolBehavior());
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

#pragma warning disable CA1812 // Handler fixtures are consumed as closed generic registration metadata.
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
#pragma warning restore CA1812
}
