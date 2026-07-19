using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeRefactorings;
using Microsoft.CodeAnalysis.Text;

namespace Roslyn.Workbench.Mcp.CodeActions.Test.Execution;

public sealed class CodeActionReplayServiceTests
{
    private readonly Mock<ICodeActionProviderCatalog> _providerCatalog;
    private readonly Mock<ICodeActionDiscoveryService> _discoveryService;
    private readonly Mock<ICodeActionResolutionService> _resolutionService;
    private readonly Mock<ICodeActionOperationService> _operationService;
    private readonly Mock<ICodeActionDescriptorRegistry> _descriptorRegistry;
    private readonly Mock<ICodeActionExecutionContext> _context;
    private readonly Mock<IWorkspaceResolver> _workspaceResolver;
    private readonly CodeActionReplayService _target;

    public CodeActionReplayServiceTests()
    {
        _providerCatalog = new Mock<ICodeActionProviderCatalog>();
        _discoveryService = new Mock<ICodeActionDiscoveryService>();
        _resolutionService = new Mock<ICodeActionResolutionService>();
        _operationService = new Mock<ICodeActionOperationService>();
        _descriptorRegistry = new Mock<ICodeActionDescriptorRegistry>();
        _context = new Mock<ICodeActionExecutionContext>();
        _workspaceResolver = new Mock<IWorkspaceResolver>();
        _providerCatalog.SetupGet(item => item.Status).Returns(new CodeActionProviderCatalogStatus
        {
            IsAvailable = true,
        });
        _workspaceResolver
            .Setup(item => item.ValidateSnapshot(It.IsAny<SnapshotPrecondition?>()))
            .Returns(SnapshotMatchResult.Matched());
        _context.SetupGet(item => item.WorkspaceResolver).Returns(_workspaceResolver.Object);
        _target = new CodeActionReplayService(
            _providerCatalog.Object,
            _discoveryService.Object,
            _resolutionService.Object,
            _operationService.Object,
            _descriptorRegistry.Object);
    }

