namespace Roslyn.Workbench.Mcp.CodeActions.Test.Refactorings;

public sealed class OrganizeImportsToolTests
{
    [Fact]
    public async Task GIVEN_SnapshotRejection_WHEN_CallingExecuteAsync_THEN_ShouldReturnRejectionBeforeResolvingDocument()
    {
        var context = new Mock<ICodeActionMutationContext>();
        var request = CreateRequest();
        var rejection = CodeActionExecutionResultFactory.Rejected<WorkspaceMutationCandidate>(
            "SnapshotMismatch",
            "Snapshot mismatch.");

        var selectionStager = new Mock<ICodeActionSelectionStager>();
        var requestResolver = new Mock<ICodeActionToolRequestResolver>();
        requestResolver
            .Setup(item => item.ValidateSnapshot<WorkspaceMutationCandidate>(
                context.Object,
                request.ExpectedSnapshot))
            .Returns(rejection);

        var target = new OrganizeImportsTool(selectionStager.Object, requestResolver.Object);

        var result = await target.ExecuteAsync(request, context.Object, CancellationToken.None);

        result.Should().BeEquivalentTo(rejection);
        selectionStager.Verify(item => item.StageSelectionAsync(
            It.IsAny<LocationSelector>(),
            It.IsAny<SnapshotPrecondition>(),
            It.IsAny<CancellationToken>(),
            It.IsAny<ICodeActionExecutionContext>(),
            It.IsAny<string>(),
            It.IsAny<string?>(),
            It.IsAny<string?>(),
            It.IsAny<string?>(),
            It.IsAny<string?>(),
            It.IsAny<IReadOnlyList<int>?>()), Times.Never);
    }

    [Theory]
    [InlineData(SelectorResolveStatus.NotFound, "DocumentNotFound")]
    [InlineData(SelectorResolveStatus.Ambiguous, "DocumentAmbiguous")]
    public async Task GIVEN_DocumentDoesNotResolve_WHEN_CallingExecuteAsync_THEN_ShouldReturnMappedRejection(
        SelectorResolveStatus status,
        string expectedCode)
    {
        var request = CreateRequest();
        var workspaceResolver = new Mock<IWorkspaceResolver>();
        workspaceResolver
            .Setup(item => item.ResolveDocument(request.Document))
            .Returns(SelectorTestFactory.CreateUnresolvedResult<Document>(status));

        var context = new Mock<ICodeActionMutationContext>();
        context.SetupGet(item => item.WorkspaceResolver).Returns(workspaceResolver.Object);

        var selectionStager = new Mock<ICodeActionSelectionStager>();
        var requestResolver = new Mock<ICodeActionToolRequestResolver>();
        var target = new OrganizeImportsTool(selectionStager.Object, requestResolver.Object);

        var result = await target.ExecuteAsync(request, context.Object, CancellationToken.None);

        result.Error!.Code.Should().Be(expectedCode);
        workspaceResolver.Verify(item => item.ResolveDocument(request.Document), Times.Once);
    }

    [Fact]
    public async Task GIVEN_DocumentWithoutImports_WHEN_CallingExecuteAsync_THEN_ShouldReturnNoChange()
    {
        using var roslyn = RoslynTestFactory.CreateDocument("namespace Sample; internal sealed class Item { }");
        var request = CreateRequest();
        var workspaceResolver = new Mock<IWorkspaceResolver>();
        workspaceResolver
            .Setup(item => item.ResolveDocument(request.Document))
            .Returns(SelectorResolveResult.Resolved(roslyn.Document));

        var context = new Mock<ICodeActionMutationContext>();
        context.SetupGet(item => item.WorkspaceResolver).Returns(workspaceResolver.Object);

        var selectionStager = new Mock<ICodeActionSelectionStager>();
        var requestResolver = new Mock<ICodeActionToolRequestResolver>();
        var target = new OrganizeImportsTool(selectionStager.Object, requestResolver.Object);

        var result = await target.ExecuteAsync(request, context.Object, CancellationToken.None);

        result.Outcome.Should().Be(CodeActionExecutionOutcome.NoChange);
        selectionStager.Verify(item => item.StageSelectionAsync(
            It.IsAny<LocationSelector>(),
            It.IsAny<SnapshotPrecondition>(),
            It.IsAny<CancellationToken>(),
            It.IsAny<ICodeActionExecutionContext>(),
            It.IsAny<string>(),
            It.IsAny<string?>(),
            It.IsAny<string?>(),
            It.IsAny<string?>(),
            It.IsAny<string?>(),
            It.IsAny<IReadOnlyList<int>?>()), Times.Never);
    }

