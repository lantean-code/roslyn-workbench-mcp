namespace Roslyn.Workbench.Mcp.CodeActions.Test.Refactorings;

public sealed class EncapsulateFieldToolTests
{
    [Fact]
    public async Task GIVEN_SymbolResolutionHasRejection_WHEN_CallingExecuteAsync_THEN_ShouldReturnRejectionResult()
    {
        var expected = CodeActionExecutionResult.Rejected<WorkspaceMutationCandidate>(new CodeActionExecutionError
        {
            Code = "SymbolNotFound",
            Message = "The symbol selector did not match any result.",
        }, RequiredAction.ResolveTargetAgain);

        var workspaceResolver = new Mock<IWorkspaceResolver>();
        var context = CreateContext(workspaceResolver);
        var request = CreateRequest();
        var selectionStager = new Mock<ICodeActionSelectionStager>();
        var target = CreateTarget(selectionStager.Object);

        workspaceResolver
            .Setup(item => item.ResolveSymbolAsync(request.Field!, CancellationToken.None))
            .ReturnsAsync(SelectorResolveResult.NotFound<ISymbol>());

        var result = await target.ExecuteAsync(request, context.Object, CancellationToken.None);

        result.Should().BeEquivalentTo(expected);
        workspaceResolver.Verify(item => item.CreateResolvedLocation(It.IsAny<Location>()), Times.Never);
        selectionStager.Verify(item => item.StageReplayCodeActionAsync(It.IsAny<ReplayCodeActionRequest>(), context.Object, It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task GIVEN_ResolvedSymbolIsNotField_WHEN_CallingExecuteAsync_THEN_ShouldReturnSymbolNotSupportedRejection()
    {
        var workspaceResolver = new Mock<IWorkspaceResolver>();
        var context = CreateContext(workspaceResolver);
        var request = CreateRequest();
        var symbol = new Mock<ISymbol>();
        var selectionStager = new Mock<ICodeActionSelectionStager>();
        var target = CreateTarget(selectionStager.Object);

        workspaceResolver
            .Setup(item => item.ResolveSymbolAsync(request.Field!, CancellationToken.None))
            .ReturnsAsync(SelectorResolveResult.Resolved(symbol.Object));

        var result = await target.ExecuteAsync(request, context.Object, CancellationToken.None);

        result.Outcome.Should().Be(CodeActionExecutionOutcome.Rejected);
        result.Error.Should().NotBeNull();
        result.Error!.Code.Should().Be("SymbolNotSupported");
        workspaceResolver.Verify(item => item.CreateResolvedLocation(It.IsAny<Location>()), Times.Never);
        selectionStager.Verify(item => item.StageReplayCodeActionAsync(It.IsAny<ReplayCodeActionRequest>(), context.Object, It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task GIVEN_FieldSymbolHasNoSourceLocation_WHEN_CallingExecuteAsync_THEN_ShouldReturnSymbolNotSupportedRejection()
    {
        var workspaceResolver = new Mock<IWorkspaceResolver>();
        var context = CreateContext(workspaceResolver);
        var request = CreateRequest();
        var field = CreateFieldSymbol("Field", []);
        var selectionStager = new Mock<ICodeActionSelectionStager>();
        var target = CreateTarget(selectionStager.Object);

        workspaceResolver
            .Setup(item => item.ResolveSymbolAsync(request.Field!, CancellationToken.None))
            .ReturnsAsync(SelectorResolveResult.Resolved<ISymbol>(field.Object));

        var result = await target.ExecuteAsync(request, context.Object, CancellationToken.None);

        result.Outcome.Should().Be(CodeActionExecutionOutcome.Rejected);
        result.Error.Should().NotBeNull();
        result.Error!.Code.Should().Be("SymbolNotSupported");
        workspaceResolver.Verify(item => item.CreateResolvedLocation(It.IsAny<Location>()), Times.Never);
        selectionStager.Verify(item => item.StageReplayCodeActionAsync(It.IsAny<ReplayCodeActionRequest>(), context.Object, It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task GIVEN_FieldSourceLocationCannotBeProjected_WHEN_CallingExecuteAsync_THEN_ShouldReturnSymbolNotSupportedRejection()
    {
        var workspaceResolver = new Mock<IWorkspaceResolver>();
        var context = CreateContext(workspaceResolver);
        var request = CreateRequest();
        var location = RoslynTestFactory.CreateSourceLocation();
        var field = CreateFieldSymbol("Field", [location]);
        var selectionStager = new Mock<ICodeActionSelectionStager>();
        var target = CreateTarget(selectionStager.Object);

        workspaceResolver
            .Setup(item => item.ResolveSymbolAsync(request.Field!, CancellationToken.None))
            .ReturnsAsync(SelectorResolveResult.Resolved<ISymbol>(field.Object));

        workspaceResolver
            .Setup(item => item.CreateResolvedLocation(location))
            .Returns((ResolvedLocation?)null);

        var result = await target.ExecuteAsync(request, context.Object, CancellationToken.None);

        result.Outcome.Should().Be(CodeActionExecutionOutcome.Rejected);
        result.Error.Should().NotBeNull();
        result.Error!.Code.Should().Be("SymbolNotSupported");
        selectionStager.Verify(item => item.StageReplayCodeActionAsync(It.IsAny<ReplayCodeActionRequest>(), context.Object, It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task GIVEN_UpdateReferencesIsTrue_WHEN_CallingExecuteAsync_THEN_ShouldStageReplayCodeActionUsingPropertyTitle()
    {
        var expected = CodeActionExecutionResult.Success(MutationCandidateTestData.CreateWorkspaceCandidate());
        var workspaceResolver = new Mock<IWorkspaceResolver>();
        var context = CreateContext(workspaceResolver);
        var request = CreateRequest(updateReferences: true);
        var location = RoslynTestFactory.CreateSourceLocation();
        var resolvedLocation = SelectorTestFactory.CreateResolvedLocation(location, "Code.cs");
        var field = CreateFieldSymbol("Field", [location]);
        var selectionStager = new Mock<ICodeActionSelectionStager>();
        var target = CreateTarget(selectionStager.Object);

        workspaceResolver
            .Setup(item => item.ResolveSymbolAsync(request.Field!, CancellationToken.None))
            .ReturnsAsync(SelectorResolveResult.Resolved<ISymbol>(field.Object));

        workspaceResolver
            .Setup(item => item.CreateResolvedLocation(location))
            .Returns(resolvedLocation);

        selectionStager
            .Setup(item => item.StageReplayCodeActionAsync(
                It.Is<ReplayCodeActionRequest>(replayRequest =>
                    replayRequest.ExpectedSnapshot == request.ExpectedSnapshot
                    && replayRequest.ProviderId == "Microsoft.CodeAnalysis.EncapsulateField.EncapsulateFieldRefactoringProvider"
                    && replayRequest.Title == "Encapsulate field: 'Field' (and use property)"
                    && replayRequest.EquivalenceKey == "Encapsulate_field_colon_0_and_use_property_Field"
                    && replayRequest.Location != null),
                context.Object, CancellationToken.None))
            .ReturnsAsync(expected);

        var result = await target.ExecuteAsync(request, context.Object, CancellationToken.None);

        result.Should().BeEquivalentTo(expected);
        selectionStager.Verify(item => item.StageReplayCodeActionAsync(
            It.Is<ReplayCodeActionRequest>(replayRequest =>
                replayRequest.ExpectedSnapshot == request.ExpectedSnapshot
                && replayRequest.ProviderId == "Microsoft.CodeAnalysis.EncapsulateField.EncapsulateFieldRefactoringProvider"
                && replayRequest.Title == "Encapsulate field: 'Field' (and use property)"
                && replayRequest.EquivalenceKey == "Encapsulate_field_colon_0_and_use_property_Field"
                && replayRequest.Location != null),
            context.Object, CancellationToken.None), Times.Once);
    }

    [Fact]
    public async Task GIVEN_UpdateReferencesIsFalse_WHEN_CallingExecuteAsync_THEN_ShouldStageReplayCodeActionUsingFieldTitle()
    {
        var expected = CodeActionExecutionResult.Success(MutationCandidateTestData.CreateWorkspaceCandidate());
        var workspaceResolver = new Mock<IWorkspaceResolver>();
        var context = CreateContext(workspaceResolver);
        var request = CreateRequest(updateReferences: false);
        var location = RoslynTestFactory.CreateSourceLocation();
        var resolvedLocation = SelectorTestFactory.CreateResolvedLocation(location, "Code.cs");
        var field = CreateFieldSymbol("Field", [location]);
        var selectionStager = new Mock<ICodeActionSelectionStager>();
        var target = CreateTarget(selectionStager.Object);

        workspaceResolver
            .Setup(item => item.ResolveSymbolAsync(request.Field!, CancellationToken.None))
            .ReturnsAsync(SelectorResolveResult.Resolved<ISymbol>(field.Object));

        workspaceResolver
            .Setup(item => item.CreateResolvedLocation(location))
            .Returns(resolvedLocation);

        selectionStager
            .Setup(item => item.StageReplayCodeActionAsync(
                It.Is<ReplayCodeActionRequest>(replayRequest =>
                    replayRequest.ExpectedSnapshot == request.ExpectedSnapshot
                    && replayRequest.ProviderId == "Microsoft.CodeAnalysis.EncapsulateField.EncapsulateFieldRefactoringProvider"
                    && replayRequest.Title == "Encapsulate field: 'Field' (but still use field)"
                    && replayRequest.EquivalenceKey == "Encapsulate_field_colon_0_but_still_use_field_Field"
                    && replayRequest.Location != null),
                context.Object, CancellationToken.None))
            .ReturnsAsync(expected);

        var result = await target.ExecuteAsync(request, context.Object, CancellationToken.None);

        result.Should().BeEquivalentTo(expected);
        selectionStager.Verify(item => item.StageReplayCodeActionAsync(
            It.Is<ReplayCodeActionRequest>(replayRequest =>
                replayRequest.ExpectedSnapshot == request.ExpectedSnapshot
                && replayRequest.ProviderId == "Microsoft.CodeAnalysis.EncapsulateField.EncapsulateFieldRefactoringProvider"
                && replayRequest.Title == "Encapsulate field: 'Field' (but still use field)"
                && replayRequest.EquivalenceKey == "Encapsulate_field_colon_0_but_still_use_field_Field"
                && replayRequest.Location != null),
            context.Object, CancellationToken.None), Times.Once);
    }

    private static Mock<ICodeActionMutationContext> CreateContext(Mock<IWorkspaceResolver> workspaceResolver)
    {
        var context = new Mock<ICodeActionMutationContext>();
        context
            .Setup(item => item.WorkspaceResolver)
            .Returns(workspaceResolver.Object);

        return context;
    }

    private static EncapsulateFieldRequest CreateRequest(bool updateReferences = true)
    {
        return new EncapsulateFieldRequest
        {
            Field = new SymbolSelector
            {
                DocumentationCommentId = "DocumentationCommentId",
            },
            ExpectedSnapshot = new SnapshotPrecondition
            {
                WorkspaceEpoch = 1,
            },
            UpdateReferences = updateReferences,
        };
    }

    private static EncapsulateFieldTool CreateTarget(ICodeActionSelectionStager selectionStager)
    {
        var requestResolver = new CodeActionToolRequestResolver(new CodeActionScopeResolver());

        return new EncapsulateFieldTool(selectionStager, requestResolver);
    }

    private static Mock<IFieldSymbol> CreateFieldSymbol(string name, IReadOnlyList<Location> locations)
    {
        var symbol = new Mock<IFieldSymbol>();

        symbol
            .Setup(item => item.Name)
            .Returns(name);

        symbol
            .Setup(item => item.Locations)
            .Returns(System.Collections.Immutable.ImmutableArray.CreateRange(locations));

        return symbol;
    }
}