    [Fact]
    public async Task GIVEN_CodeActionsAreUnavailable_WHEN_StagingCodeAction_THEN_ShouldRejectWithoutResolvingAction()
    {
        _providerCatalog.SetupGet(item => item.Status).Returns(new CodeActionProviderCatalogStatus
        {
            IsAvailable = false,
        });

        var result = await _target.StageCodeActionAsync(
            new StageCodeActionRequest
            {
                ActionId = "ActionId",
            },
            _context.Object,
            CancellationToken.None);

        result.Outcome.Should().Be(CodeActionExecutionOutcome.Rejected);
        result.Error!.Code.Should().Be("CodeActionsUnavailable");
        _resolutionService.Verify(item => item.ResolveActionAsync<WorkspaceMutationCandidate>(
            It.IsAny<string>(),
            It.IsAny<SnapshotPrecondition?>(),
            It.IsAny<DiscoveredActionKind?>(),
            It.IsAny<ICodeActionExecutionContext>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task GIVEN_CodeActionsAreUnavailable_WHEN_StagingCodeFix_THEN_ShouldRejectWithoutResolvingAction()
    {
        _providerCatalog.SetupGet(item => item.Status).Returns(new CodeActionProviderCatalogStatus
        {
            IsAvailable = false,
        });

        var result = await _target.StageCodeFixAsync(
            new StageCodeFixRequest
            {
                ActionId = "ActionId",
            },
            _context.Object,
            CancellationToken.None);

        result.Error!.Code.Should().Be("CodeActionsUnavailable");
        _resolutionService.Verify(item => item.ResolveActionAsync<WorkspaceMutationCandidate>(
            It.IsAny<string>(),
            It.IsAny<SnapshotPrecondition?>(),
            It.IsAny<DiscoveredActionKind?>(),
            It.IsAny<ICodeActionExecutionContext>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task GIVEN_ResolvedRefactoring_WHEN_StagingCodeAction_THEN_ShouldCreateMutationCandidate()
    {
        using var roslyn = RoslynTestFactory.CreateDocument("class C { }");
        var expectedSnapshot = new SnapshotPrecondition();
        var action = CreateAction(roslyn.Solution, "Title", "EquivalenceKey");
        var candidate = new WorkspaceMutationCandidate
        {
            CandidateSolution = roslyn.Solution,
        };
        _resolutionService
            .Setup(item => item.ResolveActionAsync<WorkspaceMutationCandidate>(
                "ActionId",
                expectedSnapshot,
                DiscoveredActionKind.Refactoring,
                _context.Object,
                CancellationToken.None))
            .ReturnsAsync(CreateResolution(action, roslyn.Document, CodeActionExecutionMode.Replay));
        _operationService
            .Setup(item => item.CreateMutationCandidateAsync(action, "Title", _context.Object, CancellationToken.None))
            .ReturnsAsync(CodeActionExecutionResult<WorkspaceMutationCandidate>.Success(candidate));

        var result = await _target.StageCodeActionAsync(
            new StageCodeActionRequest
            {
                ActionId = "ActionId",
                ExpectedSnapshot = expectedSnapshot,
            },
            _context.Object,
            CancellationToken.None);

        result.Data.Should().BeSameAs(candidate);
        _operationService.Verify(item => item.CreateMutationCandidateAsync(
            action,
            "Title",
            _context.Object,
            CancellationToken.None), Times.Once);
    }

    [Fact]
    public async Task GIVEN_ResolutionIsRejected_WHEN_StagingCodeFix_THEN_ShouldReturnResolutionRejection()
    {
        var rejection = CodeActionExecutionResult<WorkspaceMutationCandidate>.Rejected(new CodeActionExecutionError
        {
            Code = "ErrorCode",
            Message = "Message",
        });
        _resolutionService
            .Setup(item => item.ResolveActionAsync<WorkspaceMutationCandidate>(
                "ActionId",
                null,
                DiscoveredActionKind.CodeFix,
                _context.Object,
                CancellationToken.None))
            .ReturnsAsync(new CodeActionResolution<WorkspaceMutationCandidate>
            {
                Rejection = rejection,
            });

        var result = await _target.StageCodeFixAsync(
            new StageCodeFixRequest
            {
                ActionId = "ActionId",
            },
            _context.Object,
            CancellationToken.None);

        result.Should().BeSameAs(rejection);
        _operationService.Verify(item => item.CreateMutationCandidateAsync(
            It.IsAny<CodeAction>(),
            It.IsAny<string>(),
            It.IsAny<ICodeActionExecutionContext>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task GIVEN_ResolvedActionRequiresParameters_WHEN_StagingCodeAction_THEN_ShouldRejectWithoutCreatingCandidate()
    {
        using var roslyn = RoslynTestFactory.CreateDocument("class C { }");
        var action = CreateAction(roslyn.Solution, "Title", "EquivalenceKey");
        _resolutionService
            .Setup(item => item.ResolveActionAsync<WorkspaceMutationCandidate>(
                "ActionId",
                null,
                DiscoveredActionKind.Refactoring,
                _context.Object,
                CancellationToken.None))
            .ReturnsAsync(CreateResolution(action, roslyn.Document, CodeActionExecutionMode.Parameterised));

        var result = await _target.StageCodeActionAsync(
            new StageCodeActionRequest
            {
                ActionId = "ActionId",
            },
            _context.Object,
            CancellationToken.None);

        result.Error!.Code.Should().Be("ActionRequiresParameters");
        _operationService.Verify(item => item.CreateMutationCandidateAsync(
            It.IsAny<CodeAction>(),
            It.IsAny<string>(),
            It.IsAny<ICodeActionExecutionContext>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task GIVEN_SelectionIsMissing_WHEN_StagingSelection_THEN_ShouldRejectWithoutDiscoveringActions()
    {
        var result = await _target.StageSelectionAsync(
            null,
            null,
            CancellationToken.None,
            _context.Object,
            "ProviderId");

        result.Outcome.Should().Be(CodeActionExecutionOutcome.Rejected);
        result.Error!.Code.Should().Be("InvalidRequest");
        _discoveryService.Verify(item => item.GetMatchingRefactoringProviders(It.IsAny<string?>()), Times.Never);
    }

    [Fact]
    public async Task GIVEN_CancellationIsRequested_WHEN_ReplayingCodeAction_THEN_ShouldThrowBeforeCheckingAvailability()
    {
        using var cancellationSource = new CancellationTokenSource();
        await cancellationSource.CancelAsync();

        var action = async () => await _target.StageReplayCodeActionAsync(
            new ReplayCodeActionRequest(),
            _context.Object,
            cancellationSource.Token);

        await action.Should().ThrowAsync<OperationCanceledException>();
        _providerCatalog.VerifyGet(item => item.Status, Times.Never);
    }

    [Fact]
    public async Task GIVEN_CodeActionsAreUnavailable_WHEN_ReplayingCodeAction_THEN_ShouldRejectBeforeValidatingSnapshot()
    {
        _providerCatalog.SetupGet(item => item.Status).Returns(new CodeActionProviderCatalogStatus
        {
            IsAvailable = false,
        });

        var result = await _target.StageReplayCodeActionAsync(
            new ReplayCodeActionRequest(),
            _context.Object,
            CancellationToken.None);

        result.Error!.Code.Should().Be("CodeActionsUnavailable");
        _workspaceResolver.Verify(item => item.ValidateSnapshot(It.IsAny<SnapshotPrecondition?>()), Times.Never);
    }

    [Fact]
    public async Task GIVEN_SnapshotDoesNotMatch_WHEN_ReplayingCodeAction_THEN_ShouldReturnConflict()
    {
        var expectedSnapshot = new SnapshotPrecondition();
        _workspaceResolver
            .Setup(item => item.ValidateSnapshot(expectedSnapshot))
            .Returns(SnapshotMatchResult.TransactionRevisionMismatch());

        var result = await _target.StageReplayCodeActionAsync(
            new ReplayCodeActionRequest
            {
                ExpectedSnapshot = expectedSnapshot,
            },
            _context.Object,
            CancellationToken.None);

        result.Outcome.Should().Be(CodeActionExecutionOutcome.Conflict);
        result.Error!.Code.Should().Be("SnapshotMismatch");
    }

    [Fact]
    public async Task GIVEN_LocationIsMissing_WHEN_ReplayingCodeAction_THEN_ShouldRejectRequest()
    {
        var result = await _target.StageReplayCodeActionAsync(
            new ReplayCodeActionRequest(),
            _context.Object,
            CancellationToken.None);

        result.Error!.Code.Should().Be("InvalidRequest");
        _workspaceResolver.Verify(item => item.ResolveLocationAsync(
            It.IsAny<LocationSelector>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Theory]
    [InlineData(SelectorResolveStatus.NotFound, "LocationNotFound")]
    [InlineData(SelectorResolveStatus.Ambiguous, "LocationAmbiguous")]
    public async Task GIVEN_LocationDoesNotResolve_WHEN_ReplayingCodeAction_THEN_ShouldReturnResolutionRejection(
        SelectorResolveStatus status,
        string expectedCode)
    {
        var selector = new LocationSelector();
        _workspaceResolver
            .Setup(item => item.ResolveLocationAsync(selector, CancellationToken.None))
            .ReturnsAsync(new SelectorResolveResult<Location>
            {
                Status = status,
            });

        var result = await _target.StageReplayCodeActionAsync(
            new ReplayCodeActionRequest
            {
                Location = selector,
            },
            _context.Object,
            CancellationToken.None);

        result.Error!.Code.Should().Be(expectedCode);
    }

    [Fact]
    public async Task GIVEN_ResolvedStatusHasNoLocation_WHEN_ReplayingCodeAction_THEN_ShouldRejectLocation()
    {
        var selector = new LocationSelector();
        _workspaceResolver
            .Setup(item => item.ResolveLocationAsync(selector, CancellationToken.None))
            .ReturnsAsync(new SelectorResolveResult<Location>
            {
                Status = SelectorResolveStatus.Resolved,
            });

        var result = await _target.StageReplayCodeActionAsync(
            new ReplayCodeActionRequest
            {
                Location = selector,
            },
            _context.Object,
            CancellationToken.None);

        result.Error!.Code.Should().Be("LocationNotFound");
    }

    [Fact]
    public async Task GIVEN_LocationIsNotOwnedByCurrentSolution_WHEN_ReplayingCodeAction_THEN_ShouldRejectLocation()
    {
        using var currentRoslyn = RoslynTestFactory.CreateDocument("class C { }");
        using var otherRoslyn = RoslynTestFactory.CreateDocument("class D { }");
        var selector = new LocationSelector();
        var location = await CreateLocationAsync(otherRoslyn.Document);
        _workspaceResolver
            .Setup(item => item.ResolveLocationAsync(selector, CancellationToken.None))
            .ReturnsAsync(SelectorResolveResult<Location>.Resolved(location));
        _context.SetupGet(item => item.CurrentSolution).Returns(currentRoslyn.Solution);

        var result = await _target.StageReplayCodeActionAsync(
            new ReplayCodeActionRequest
            {
                Location = selector,
            },
            _context.Object,
            CancellationToken.None);

        result.Error!.Code.Should().Be("LocationNotFound");
        _discoveryService.Verify(item => item.GetMatchingRefactoringProviders(It.IsAny<string?>()), Times.Never);
    }

    [Fact]
    public async Task GIVEN_NoProviderMatches_WHEN_ReplayingCodeAction_THEN_ShouldRejectAction()
    {
        using var roslyn = RoslynTestFactory.CreateDocument("class C { }");
        var selector = new LocationSelector();
        var location = await CreateLocationAsync(roslyn.Document);
        _workspaceResolver
            .Setup(item => item.ResolveLocationAsync(selector, CancellationToken.None))
            .ReturnsAsync(SelectorResolveResult<Location>.Resolved(location));
        _context.SetupGet(item => item.CurrentSolution).Returns(roslyn.Solution);
        _discoveryService
            .Setup(item => item.GetMatchingRefactoringProviders("ProviderId"))
            .Returns([]);

        var result = await _target.StageReplayCodeActionAsync(
            new ReplayCodeActionRequest
            {
                Location = selector,
                ProviderId = "ProviderId",
            },
            _context.Object,
            CancellationToken.None);

        result.Error!.Code.Should().Be("CodeActionUnavailable");
    }

    [Theory]
    [InlineData(ReplayFilter.Title)]
    [InlineData(ReplayFilter.TitleStartsWith)]
    [InlineData(ReplayFilter.TitleDoesNotContain)]
    [InlineData(ReplayFilter.EquivalenceKey)]
    [InlineData(ReplayFilter.ActionPath)]
    public async Task GIVEN_ActionDoesNotSatisfyFilter_WHEN_ReplayingCodeAction_THEN_ShouldRejectAction(ReplayFilter filter)
    {
        using var roslyn = RoslynTestFactory.CreateDocument("class C { }");
        var selector = new LocationSelector();
        var location = await CreateLocationAsync(roslyn.Document);
        var provider = new Mock<CodeRefactoringProvider>();
        var action = CreateDiscoveredAction(roslyn.Solution, "Title", "EquivalenceKey", [1]);
        var request = CreateMismatchingRequest(selector, filter);
        _workspaceResolver
            .Setup(item => item.ResolveLocationAsync(selector, CancellationToken.None))
            .ReturnsAsync(SelectorResolveResult<Location>.Resolved(location));
        _context.SetupGet(item => item.CurrentSolution).Returns(roslyn.Solution);
        _discoveryService
            .Setup(item => item.GetMatchingRefactoringProviders("ProviderId"))
            .Returns([provider.Object]);
        _discoveryService
            .Setup(item => item.DiscoverRefactoringsAsync(provider.Object, roslyn.Document, location.SourceSpan, CancellationToken.None))
            .ReturnsAsync([action]);

        var result = await _target.StageReplayCodeActionAsync(request, _context.Object, CancellationToken.None);

        result.Error!.Code.Should().Be("CodeActionUnavailable");
        _descriptorRegistry.Verify(item => item.Classify(
            It.IsAny<CodeAction>(),
            It.IsAny<string>(),
            It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task GIVEN_MatchingActionIsHidden_WHEN_ReplayingCodeAction_THEN_ShouldRejectAction()
    {
        using var roslyn = RoslynTestFactory.CreateDocument("class C { }");
        var selector = new LocationSelector();
        var location = await CreateLocationAsync(roslyn.Document);
        var provider = new Mock<CodeRefactoringProvider>();
        var action = CreateDiscoveredAction(roslyn.Solution, "Title", "EquivalenceKey", []);
        _workspaceResolver
            .Setup(item => item.ResolveLocationAsync(selector, CancellationToken.None))
            .ReturnsAsync(SelectorResolveResult<Location>.Resolved(location));
        _context.SetupGet(item => item.CurrentSolution).Returns(roslyn.Solution);
        _discoveryService
            .Setup(item => item.GetMatchingRefactoringProviders(null))
            .Returns([provider.Object]);
        _discoveryService
            .Setup(item => item.DiscoverRefactoringsAsync(provider.Object, roslyn.Document, location.SourceSpan, CancellationToken.None))
            .ReturnsAsync([action]);
        _descriptorRegistry
            .Setup(item => item.Classify(action.Action, "ProviderId", "Title"))
            .Returns(new CodeActionDescriptorEntry
            {
                IsVisible = false,
            });

        var result = await _target.StageReplayCodeActionAsync(
            new ReplayCodeActionRequest
            {
                Location = selector,
            },
            _context.Object,
            CancellationToken.None);

        result.Error!.Code.Should().Be("CodeActionUnavailable");
    }

    [Fact]
    public async Task GIVEN_MultipleDistinctActionsMatch_WHEN_ReplayingCodeAction_THEN_ShouldRejectAmbiguousSelection()
    {
        using var roslyn = RoslynTestFactory.CreateDocument("class C { }");
        var selector = new LocationSelector();
        var location = await CreateLocationAsync(roslyn.Document);
        var firstProvider = new Mock<CodeRefactoringProvider>();
        var secondProvider = new Mock<CodeRefactoringProvider>();
        var firstAction = CreateDiscoveredAction(roslyn.Solution, "FirstTitle", "FirstEquivalenceKey", [1]);
        var secondAction = CreateDiscoveredAction(roslyn.Solution, "SecondTitle", "SecondEquivalenceKey", [2]);
        _workspaceResolver
            .Setup(item => item.ResolveLocationAsync(selector, CancellationToken.None))
            .ReturnsAsync(SelectorResolveResult<Location>.Resolved(location));
        _context.SetupGet(item => item.CurrentSolution).Returns(roslyn.Solution);
        _discoveryService
            .Setup(item => item.GetMatchingRefactoringProviders(null))
            .Returns([firstProvider.Object, secondProvider.Object]);
        _discoveryService
            .Setup(item => item.DiscoverRefactoringsAsync(firstProvider.Object, roslyn.Document, location.SourceSpan, CancellationToken.None))
            .ReturnsAsync([firstAction]);
        _discoveryService
            .Setup(item => item.DiscoverRefactoringsAsync(secondProvider.Object, roslyn.Document, location.SourceSpan, CancellationToken.None))
            .ReturnsAsync([secondAction]);
        _descriptorRegistry
            .Setup(item => item.Classify(It.IsAny<CodeAction>(), It.IsAny<string>(), It.IsAny<string>()))
            .Returns(new CodeActionDescriptorEntry
            {
                ExecutionMode = CodeActionExecutionMode.Replay,
            });

        var result = await _target.StageReplayCodeActionAsync(
            new ReplayCodeActionRequest
            {
                Location = selector,
            },
            _context.Object,
            CancellationToken.None);

        result.Error!.Code.Should().Be("ActionAmbiguous");
        result.RequiredAction.Should().Be(RequiredAction.ResolveTargetAgain);
    }

    [Theory]
    [InlineData(CodeActionExecutionMode.Parameterised, "ActionRequiresParameters")]
    [InlineData(CodeActionExecutionMode.Unsupported, "CodeActionUnavailable")]
    public async Task GIVEN_SelectedActionCannotBeReplayed_WHEN_ReplayingCodeAction_THEN_ShouldRejectAction(
        CodeActionExecutionMode executionMode,
        string expectedCode)
    {
        using var roslyn = RoslynTestFactory.CreateDocument("class C { }");
        var selector = new LocationSelector();
        var location = await CreateLocationAsync(roslyn.Document);
        var provider = new Mock<CodeRefactoringProvider>();
        var action = CreateDiscoveredAction(roslyn.Solution, "Title", "EquivalenceKey", []);
        _workspaceResolver
            .Setup(item => item.ResolveLocationAsync(selector, CancellationToken.None))
            .ReturnsAsync(SelectorResolveResult<Location>.Resolved(location));
        _context.SetupGet(item => item.CurrentSolution).Returns(roslyn.Solution);
        _discoveryService
            .Setup(item => item.GetMatchingRefactoringProviders("ProviderId"))
            .Returns([provider.Object]);
        _discoveryService
            .Setup(item => item.DiscoverRefactoringsAsync(provider.Object, roslyn.Document, location.SourceSpan, CancellationToken.None))
            .ReturnsAsync([action]);
        _descriptorRegistry
            .Setup(item => item.Classify(action.Action, "ProviderId", "Title"))
            .Returns(new CodeActionDescriptorEntry
            {
                ExecutionMode = executionMode,
            });

        var result = await _target.StageReplayCodeActionAsync(
            new ReplayCodeActionRequest
            {
                Location = selector,
                ProviderId = "ProviderId",
            },
            _context.Object,
            CancellationToken.None);

        result.Error!.Code.Should().Be(expectedCode);
        _operationService.Verify(item => item.CreateMutationCandidateAsync(
            It.IsAny<CodeAction>(),
            It.IsAny<string>(),
            It.IsAny<ICodeActionExecutionContext>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task GIVEN_SelectionMatchesReplayableAction_WHEN_StagingSelection_THEN_ShouldCreateMutationCandidate()
    {
        using var roslyn = RoslynTestFactory.CreateDocument("class C { }");
        var selector = new LocationSelector();
        var expectedSnapshot = new SnapshotPrecondition();
        var location = await CreateLocationAsync(roslyn.Document);
        var provider = new Mock<CodeRefactoringProvider>();
        var action = CreateDiscoveredAction(roslyn.Solution, "Title", "EquivalenceKey", [1]);
        var candidate = new WorkspaceMutationCandidate
        {
            CandidateSolution = roslyn.Solution,
        };
        _workspaceResolver
            .Setup(item => item.ResolveLocationAsync(selector, CancellationToken.None))
            .ReturnsAsync(SelectorResolveResult<Location>.Resolved(location));
        _context.SetupGet(item => item.CurrentSolution).Returns(roslyn.Solution);
        _discoveryService
            .Setup(item => item.GetMatchingRefactoringProviders("ProviderId"))
            .Returns([provider.Object]);
        _discoveryService
            .Setup(item => item.DiscoverRefactoringsAsync(provider.Object, roslyn.Document, location.SourceSpan, CancellationToken.None))
            .ReturnsAsync([action]);
        _descriptorRegistry
            .Setup(item => item.Classify(action.Action, "ProviderId", "Title"))
            .Returns(new CodeActionDescriptorEntry
            {
                ExecutionMode = CodeActionExecutionMode.Replay,
            });
        _operationService
            .Setup(item => item.CreateMutationCandidateAsync(action.Action, "Title", _context.Object, CancellationToken.None))
            .ReturnsAsync(CodeActionExecutionResult<WorkspaceMutationCandidate>.Success(candidate));

        var result = await _target.StageSelectionAsync(
            selector,
            expectedSnapshot,
            CancellationToken.None,
            _context.Object,
            "ProviderId",
            "Title",
            "Tit",
            "Excluded",
            "EquivalenceKey",
            [1]);

        result.Data.Should().BeSameAs(candidate);
        _workspaceResolver.Verify(item => item.ValidateSnapshot(expectedSnapshot), Times.Once);
        _operationService.Verify(item => item.CreateMutationCandidateAsync(
            action.Action,
            "Title",
            _context.Object,
            CancellationToken.None), Times.Once);
    }

    [Fact]
    public async Task GIVEN_DuplicateReplayCandidates_WHEN_ReplayingCodeAction_THEN_ShouldExecuteOneCandidate()
    {
        using var roslyn = RoslynTestFactory.CreateDocument("class C { }");
        var selector = new LocationSelector();
        var location = await CreateLocationAsync(roslyn.Document);
        var provider = new Mock<CodeRefactoringProvider>();
        var firstAction = CreateDiscoveredAction(roslyn.Solution, "Title", "EquivalenceKey", [1]);
        var secondAction = CreateDiscoveredAction(roslyn.Solution, "Title", "EquivalenceKey", [1]);
        var candidate = new WorkspaceMutationCandidate
        {
            CandidateSolution = roslyn.Solution,
        };
        _workspaceResolver
            .Setup(item => item.ResolveLocationAsync(selector, CancellationToken.None))
            .ReturnsAsync(SelectorResolveResult<Location>.Resolved(location));
        _context.SetupGet(item => item.CurrentSolution).Returns(roslyn.Solution);
        _discoveryService
            .Setup(item => item.GetMatchingRefactoringProviders("ProviderId"))
            .Returns([provider.Object]);
        _discoveryService
            .Setup(item => item.DiscoverRefactoringsAsync(provider.Object, roslyn.Document, location.SourceSpan, CancellationToken.None))
            .ReturnsAsync([firstAction, secondAction]);
        _descriptorRegistry
            .Setup(item => item.Classify(It.IsAny<CodeAction>(), "ProviderId", "Title"))
            .Returns(new CodeActionDescriptorEntry
            {
                ExecutionMode = CodeActionExecutionMode.Replay,
            });
        _operationService
            .Setup(item => item.CreateMutationCandidateAsync(firstAction.Action, "Title", _context.Object, CancellationToken.None))
            .ReturnsAsync(CodeActionExecutionResult<WorkspaceMutationCandidate>.Success(candidate));

        var result = await _target.StageReplayCodeActionAsync(
            new ReplayCodeActionRequest
            {
                Location = selector,
                ProviderId = "ProviderId",
            },
            _context.Object,
            CancellationToken.None);

        result.Data.Should().BeSameAs(candidate);
        _operationService.Verify(item => item.CreateMutationCandidateAsync(
            firstAction.Action,
            "Title",
            _context.Object,
            CancellationToken.None), Times.Once);
    }

    private static ReplayCodeActionRequest CreateMismatchingRequest(LocationSelector selector, ReplayFilter filter)
    {
        return filter switch
        {
            ReplayFilter.Title => new ReplayCodeActionRequest
            {
                Location = selector,
                ProviderId = "ProviderId",
                Title = "OtherTitle",
            },
            ReplayFilter.TitleStartsWith => new ReplayCodeActionRequest
            {
                Location = selector,
                ProviderId = "ProviderId",
                TitleStartsWith = "Other",
            },
            ReplayFilter.TitleDoesNotContain => new ReplayCodeActionRequest
            {
                Location = selector,
                ProviderId = "ProviderId",
                TitleDoesNotContain = "Title",
            },
            ReplayFilter.EquivalenceKey => new ReplayCodeActionRequest
            {
                Location = selector,
                ProviderId = "ProviderId",
                EquivalenceKey = "OtherEquivalenceKey",
            },
            _ => new ReplayCodeActionRequest
            {
                Location = selector,
                ProviderId = "ProviderId",
                ActionPath = [2],
            },
        };
    }

    private static CodeActionResolution<WorkspaceMutationCandidate> CreateResolution(
        CodeAction action,
        Document document,
        CodeActionExecutionMode executionMode)
    {
        return new CodeActionResolution<WorkspaceMutationCandidate>
        {
            Action = new DiscoveredCodeAction
            {
                Action = action,
                Kind = DiscoveredActionKind.Refactoring,
                ProviderId = "ProviderId",
                Title = "Title",
                EquivalenceKey = "EquivalenceKey",
            },
            Descriptor = new CodeActionDescriptorEntry
            {
                ExecutionMode = executionMode,
            },
            Document = document,
        };
    }

    private static DiscoveredCodeAction CreateDiscoveredAction(
        Solution solution,
        string title,
        string equivalenceKey,
        IReadOnlyList<int> actionPath)
    {
        return new DiscoveredCodeAction
        {
            Action = CreateAction(solution, title, equivalenceKey),
            Kind = DiscoveredActionKind.Refactoring,
            ProviderId = "ProviderId",
            Title = title,
            EquivalenceKey = equivalenceKey,
            ActionPath = actionPath,
        };
    }

    private static CodeAction CreateAction(Solution solution, string title, string equivalenceKey)
    {
        return CodeAction.Create(title, _ => Task.FromResult(solution), equivalenceKey);
    }

    private static async Task<Location> CreateLocationAsync(Document document)
    {
        var syntaxTree = await document.GetSyntaxTreeAsync();
        return syntaxTree!.GetLocation(new TextSpan(0, 1));
    }

    #pragma warning disable CA1515 // The enum is part of a public xUnit theory method signature.
    public enum ReplayFilter
    {
        Title,
        TitleStartsWith,
        TitleDoesNotContain,
        EquivalenceKey,
        ActionPath,
    }
    #pragma warning restore CA1515
}
