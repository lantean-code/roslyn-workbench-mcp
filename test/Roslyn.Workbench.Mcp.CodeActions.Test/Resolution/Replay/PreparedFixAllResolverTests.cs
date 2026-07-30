using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;

namespace Roslyn.Workbench.Mcp.CodeActions.Test.Resolution.Replay;

public sealed class PreparedFixAllResolverTests
{
    private readonly Mock<ICodeActionDiscoveryService> _discoveryService;
    private readonly Mock<IFixAllActionFactory> _fixAllActionFactory;
    private readonly Mock<ICodeActionResolver> _resolver;
    private readonly Mock<ICodeActionExecutionContext> _context;
    private readonly PreparedFixAllResolver _target;

    public PreparedFixAllResolverTests()
    {
        _discoveryService = new Mock<ICodeActionDiscoveryService>();
        _fixAllActionFactory = new Mock<IFixAllActionFactory>();
        _resolver = new Mock<ICodeActionResolver>();
        _context = new Mock<ICodeActionExecutionContext>();
        _target = new PreparedFixAllResolver(
            _discoveryService.Object,
            _fixAllActionFactory.Object,
            _resolver.Object);
    }

    [Fact]
    public async Task GIVEN_OriginResolutionFails_WHEN_ResolvingPreparedFixAll_THEN_ShouldReturnOriginFailure()
    {
        var rejection = CodeActionExecutionResultFactory.ActionExpired<WorkspaceMutationCandidate>();
        var resolution = CodeActionResolution.Rejected(rejection);
        SetupResolution(resolution);

        var result = await _target.ResolveActionAsync<WorkspaceMutationCandidate>(
            Guid.Empty,
            null,
            _context.Object,
            TestContext.Current.CancellationToken);

        result.Should().BeSameAs(resolution);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, true)]
    public async Task GIVEN_RecipeOrActionIsNotPreparedCodeFix_WHEN_Resolving_THEN_ShouldRejectReference(
        bool hasScope,
        bool isRefactoring)
    {
        using var roslyn = RoslynTestFactory.CreateDocument("class C { }");
        var kind = isRefactoring
            ? DiscoveredActionKind.Refactoring
            : DiscoveredActionKind.CodeFix;

        SetupResolution(CreateResolution(
            roslyn.Document,
            kind,
            hasScope ? CodeActionFixAllScope.Document : null,
            [CodeActionFixAllScope.Document]));

        var result = await _target.ResolveActionAsync<WorkspaceMutationCandidate>(
            Guid.Empty,
            null,
            _context.Object,
            TestContext.Current.CancellationToken);

        result.Rejection!.Error!.Code.Should().Be("FixAllUnavailable");
        result.FailureKind.Should().Be(CodeActionResolutionFailureKind.InvalidReference);
    }

    [Fact]
    public async Task GIVEN_PreparedScopeIsNoLongerAdvertised_WHEN_Resolving_THEN_ShouldRejectReference()
    {
        using var roslyn = RoslynTestFactory.CreateDocument("class C { }");
        SetupResolution(CreateResolution(
            roslyn.Document,
            DiscoveredActionKind.CodeFix,
            CodeActionFixAllScope.Solution,
            [CodeActionFixAllScope.Document]));

        var result = await _target.ResolveActionAsync<WorkspaceMutationCandidate>(
            Guid.Empty,
            null,
            _context.Object,
            TestContext.Current.CancellationToken);

        result.FailureKind.Should().Be(CodeActionResolutionFailureKind.InvalidReference);
    }

    [Fact]
    public async Task GIVEN_ProviderHasNoFixAllProvider_WHEN_Resolving_THEN_ShouldRejectReference()
    {
        using var roslyn = RoslynTestFactory.CreateDocument("class C { }");
        SetupResolution(CreateResolution(
            roslyn.Document,
            DiscoveredActionKind.CodeFix,
            CodeActionFixAllScope.Document,
            [CodeActionFixAllScope.Document]));

        _discoveryService
            .Setup(item => item.FindCodeFixProvider("ProviderId"))
            .Returns(new Mock<CodeFixProvider>().Object);

        var result = await _target.ResolveActionAsync<WorkspaceMutationCandidate>(
            Guid.Empty,
            null,
            _context.Object,
            TestContext.Current.CancellationToken);

        result.FailureKind.Should().Be(CodeActionResolutionFailureKind.InvalidReference);
    }

    [Fact]
    public async Task GIVEN_ActionCreationFails_WHEN_Resolving_THEN_ShouldRejectReference()
    {
        using var roslyn = RoslynTestFactory.CreateDocument("class C { }");
        var (provider, fixAllProvider) = SetupProviderAndResolution(
            roslyn.Document,
            CodeActionFixAllScope.Document);

        _fixAllActionFactory
            .Setup(item => item.CreateDocumentAsync(
                provider,
                fixAllProvider,
                roslyn.Document,
                It.IsAny<IReadOnlyList<string>>(),
                It.IsAny<string?>(),
                TestContext.Current.CancellationToken))
            .ReturnsAsync(FixAllActionCreationResult.Failed(
                new FixAllActionCreationFailure { Message = "Creation failed." }));

        var result = await _target.ResolveActionAsync<WorkspaceMutationCandidate>(
            Guid.Empty,
            null,
            _context.Object,
            TestContext.Current.CancellationToken);

        result.Rejection!.Error!.Message.Should().Be("Creation failed.");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public async Task GIVEN_PreparedFixAllIsAvailable_WHEN_Resolving_THEN_ShouldRecreateScopedAction(
        int scopeValue)
    {
        var scope = (CodeActionFixAllScope)scopeValue;
        using var roslyn = RoslynTestFactory.CreateDocument("class C { }");
        var (provider, fixAllProvider) = SetupProviderAndResolution(roslyn.Document, scope);
        var fixAllAction = CodeAction.Create("Prepared", _ => Task.FromResult(roslyn.Solution));
        var creation = FixAllActionCreationResult.Created(fixAllAction);
        if (scope == CodeActionFixAllScope.Project)
        {
            _fixAllActionFactory
                .Setup(item => item.CreateProjectAsync(
                    provider,
                    fixAllProvider,
                    roslyn.Document.Project,
                    It.IsAny<IReadOnlyList<string>>(),
                    It.IsAny<string?>(),
                    TestContext.Current.CancellationToken))
                .ReturnsAsync(creation);
        }
        else if (scope == CodeActionFixAllScope.Document)
        {
            _fixAllActionFactory
                .Setup(item => item.CreateDocumentAsync(
                    provider,
                    fixAllProvider,
                    roslyn.Document,
                    It.IsAny<IReadOnlyList<string>>(),
                    It.IsAny<string?>(),
                    TestContext.Current.CancellationToken))
                .ReturnsAsync(creation);
        }
        else
        {
            _fixAllActionFactory
                .Setup(item => item.CreateSolutionAsync(
                    provider,
                    fixAllProvider,
                    roslyn.Document,
                    It.IsAny<IReadOnlyList<string>>(),
                    It.IsAny<string?>(),
                    TestContext.Current.CancellationToken))
                .ReturnsAsync(creation);
        }

        var result = await _target.ResolveActionAsync<WorkspaceMutationCandidate>(
            Guid.Empty,
            null,
            _context.Object,
            TestContext.Current.CancellationToken);

        result.HasRejection.Should().BeFalse();
        result.Action!.Action.Should().BeSameAs(fixAllAction);
        result.Action.Title.Should().Be("Fix all: Title");
    }

    private (CodeFixProvider Provider, FixAllProvider FixAllProvider) SetupProviderAndResolution(
        Document document,
        CodeActionFixAllScope scope)
    {
        SetupResolution(CreateResolution(
            document,
            DiscoveredActionKind.CodeFix,
            scope,
            [scope]));

        var provider = new Mock<CodeFixProvider>();
        var fixAllProvider = new Mock<FixAllProvider>();
        provider.Setup(item => item.GetFixAllProvider()).Returns(fixAllProvider.Object);
        _discoveryService.Setup(item => item.FindCodeFixProvider("ProviderId")).Returns(provider.Object);
        return (provider.Object, fixAllProvider.Object);
    }

    private void SetupResolution(CodeActionResolution<WorkspaceMutationCandidate> resolution)
    {
        _resolver
            .Setup(item => item.ResolveActionAsync<WorkspaceMutationCandidate>(
                Guid.Empty,
                null,
                _context.Object,
                TestContext.Current.CancellationToken))
            .ReturnsAsync(resolution);
    }

    private static CodeActionResolution<WorkspaceMutationCandidate> CreateResolution(
        Document document,
        DiscoveredActionKind kind,
        CodeActionFixAllScope? preparedScope,
        IReadOnlyList<CodeActionFixAllScope> advertisedScopes)
    {
        var action = new DiscoveredCodeAction
        {
            Action = CodeAction.Create("Title", _ => Task.FromResult(document.Project.Solution)),
            Kind = kind,
            ProviderId = "ProviderId",
            Title = "Title",
            TargetSpan = default,
            DiagnosticIds = ["DiagnosticId"],
            EquivalenceKey = "EquivalenceKey",
            FixAllScopes = advertisedScopes,
        };

        var reference = new CodeActionReference(
            Guid.Empty,
            CodeActionExecutionTestFactory.CreateReplayRecipe() with
            {
                PreparedFixAllScope = preparedScope,
            },
            new DateTimeOffset(2000, 1, 1, 0, 5, 0, TimeSpan.Zero));

        return CodeActionResolution.Resolved<WorkspaceMutationCandidate>(
            action,
            document,
            default,
            reference);
    }
}
