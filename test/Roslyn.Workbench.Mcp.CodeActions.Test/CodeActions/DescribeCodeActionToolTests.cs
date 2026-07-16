using Microsoft.CodeAnalysis.Text;

namespace Roslyn.Workbench.Mcp.CodeActions.Test.Tools;

public sealed class DescribeCodeActionToolTests
{
    [Fact]
    public async Task GIVEN_CodeActionsAreUnavailable_WHEN_Executing_THEN_ShouldRejectWithoutResolvingAction()
    {
        var providerCatalog = new Mock<ICodeActionProviderCatalog>();
        var resolutionService = new Mock<ICodeActionResolutionService>();
        var infoFactory = new Mock<ICodeActionInfoFactory>();
        var context = new Mock<ICodeActionQueryContext>();
        providerCatalog.SetupGet(item => item.Status).Returns(new CodeActionProviderCatalogStatus
        {
            IsAvailable = false,
        });
        var target = new DescribeCodeActionTool(providerCatalog.Object, resolutionService.Object, infoFactory.Object);

        var result = await target.ExecuteAsync(
            new DescribeCodeActionRequest
            {
                ActionId = "ActionId",
            },
            context.Object,
            CancellationToken.None);

        result.Error!.Code.Should().Be("CodeActionsUnavailable");
        resolutionService.Verify(item => item.ResolveActionAsync<DescribeCodeActionData>(
            It.IsAny<string>(),
            It.IsAny<SnapshotPrecondition?>(),
            It.IsAny<DiscoveredActionKind?>(),
            It.IsAny<ICodeActionExecutionContext>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task GIVEN_ActionResolutionIsRejected_WHEN_Executing_THEN_ShouldReturnRejectionWithoutCreatingInfo()
    {
        var providerCatalog = new Mock<ICodeActionProviderCatalog>();
        var resolutionService = new Mock<ICodeActionResolutionService>();
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
        resolutionService
            .Setup(item => item.ResolveActionAsync<DescribeCodeActionData>(
                "ActionId",
                expectedSnapshot,
                null,
                context.Object,
                CancellationToken.None))
            .ReturnsAsync(new CodeActionResolution<DescribeCodeActionData>
            {
                Rejection = rejection,
            });
        var target = new DescribeCodeActionTool(providerCatalog.Object, resolutionService.Object, infoFactory.Object);

        var result = await target.ExecuteAsync(
            new DescribeCodeActionRequest
            {
                ActionId = "ActionId",
                ExpectedSnapshot = expectedSnapshot,
            },
            context.Object,
            CancellationToken.None);

        result.Should().BeSameAs(rejection);
        infoFactory.Verify(item => item.Create(
            It.IsAny<DiscoveredCodeAction>(),
            It.IsAny<ICodeActionExecutionContext>(),
            It.IsAny<Document>(),
            It.IsAny<TextSpan>(),
            It.IsAny<CodeActionDescriptorEntry>()), Times.Never);
    }

    [Fact]
    public async Task GIVEN_ActionResolves_WHEN_Executing_THEN_ShouldReturnRefreshedDescriptorAndContext()
    {
        using var roslyn = RoslynTestFactory.CreateDocument("class C { }");
        var providerCatalog = new Mock<ICodeActionProviderCatalog>();
        var resolutionService = new Mock<ICodeActionResolutionService>();
        var infoFactory = new Mock<ICodeActionInfoFactory>();
        var context = new Mock<ICodeActionQueryContext>();
        var action = CreateDiscoveredAction(roslyn.Solution);
        var descriptor = new CodeActionDescriptorEntry
        {
            ContextKind = CodeActionDescriptorContextKind.MemberSelection,
            Message = "Message",
        };
        var info = new CodeActionInfo
        {
            ActionId = "RefreshedActionId",
            Title = "Title",
            ProviderId = "ProviderId",
            ExpiresAt = "2000-01-01T00:00:00.0000000+00:00",
        };
        providerCatalog.SetupGet(item => item.Status).Returns(new CodeActionProviderCatalogStatus
        {
            IsAvailable = true,
        });
        resolutionService
            .Setup(item => item.ResolveActionAsync<DescribeCodeActionData>(
                "ActionId",
                null,
                null,
                context.Object,
                CancellationToken.None))
            .ReturnsAsync(new CodeActionResolution<DescribeCodeActionData>
            {
                Action = action,
                Descriptor = descriptor,
                Document = roslyn.Document,
                Span = new TextSpan(1, 2),
            });
        infoFactory
            .Setup(item => item.Create(action, context.Object, roslyn.Document, new TextSpan(1, 2), descriptor))
            .Returns(info);
        var target = new DescribeCodeActionTool(providerCatalog.Object, resolutionService.Object, infoFactory.Object);

        var result = await target.ExecuteAsync(
            new DescribeCodeActionRequest
            {
                ActionId = "ActionId",
            },
            context.Object,
            CancellationToken.None);

        result.Outcome.Should().Be(CodeActionExecutionOutcome.Succeeded);
        result.Data!.Descriptor.Should().BeSameAs(info);
        result.Data.Context!.Kind.Should().Be(CodeActionDescriptorContextKind.MemberSelection);
        result.Data.Context.Message.Should().Be("Message");
        infoFactory.Verify(item => item.Create(action, context.Object, roslyn.Document, new TextSpan(1, 2), descriptor), Times.Once);
    }

    private static DiscoveredCodeAction CreateDiscoveredAction(Solution solution)
    {
        return new DiscoveredCodeAction
        {
            Action = Microsoft.CodeAnalysis.CodeActions.CodeAction.Create("Title", _ => Task.FromResult(solution), "EquivalenceKey"),
            Kind = DiscoveredActionKind.Refactoring,
            ProviderId = "ProviderId",
            Title = "Title",
            EquivalenceKey = "EquivalenceKey",
        };
    }
}
