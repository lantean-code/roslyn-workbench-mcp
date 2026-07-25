using Microsoft.CodeAnalysis.Text;

namespace Roslyn.Workbench.Mcp.CodeActions.Test.Tools;

public sealed class DescribeCodeActionToolTests
{
    [Fact]
    public async Task GIVEN_CodeActionsAreUnavailable_WHEN_Executing_THEN_ShouldRejectWithoutResolvingAction()
    {
        var providerCatalog = new Mock<ICodeActionProviderCatalog>();
        var resolver = new Mock<ICodeActionResolver>();
        var infoFactory = new Mock<ICodeActionInfoFactory>();
        var context = new Mock<ICodeActionQueryContext>();
        providerCatalog.SetupGet(item => item.Status).Returns(new CodeActionProviderCatalogStatus
        {
            IsAvailable = false,
        });

        var target = new DescribeCodeActionTool(providerCatalog.Object, resolver.Object, infoFactory.Object);

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
            It.IsAny<DiscoveredActionKind?>(),
            It.IsAny<ICodeActionExecutionContext>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task GIVEN_ActionResolutionIsRejected_WHEN_Executing_THEN_ShouldReturnRejectionWithoutCreatingInfo()
    {
        var providerCatalog = new Mock<ICodeActionProviderCatalog>();
        var resolver = new Mock<ICodeActionResolver>();
        var infoFactory = new Mock<ICodeActionInfoFactory>();
        var context = new Mock<ICodeActionQueryContext>();
        var expectedSnapshot = new SnapshotPrecondition();
        var rejection = CodeActionExecutionResult<DescribeCodeActionData>.Rejected(new CodeActionExecutionError
        {
            Code = "ErrorCode",
            Message = "Message",
        });

        providerCatalog.SetupGet(item => item.Status).Returns(new CodeActionProviderCatalogStatus
        {
            IsAvailable = true,
        });

        resolver
            .Setup(item => item.ResolveActionAsync<DescribeCodeActionData>(
                Guid.Empty,
                expectedSnapshot,
                null,
                context.Object,
                CancellationToken.None))
            .ReturnsAsync(CodeActionResolution<DescribeCodeActionData>.Rejected(rejection));

        var target = new DescribeCodeActionTool(providerCatalog.Object, resolver.Object, infoFactory.Object);

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
            It.IsAny<CodeActionReference>()), Times.Never);
    }

    [Fact]
    public async Task GIVEN_ActionResolves_WHEN_Executing_THEN_ShouldReturnDescriptorWithOriginalReferenceAndContext()
    {
        using var roslyn = RoslynTestFactory.CreateDocument("class C { }");
        var providerCatalog = new Mock<ICodeActionProviderCatalog>();
        var resolver = new Mock<ICodeActionResolver>();
        var infoFactory = new Mock<ICodeActionInfoFactory>();
        var context = new Mock<ICodeActionQueryContext>();
        var action = CreateDiscoveredAction(roslyn.Solution);
        var descriptor = new CodeActionDescriptorEntry
        {
            ContextKind = CodeActionDescriptorContextKind.MemberSelection,
            Message = "Message",
        };

        action = action with { Descriptor = descriptor };
        var reference = new CodeActionReference(
            Guid.Empty,
            new CodeActionReplayRecipe(),
            new DateTimeOffset(2000, 1, 1, 0, 5, 0, TimeSpan.Zero));

        var info = new CodeActionInfo
        {
            ActionId = Guid.Empty,
            Title = "Title",
            ProviderId = "ProviderId",
            ExpiresAt = "2000-01-01T00:00:00.0000000+00:00",
        };

        providerCatalog.SetupGet(item => item.Status).Returns(new CodeActionProviderCatalogStatus
        {
            IsAvailable = true,
        });

        resolver
            .Setup(item => item.ResolveActionAsync<DescribeCodeActionData>(
                Guid.Empty,
                null,
                null,
                context.Object,
                CancellationToken.None))
            .ReturnsAsync(CodeActionResolution<DescribeCodeActionData>.Resolved(
                action,
                roslyn.Document,
                new TextSpan(1, 2),
                reference));

        infoFactory
            .Setup(item => item.CreateFromReference(
                action,
                context.Object,
                descriptor,
                reference))
            .Returns(info);

        var target = new DescribeCodeActionTool(providerCatalog.Object, resolver.Object, infoFactory.Object);

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
            reference), Times.Once);
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
            EquivalenceKey = "EquivalenceKey",
        };
    }
}
