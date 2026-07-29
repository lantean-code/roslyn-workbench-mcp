using Microsoft.CodeAnalysis.Text;

namespace Roslyn.Workbench.Mcp.CodeActions.Test.Tools;

public sealed class DescribeCodeActionToolTests
{
    [Fact]
    public async Task GIVEN_CodeActionsAreUnavailable_WHEN_Executing_THEN_ShouldRejectWithoutResolvingAction()
    {
        var composition = new Mock<ICodeActionComposition>();
        var resolver = new Mock<ICodeActionResolver>();
        var infoFactory = new Mock<ICodeActionInfoFactory>();
        var context = new Mock<ICodeActionQueryContext>();
        composition.SetupGet(item => item.Status).Returns(CodeActionCompositionStatus.Unavailable("Unavailable."));

        var target = new DescribeCodeActionTool(composition.Object, resolver.Object, infoFactory.Object);

        var result = await target.ExecuteAsync(
            new DescribeCodeActionRequest
            {
                ActionId = Guid.Empty,
            },
            context.Object,
            CancellationToken.None);

        result.Error!.Code.Should().Be("CodeActionsUnavailable");
        resolver.Verify(item => item.ResolveActionAsync<DescribeCodeActionData>(
            It.IsAny<Guid>(),
            It.IsAny<SnapshotPrecondition?>(),
            It.IsAny<ICodeActionExecutionContext>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task GIVEN_ActionResolutionIsRejected_WHEN_Executing_THEN_ShouldReturnRejectionWithoutCreatingInfo()
    {
        var composition = new Mock<ICodeActionComposition>();
        var resolver = new Mock<ICodeActionResolver>();
        var infoFactory = new Mock<ICodeActionInfoFactory>();
        var context = new Mock<ICodeActionQueryContext>();
        var expectedSnapshot = new SnapshotPrecondition();
        var rejection = CodeActionExecutionResult.Rejected<DescribeCodeActionData>(new CodeActionExecutionError
        {
            Code = "ErrorCode",
            Message = "Message",
        });

        composition.SetupGet(item => item.Status).Returns(CodeActionCompositionStatus.Available());

        resolver
            .Setup(item => item.ResolveActionAsync<DescribeCodeActionData>(
                Guid.Empty,
                expectedSnapshot,
                context.Object,
                CancellationToken.None))
            .ReturnsAsync(CodeActionResolution.Rejected(rejection));

        var target = new DescribeCodeActionTool(composition.Object, resolver.Object, infoFactory.Object);

        var result = await target.ExecuteAsync(
            new DescribeCodeActionRequest
            {
                ActionId = Guid.Empty,
                ExpectedSnapshot = expectedSnapshot,
            },
            context.Object,
            CancellationToken.None);

        result.Should().BeSameAs(rejection);
        infoFactory.Verify(item => item.CreateFromReference(
            It.IsAny<DiscoveredCodeAction>(),
            It.IsAny<ICodeActionExecutionContext>(),
            It.IsAny<CodeActionDescriptorEntry>(),
            It.IsAny<CodeActionReference>(),
            It.IsAny<ResolvedLocation>()), Times.Never);
    }

    [Fact]
    public async Task GIVEN_ActionResolves_WHEN_Executing_THEN_ShouldReturnDescriptorWithOriginalReferenceAndContext()
    {
        using var roslyn = RoslynTestFactory.CreateDocument("class C { }");
        var composition = new Mock<ICodeActionComposition>();
        var resolver = new Mock<ICodeActionResolver>();
        var infoFactory = new Mock<ICodeActionInfoFactory>();
        var context = new Mock<ICodeActionQueryContext>();
        var workspaceResolver = new Mock<IWorkspaceResolver>();
        var action = CreateDiscoveredAction(roslyn.Solution);
        var descriptor = new CodeActionDescriptorEntry
        {
            ContextKind = CodeActionDescriptorContextKind.MemberSelection,
            Message = "Message",
        };

        action = action with { Descriptor = descriptor };
        var reference = new CodeActionReference(
            Guid.Empty,
            CodeActionExecutionTestFactory.CreateReplayRecipe(),
            new DateTimeOffset(2000, 1, 1, 0, 5, 0, TimeSpan.Zero));

        var info = new CodeActionInfo
        {
            ActionId = Guid.Empty,
            Title = "Title",
            ProviderId = "ProviderId",
            ExpiresAt = "2000-01-01T00:00:00.0000000+00:00",
            Location = SelectorTestFactory.CreateResolvedLocation("Code.cs", 1, 2),
        };

        composition.SetupGet(item => item.Status).Returns(CodeActionCompositionStatus.Available());

        workspaceResolver
            .Setup(item => item.CreateResolvedLocation(It.IsAny<Location>()))
            .Returns(info.Location);

        context.SetupGet(item => item.WorkspaceResolver).Returns(workspaceResolver.Object);
        resolver
            .Setup(item => item.ResolveActionAsync<DescribeCodeActionData>(
                Guid.Empty,
                null,
                context.Object,
                CancellationToken.None))
            .ReturnsAsync(CodeActionResolution.Resolved<DescribeCodeActionData>(
                action,
                roslyn.Document,
                new TextSpan(1, 2),
                reference));

        infoFactory
            .Setup(item => item.CreateFromReference(
                action,
                context.Object,
                descriptor,
                reference,
                info.Location))
            .Returns(info);

        var target = new DescribeCodeActionTool(composition.Object, resolver.Object, infoFactory.Object);

        var result = await target.ExecuteAsync(
            new DescribeCodeActionRequest
            {
                ActionId = Guid.Empty,
            },
            context.Object,
            CancellationToken.None);

        result.Outcome.Should().Be(CodeActionExecutionOutcome.Succeeded);
        result.Data!.Descriptor.Should().BeSameAs(info);
        result.Data.Context!.Kind.Should().Be(CodeActionDescriptorContextKind.MemberSelection);
        result.Data.Context.Message.Should().Be("Message");
        infoFactory.Verify(item => item.CreateFromReference(
            action,
            context.Object,
            descriptor,
            reference,
            info.Location), Times.Once);
    }

    [Fact]
    public async Task GIVEN_ActionLocationCannotBeProjected_WHEN_Executing_THEN_ShouldRejectWithoutCreatingInfo()
    {
        using var roslyn = RoslynTestFactory.CreateDocument("class C { }");
        var composition = new Mock<ICodeActionComposition>();
        var resolver = new Mock<ICodeActionResolver>();
        var infoFactory = new Mock<ICodeActionInfoFactory>();
        var context = new Mock<ICodeActionQueryContext>();
        var workspaceResolver = new Mock<IWorkspaceResolver>();
        var action = CreateDiscoveredAction(roslyn.Solution);
        var reference = new CodeActionReference(
            Guid.Empty,
            CodeActionExecutionTestFactory.CreateReplayRecipe(),
            new DateTimeOffset(2000, 1, 1, 0, 5, 0, TimeSpan.Zero));

        composition.SetupGet(item => item.Status).Returns(CodeActionCompositionStatus.Available());

        context.SetupGet(item => item.WorkspaceResolver).Returns(workspaceResolver.Object);
        resolver
            .Setup(item => item.ResolveActionAsync<DescribeCodeActionData>(
                Guid.Empty,
                null,
                context.Object,
                CancellationToken.None))
            .ReturnsAsync(CodeActionResolution.Resolved<DescribeCodeActionData>(
                action,
                roslyn.Document,
                new TextSpan(1, 2),
                reference));

        var target = new DescribeCodeActionTool(composition.Object, resolver.Object, infoFactory.Object);
        var result = await target.ExecuteAsync(
            new DescribeCodeActionRequest
            {
                ActionId = Guid.Empty,
            },
            context.Object,
            CancellationToken.None);

        result.Error!.Code.Should().Be("ActionUnavailable");
        result.RequiredAction.Should().Be(RequiredAction.ResolveTargetAgain);
        infoFactory.Verify(item => item.CreateFromReference(
            It.IsAny<DiscoveredCodeAction>(),
            It.IsAny<ICodeActionExecutionContext>(),
            It.IsAny<CodeActionDescriptorEntry>(),
            It.IsAny<CodeActionReference>(),
            It.IsAny<ResolvedLocation>()), Times.Never);
    }

    private static DiscoveredCodeAction CreateDiscoveredAction(Solution solution)
    {
        return new DiscoveredCodeAction
        {
            Action = Microsoft.CodeAnalysis.CodeActions.CodeAction.Create("Title", _ => Task.FromResult(solution), "EquivalenceKey"),
            Kind = DiscoveredActionKind.Refactoring,
            ProviderId = "ProviderId",
            Title = "Title",
            Descriptor = new CodeActionDescriptorEntry(),
            TargetSpan = new TextSpan(1, 2),
            EquivalenceKey = "EquivalenceKey",
        };
    }
}