    [Fact]
    public async Task GIVEN_DocumentWithoutSyntaxRoot_WHEN_CallingExecuteAsync_THEN_ShouldReturnDocumentRejection()
    {
        using var roslyn = RoslynTestFactory.CreateUnsupportedDocument();
        var request = CreateRequest();
        var workspaceResolver = new Mock<IWorkspaceResolver>();
        workspaceResolver
            .Setup(item => item.ResolveDocument(request.Document))
            .Returns(SelectorResolveResult.Resolved(roslyn.Document));

        var context = new Mock<ICodeActionMutationContext>();
        context.SetupGet(item => item.WorkspaceResolver).Returns(workspaceResolver.Object);

        var selectionStager = new Mock<ICodeActionSelectionStager>();
        var requestResolver = new Mock<ICodeActionToolRequestResolver>();
        var target = new OrganizeImportsTool(selectionStager.Object, requestResolver.Object);

        var result = await target.ExecuteAsync(request, context.Object, CancellationToken.None);

        result.Error!.Code.Should().Be("DocumentNotFound");
        selectionStager.Verify(item => item.StageSelectionAsync(
            It.IsAny<LocationSelector>(),
            It.IsAny<SnapshotPrecondition>(),
            It.IsAny<CancellationToken>(),
            It.IsAny<ICodeActionExecutionContext>(),
            It.IsAny<string>(),
            It.IsAny<string?>(),
            It.IsAny<string?>(),
            It.IsAny<string?>(),
            It.IsAny<string?>(),
            It.IsAny<IReadOnlyList<int>?>()), Times.Never);
    }

    [Fact]
    public async Task GIVEN_DocumentWithImports_WHEN_CallingExecuteAsync_THEN_ShouldStageOrganizeImportsAction()
    {
        using var roslyn = RoslynTestFactory.CreateDocument(
            "using System.Text;\r\nusing System;\r\n\r\nnamespace Sample;");

        var expected = CodeActionExecutionResult.Success(MutationCandidateTestData.CreateWorkspaceCandidate());
        var request = CreateRequest();
        var workspaceResolver = new Mock<IWorkspaceResolver>();
        workspaceResolver
            .Setup(item => item.ResolveDocument(request.Document))
            .Returns(SelectorResolveResult.Resolved(roslyn.Document));

        var context = new Mock<ICodeActionMutationContext>();
        context.SetupGet(item => item.WorkspaceResolver).Returns(workspaceResolver.Object);

        var selectionStager = new Mock<ICodeActionSelectionStager>();
        selectionStager
            .Setup(item => item.StageSelectionAsync(
                It.Is<LocationSelector>(selector =>
                    selector.Span != null
                    && selector.Span.Document == request.Document
                    && selector.Span.Start == 0
                    && selector.Span.Length == 0),
                request.ExpectedSnapshot,
                CancellationToken.None,
                context.Object,
                "Microsoft.CodeAnalysis.OrganizeImports.OrganizeImportsCodeRefactoringProvider",
                "Sort Usings",
                null,
                null,
                null,
                null))
            .ReturnsAsync(expected);

        var requestResolver = new Mock<ICodeActionToolRequestResolver>();
        var target = new OrganizeImportsTool(selectionStager.Object, requestResolver.Object);

        var result = await target.ExecuteAsync(request, context.Object, CancellationToken.None);

        result.Should().BeEquivalentTo(expected);
        selectionStager.Verify(item => item.StageSelectionAsync(
                It.Is<LocationSelector>(selector =>
                    selector.Span != null
                    && selector.Span.Document == request.Document
                    && selector.Span.Start == 0
                    && selector.Span.Length == 0),
                request.ExpectedSnapshot,
                CancellationToken.None,
                context.Object,
                "Microsoft.CodeAnalysis.OrganizeImports.OrganizeImportsCodeRefactoringProvider",
                "Sort Usings",
                null,
                null,
                null,
                null)
            , Times.Once);
    }

    private static OrganizeImportsRequest CreateRequest()
    {
        return new OrganizeImportsRequest
        {
            Document = new DocumentSelector
            {
                Path = "Document.cs",
            },
            ExpectedSnapshot = new SnapshotPrecondition
            {
                WorkspaceEpoch = 1,
            },
        };
    }
}
