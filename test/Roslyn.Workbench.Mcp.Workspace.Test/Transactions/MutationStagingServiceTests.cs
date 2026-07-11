using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using Roslyn.Workbench.Mcp.Workspace.Coordination;

namespace Roslyn.Workbench.Mcp.Workspace.Test.Transactions;

public sealed class MutationStagingServiceTests : IDisposable
{
    private readonly AdhocWorkspace _workspace;
    private readonly Mock<IWorkspaceOperationResultFactory> _resultFactory;
    private readonly Mock<IWorkspaceSessionStore> _sessionStore;
    private readonly Mock<IWorkspaceDiffBuilder> _diffBuilder;
    private readonly Mock<IWorkspaceResolverFactory> _resolverFactory;
    private readonly Mock<IWorkspaceInstanceStatusPublisher> _instanceStatusPublisher;
    private readonly MutationStagingService _target;

    public MutationStagingServiceTests()
    {
        _workspace = new AdhocWorkspace();
        _resultFactory = new Mock<IWorkspaceOperationResultFactory>();
        _sessionStore = new Mock<IWorkspaceSessionStore>();
        _diffBuilder = new Mock<IWorkspaceDiffBuilder>();
        _resolverFactory = new Mock<IWorkspaceResolverFactory>();
        _instanceStatusPublisher = new Mock<IWorkspaceInstanceStatusPublisher>();
        _target = new MutationStagingService(
            _resultFactory.Object,
            _sessionStore.Object,
            _diffBuilder.Object,
            _resolverFactory.Object,
            _instanceStatusPublisher.Object);
    }

    [Fact]
    public async Task GIVEN_CancelledToken_WHEN_Staging_THEN_ShouldPropagateCancellation()
    {
        using var cancellationSource = new CancellationTokenSource();
        cancellationSource.Cancel();

        var action = async () => await _target.StageAsync(
            "OperationName",
            new WorkspaceMutationProposal(),
            [],
            [],
            cancellationSource.Token);

        await action.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task GIVEN_NoTransactionOwner_WHEN_Staging_THEN_ShouldRequireTransaction()
    {
        var expected = CreateRejectedResult("TransactionRequired");
        _sessionStore.Setup(item => item.ReadSnapshot()).Returns(new WorkspaceHostSnapshot());
        _resultFactory
            .Setup(item => item.Rejected<MutationStagingOutcome>(
                WorkspaceErrorCodes.TransactionRequired,
                "Start a transaction before invoking mutation tools.",
                RequiredAction.StartTransaction,
                null,
                null,
                null))
            .Returns(expected);

        var result = await _target.StageAsync(
            "OperationName",
            new WorkspaceMutationProposal(),
            [],
            [],
            TestContext.Current.CancellationToken);

        result.Should().BeSameAs(expected);
        _sessionStore.Verify(item => item.ReadSession(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task GIVEN_OwnerWithoutTransaction_WHEN_Staging_THEN_ShouldRequireTransaction()
    {
        var expected = CreateRejectedResult("TransactionRequired");
        var session = CreateSession(transaction: null);
        SetupOwner(session);
        SetupTransactionRequiredResult(expected);

        var result = await _target.StageAsync(
            "OperationName",
            new WorkspaceMutationProposal(),
            [],
            [],
            TestContext.Current.CancellationToken);

        result.Should().BeSameAs(expected);
    }

    [Fact]
    public async Task GIVEN_MissingOwnerSession_WHEN_Staging_THEN_ShouldRequireTransaction()
    {
        var expected = CreateRejectedResult("TransactionRequired");
        _sessionStore.Setup(item => item.ReadSnapshot()).Returns(new WorkspaceHostSnapshot
        {
            TransactionOwnerWorkspaceId = "WorkspaceId",
        });
        _sessionStore.Setup(item => item.ReadSession("WorkspaceId")).Returns((WorkspaceSessionSnapshot?)null);
        SetupTransactionRequiredResult(expected);

        var result = await _target.StageAsync(
            "OperationName",
            new WorkspaceMutationProposal(),
            [],
            [],
            TestContext.Current.CancellationToken);

        result.Should().BeSameAs(expected);
    }

    [Fact]
    public async Task GIVEN_ProposalWithoutCandidateSolution_WHEN_Staging_THEN_ShouldReturnValidationFailureWithMessages()
    {
        var expected = CreateRejectedResult("InvalidMutationProposal");
        var session = CreateSession(CreateTransaction(CreateSolution()));
        SetupOwner(session);
        var diagnostic = new DiagnosticInfo { Id = "Id", Message = "Message" };
        var warning = new WarningInfo { Code = "Code", Message = "Message" };
        _resultFactory
            .Setup(item => item.Rejected<MutationStagingOutcome>(
                It.Is<WorkspaceOperationError>(error =>
                    error.Code == "InvalidMutationProposal"
                    && error.Message == "Mutation proposals must provide a candidate solution."),
                null,
                It.Is<IReadOnlyList<DiagnosticInfo>>(items => items.SequenceEqual(new[] { diagnostic })),
                It.Is<IReadOnlyList<WarningInfo>>(items => items.SequenceEqual(new[] { warning }))))
            .Returns(expected);

        var result = await _target.StageAsync(
            "OperationName",
            new WorkspaceMutationProposal(),
            [diagnostic],
            [warning],
            TestContext.Current.CancellationToken);

        result.Should().BeSameAs(expected);
        _diffBuilder.Verify(item => item.CreateChangeSummaryAsync(
            It.IsAny<Solution>(),
            It.IsAny<Solution>(),
            It.IsAny<IWorkspaceResolver>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task GIVEN_CandidateFromDifferentWorkspace_WHEN_Staging_THEN_ShouldReturnValidationFailure()
    {
        var currentSolution = CreateSolution();
        using var otherWorkspace = new AdhocWorkspace();
        var expected = CreateRejectedResult("InvalidMutationProposal");
        var session = CreateSession(CreateTransaction(currentSolution));
        SetupOwner(session);
        SetupValidationFailureResult(expected, "InvalidMutationProposal", "Mutation proposals must belong to the current workspace.");

        var result = await _target.StageAsync(
            "OperationName",
            new WorkspaceMutationProposal { CandidateSolution = otherWorkspace.CurrentSolution },
            [],
            [],
            TestContext.Current.CancellationToken);

        result.Should().BeSameAs(expected);
    }

    [Fact]
    public async Task GIVEN_CandidateAddsProject_WHEN_Staging_THEN_ShouldReturnUnsupportedChange()
    {
        var currentSolution = CreateSolution();
        var candidateSolution = currentSolution.AddProject("AddedProject", "AddedProject", LanguageNames.CSharp).Solution;
        var expected = CreateRejectedResult("UnsupportedChange");
        var session = CreateSession(CreateTransaction(currentSolution));
        SetupOwner(session);
        SetupValidationFailureResult(expected, "UnsupportedChange", "Mutation proposals must not add or remove projects.");

        var result = await _target.StageAsync(
            "OperationName",
            new WorkspaceMutationProposal { CandidateSolution = candidateSolution },
            [],
            [],
            TestContext.Current.CancellationToken);

        result.Should().BeSameAs(expected);
    }

    [Theory]
    [InlineData("ProjectIdentity", "UnsupportedChange", "Mutation proposals must not alter project identity.")]
    [InlineData("ProjectFilePath", "UnsupportedChange", "Mutation proposals must not alter project identity or options.")]
    [InlineData("ProjectName", "UnsupportedChange", "Mutation proposals must not alter project identity or options.")]
    [InlineData("AssemblyName", "UnsupportedChange", "Mutation proposals must not alter project identity or options.")]
    [InlineData("DefaultNamespace", "UnsupportedChange", "Mutation proposals must not alter project identity or options.")]
    [InlineData("CompilationOptions", "UnsupportedChange", "Mutation proposals must not alter project identity or options.")]
    [InlineData("ParseOptions", "UnsupportedChange", "Mutation proposals must not alter project identity or options.")]
    [InlineData("DocumentMetadata", "UnsupportedChange", "Mutation proposals must not alter source document metadata.")]
    public async Task GIVEN_UnsupportedCandidateShape_WHEN_Staging_THEN_ShouldReturnExpectedValidationFailure(
        string changeKind,
        string errorCode,
        string errorMessage)
    {
        var currentSolution = CreateSolution();
        var currentProject = currentSolution.Projects.Single();
        var currentDocument = currentProject.Documents.Single();
        var candidateSolution = changeKind switch
        {
            "ProjectIdentity" => currentSolution
                .RemoveProject(currentProject.Id)
                .AddProject("ReplacementProject", "ReplacementProject", LanguageNames.CSharp).Solution,
            "ProjectFilePath" => currentSolution.WithProjectFilePath(currentProject.Id, "DifferentProjectPath"),
            "ProjectName" => currentSolution.WithProjectName(currentProject.Id, "DifferentProjectName"),
            "AssemblyName" => currentSolution.WithProjectAssemblyName(currentProject.Id, "DifferentAssemblyName"),
            "DefaultNamespace" => currentSolution.WithProjectDefaultNamespace(currentProject.Id, "DifferentNamespace"),
            "CompilationOptions" => currentSolution.WithProjectCompilationOptions(
                currentProject.Id,
                new CSharpCompilationOptions(OutputKind.ConsoleApplication)),
            "ParseOptions" => currentSolution.WithProjectParseOptions(
                currentProject.Id,
                new CSharpParseOptions(LanguageVersion.CSharp13)),
            "DocumentMetadata" => currentSolution.WithDocumentName(currentDocument.Id, "DifferentDocumentName.cs"),
            _ => throw new InvalidOperationException("Unsupported test change kind."),
        };
        var expected = CreateRejectedResult(errorCode);
        SetupOwner(CreateSession(CreateTransaction(currentSolution)));
        SetupValidationFailureResult(expected, errorCode, errorMessage);

        var result = await _target.StageAsync(
            "OperationName",
            new WorkspaceMutationProposal { CandidateSolution = candidateSolution },
            [],
            [],
            TestContext.Current.CancellationToken);

        result.Should().BeSameAs(expected);
    }

    [Theory]
    [InlineData("AddedWithoutPath", "Mutation proposals must use regular source documents for created files.")]
    [InlineData("AddedScript", "Mutation proposals must use regular source documents for created files.")]
    [InlineData("ProjectWithoutPath", "Mutation proposals must keep source files within the owning project directory.")]
    [InlineData("AddedOutsideProject", "Mutation proposals must keep source files within the owning project directory.")]
    public async Task GIVEN_InvalidAddedDocument_WHEN_Staging_THEN_ShouldReturnUnsupportedChange(
        string changeKind,
        string errorMessage)
    {
        var currentSolution = CreateSolution(projectHasPath: changeKind != "ProjectWithoutPath");
        var project = currentSolution.Projects.Single();
        var documentInfo = DocumentInfo.Create(
            DocumentId.CreateNewId(project.Id),
            "AddedDocument.cs",
            loader: TextLoader.From(TextAndVersion.Create(SourceText.From("class Added { }"), VersionStamp.Default)),
            sourceCodeKind: changeKind == "AddedScript" ? SourceCodeKind.Script : SourceCodeKind.Regular,
            filePath: changeKind == "AddedWithoutPath"
                ? null
                : changeKind == "AddedOutsideProject"
                    ? Path.Combine(Path.GetTempPath(), "OutsideProject", "AddedDocument.cs")
                    : Path.Combine(Path.GetTempPath(), "Project", "AddedDocument.cs"));
        var candidateSolution = currentSolution.AddDocument(documentInfo);
        var expected = CreateRejectedResult("UnsupportedChange");
        SetupOwner(CreateSession(CreateTransaction(currentSolution)));
        SetupValidationFailureResult(expected, "UnsupportedChange", errorMessage);

        var result = await _target.StageAsync(
            "OperationName",
            new WorkspaceMutationProposal { CandidateSolution = candidateSolution },
            [],
            [],
            TestContext.Current.CancellationToken);

        result.Should().BeSameAs(expected);
    }

    [Theory]
    [InlineData("AddedMetadataReference")]
    [InlineData("RemovedMetadataReference")]
    [InlineData("AddedProjectReference")]
    [InlineData("RemovedProjectReference")]
    [InlineData("AddedAnalyzerReference")]
    [InlineData("RemovedAnalyzerReference")]
    [InlineData("AddedAdditionalDocument")]
    [InlineData("ChangedAdditionalDocument")]
    [InlineData("RemovedAdditionalDocument")]
    public async Task GIVEN_ReferenceOrNonSourceDocumentChange_WHEN_Staging_THEN_ShouldReturnUnsupportedChange(string changeKind)
    {
        var analyzerReference = new Mock<AnalyzerReference>();
        var solutions = CreateReferenceOrNonSourceDocumentChange(changeKind, analyzerReference.Object);
        var expected = CreateRejectedResult("UnsupportedChange");
        SetupOwner(CreateSession(CreateTransaction(solutions.Current)));
        SetupValidationFailureResult(
            expected,
            "UnsupportedChange",
            "Mutation proposals must not alter project references or non-source documents.");

        var result = await _target.StageAsync(
            "OperationName",
            new WorkspaceMutationProposal { CandidateSolution = solutions.Candidate },
            [],
            [],
            TestContext.Current.CancellationToken);

        result.Should().BeSameAs(expected);
    }

    [Theory]
    [InlineData("AddedAnalyzerConfigDocument")]
    [InlineData("ChangedAnalyzerConfigDocument")]
    [InlineData("RemovedAnalyzerConfigDocument")]
    public async Task GIVEN_AnalyzerConfigDocumentChange_WHEN_Staging_THEN_ShouldReturnUnsupportedNonSourceDocumentChange(
        string changeKind)
    {
        var analyzerReference = new Mock<AnalyzerReference>();
        var solutions = CreateReferenceOrNonSourceDocumentChange(changeKind, analyzerReference.Object);
        var expected = CreateRejectedResult("UnsupportedChange");
        SetupOwner(CreateSession(CreateTransaction(solutions.Current)));
        SetupValidationFailureResult(
            expected,
            "UnsupportedChange",
            "Mutation proposals must not alter project references or non-source documents.");

        var result = await _target.StageAsync(
            "OperationName",
            new WorkspaceMutationProposal { CandidateSolution = solutions.Candidate },
            [],
            [],
            TestContext.Current.CancellationToken);

        result.Should().BeSameAs(expected);
    }

    [Theory]
    [InlineData("Removed", "Mutation proposals must use regular source documents for deleted files.")]
    [InlineData("Changed", "Mutation proposals must use regular source documents for changed files.")]
    public async Task GIVEN_PathlessExistingDocument_WHEN_RemovingOrChanging_THEN_ShouldReturnUnsupportedChange(
        string changeKind,
        string errorMessage)
    {
        var currentSolution = CreateSolution(documentHasPath: false);
        var document = currentSolution.Projects.Single().Documents.Single();
        var candidateSolution = changeKind == "Removed"
            ? currentSolution.RemoveDocument(document.Id)
            : currentSolution.WithDocumentText(document.Id, SourceText.From("class Updated { }"));
        var expected = CreateRejectedResult("UnsupportedChange");
        SetupOwner(CreateSession(CreateTransaction(currentSolution)));
        SetupValidationFailureResult(expected, "UnsupportedChange", errorMessage);

        var result = await _target.StageAsync(
            "OperationName",
            new WorkspaceMutationProposal { CandidateSolution = candidateSolution },
            [],
            [],
            TestContext.Current.CancellationToken);

        result.Should().BeSameAs(expected);
    }

    [Fact]
    public async Task GIVEN_ValidCandidate_WHEN_Staging_THEN_ShouldReplaceSessionAndReturnMappedSuccess()
    {
        var currentSolution = CreateSolution();
        var document = currentSolution.Projects.Single().Documents.Single();
        var candidateSolution = document.WithText(SourceText.From("class Updated { }")).Project.Solution;
        var transaction = CreateTransaction(currentSolution) with
        {
            Revisions = [new WorkspaceTransactionRevision { Solution = currentSolution }],
            CurrentRevision = 0,
            MaxRevisions = 3,
        };
        var session = CreateSession(transaction);
        SetupOwner(session);
        var changes = new ChangeSummary();
        var handlerWarning = new WarningInfo { Code = "HandlerWarning", Message = "Message" };
        var proposalWarning = new WarningInfo { Code = "ProposalWarning", Message = "Message" };
        var expected = new WorkspaceOperationResult<MutationStagingOutcome>
        {
            Status = WorkspaceOperationStatus.Succeeded,
        };
        _diffBuilder
            .Setup(item => item.CreateChangeSummaryAsync(
                currentSolution,
                candidateSolution,
                It.IsAny<IWorkspaceResolver>(),
                TestContext.Current.CancellationToken))
            .ReturnsAsync(changes);
        _resultFactory
            .Setup(item => item.Succeeded(
                It.Is<MutationStagingOutcome>(outcome =>
                    outcome.Operation == "OperationName"
                    && outcome.Summary == "Summary"
                    && outcome.Changes == changes
                    && outcome.Transaction.Revision == 1),
                null,
                It.IsAny<IReadOnlyList<DiagnosticInfo>>(),
                It.Is<IReadOnlyList<WarningInfo>>(items => items.SequenceEqual(new[] { handlerWarning, proposalWarning }))))
            .Returns(expected);

        var result = await _target.StageAsync(
            "OperationName",
            new WorkspaceMutationProposal
            {
                CandidateSolution = candidateSolution,
                Summary = "Summary",
                Warnings = [proposalWarning],
            },
            [],
            [handlerWarning],
            TestContext.Current.CancellationToken);

        result.Should().BeSameAs(expected);
        _sessionStore.Verify(item => item.ReplaceSession(It.Is<WorkspaceSessionSnapshot>(replacement =>
            replacement.CurrentSolution == candidateSolution
            && replacement.Transaction!.CurrentRevision == 1
            && replacement.Transaction.Revisions.Count == 1)), Times.Once);
    }

    public void Dispose()
    {
        _workspace.Dispose();
    }

    private void SetupOwner(WorkspaceSessionSnapshot session)
    {
        _sessionStore.Setup(item => item.ReadSnapshot()).Returns(new WorkspaceHostSnapshot
        {
            TransactionOwnerWorkspaceId = "WorkspaceId",
        });
        _sessionStore.Setup(item => item.ReadSession("WorkspaceId")).Returns(session);
    }

    private void SetupTransactionRequiredResult(WorkspaceOperationResult<MutationStagingOutcome> result)
    {
        _resultFactory
            .Setup(item => item.Rejected<MutationStagingOutcome>(
                WorkspaceErrorCodes.TransactionRequired,
                "Start a transaction before invoking mutation tools.",
                RequiredAction.StartTransaction,
                null,
                null,
                null))
            .Returns(result);
    }

    private void SetupValidationFailureResult(
        WorkspaceOperationResult<MutationStagingOutcome> result,
        string code,
        string message)
    {
        _resultFactory
            .Setup(item => item.Rejected<MutationStagingOutcome>(
                It.Is<WorkspaceOperationError>(error => error.Code == code && error.Message == message),
                null,
                It.IsAny<IReadOnlyList<DiagnosticInfo>>(),
                It.IsAny<IReadOnlyList<WarningInfo>>()))
            .Returns(result);
    }

    private Solution CreateSolution(bool documentHasPath = true, bool projectHasPath = true)
    {
        var project = _workspace.AddProject(ProjectInfo.Create(
            ProjectId.CreateNewId(),
            VersionStamp.Default,
            "Project",
            "Project",
            LanguageNames.CSharp,
            filePath: projectHasPath ? Path.Combine(Path.GetTempPath(), "Project", "Project.csproj") : null));
        _workspace.AddDocument(DocumentInfo.Create(
            DocumentId.CreateNewId(project.Id),
            "Document.cs",
            loader: TextLoader.From(TextAndVersion.Create(SourceText.From("class C { }"), VersionStamp.Default)),
            filePath: documentHasPath
                ? Path.Combine(Path.GetTempPath(), "Project", "Document.cs")
                : null));
        return _workspace.CurrentSolution;
    }

    private (Solution Current, Solution Candidate) CreateReferenceOrNonSourceDocumentChange(
        string changeKind,
        AnalyzerReference analyzerReference)
    {
        var current = CreateSolution();
        var project = current.Projects.Single();
        var metadataReference = MetadataReference.CreateFromFile(typeof(object).Assembly.Location);
        var referencedProjectId = ProjectId.CreateNewId();
        var projectReference = new ProjectReference(referencedProjectId);
        var additionalDocumentId = DocumentId.CreateNewId(project.Id);
        var additionalDocumentInfo = CreateTextDocumentInfo(additionalDocumentId, "Additional.txt");
        var analyzerConfigDocumentId = DocumentId.CreateNewId(project.Id);
        var analyzerConfigDocumentInfo = CreateTextDocumentInfo(analyzerConfigDocumentId, ".editorconfig");

        return changeKind switch
        {
            "AddedMetadataReference" => (current, current.AddMetadataReference(project.Id, metadataReference)),
            "RemovedMetadataReference" => RemoveMetadataReference(current, project.Id, metadataReference),
            "AddedProjectReference" => AddProjectReference(current, project.Id, referencedProjectId, projectReference),
            "RemovedProjectReference" => RemoveProjectReference(current, project.Id, referencedProjectId, projectReference),
            "AddedAnalyzerReference" => (current, current.AddAnalyzerReference(project.Id, analyzerReference)),
            "RemovedAnalyzerReference" => RemoveAnalyzerReference(current, project.Id, analyzerReference),
            "AddedAdditionalDocument" => (current, current.AddAdditionalDocument(additionalDocumentInfo)),
            "ChangedAdditionalDocument" => ChangeAdditionalDocument(current, additionalDocumentInfo),
            "RemovedAdditionalDocument" => RemoveAdditionalDocument(current, additionalDocumentInfo),
            "AddedAnalyzerConfigDocument" => (current, AddAnalyzerConfigDocument(current, analyzerConfigDocumentInfo)),
            "ChangedAnalyzerConfigDocument" => ChangeAnalyzerConfigDocument(current, analyzerConfigDocumentInfo),
            "RemovedAnalyzerConfigDocument" => RemoveAnalyzerConfigDocument(current, analyzerConfigDocumentInfo),
            _ => throw new InvalidOperationException("Unsupported reference test change kind."),
        };
    }

    private static (Solution Current, Solution Candidate) RemoveMetadataReference(
        Solution solution,
        ProjectId projectId,
        MetadataReference reference)
    {
        var current = solution.AddMetadataReference(projectId, reference);
        return (current, current.RemoveMetadataReference(projectId, reference));
    }

    private static (Solution Current, Solution Candidate) AddProjectReference(
        Solution solution,
        ProjectId projectId,
        ProjectId referencedProjectId,
        ProjectReference reference)
    {
        var current = solution.AddProject(ProjectInfo.Create(
            referencedProjectId,
            VersionStamp.Default,
            "ReferencedProject",
            "ReferencedProject",
            LanguageNames.CSharp));
        return (current, current.AddProjectReference(projectId, reference));
    }

    private static (Solution Current, Solution Candidate) RemoveProjectReference(
        Solution solution,
        ProjectId projectId,
        ProjectId referencedProjectId,
        ProjectReference reference)
    {
        var current = solution
            .AddProject(ProjectInfo.Create(
                referencedProjectId,
                VersionStamp.Default,
                "ReferencedProject",
                "ReferencedProject",
                LanguageNames.CSharp))
            .AddProjectReference(projectId, reference);
        return (current, current.RemoveProjectReference(projectId, reference));
    }

    private static (Solution Current, Solution Candidate) RemoveAnalyzerReference(
        Solution solution,
        ProjectId projectId,
        AnalyzerReference reference)
    {
        var current = solution.AddAnalyzerReference(projectId, reference);
        return (current, current.RemoveAnalyzerReference(projectId, reference));
    }

    private static (Solution Current, Solution Candidate) ChangeAdditionalDocument(Solution solution, DocumentInfo documentInfo)
    {
        var current = solution.AddAdditionalDocument(documentInfo);
        return (current, current.WithAdditionalDocumentText(documentInfo.Id, SourceText.From("Changed")));
    }

    private static (Solution Current, Solution Candidate) RemoveAdditionalDocument(Solution solution, DocumentInfo documentInfo)
    {
        var current = solution.AddAdditionalDocument(documentInfo);
        return (current, current.RemoveAdditionalDocument(documentInfo.Id));
    }

    private static (Solution Current, Solution Candidate) ChangeAnalyzerConfigDocument(Solution solution, DocumentInfo documentInfo)
    {
        var current = AddAnalyzerConfigDocument(solution, documentInfo);
        return (current, current.WithAnalyzerConfigDocumentText(documentInfo.Id, SourceText.From("[*.cs]\nindent_style = space")));
    }

    private static (Solution Current, Solution Candidate) RemoveAnalyzerConfigDocument(Solution solution, DocumentInfo documentInfo)
    {
        var current = AddAnalyzerConfigDocument(solution, documentInfo);
        return (current, current.RemoveAnalyzerConfigDocument(documentInfo.Id));
    }

    private static Solution AddAnalyzerConfigDocument(Solution solution, DocumentInfo documentInfo)
    {
        return solution.AddAnalyzerConfigDocument(
            documentInfo.Id,
            documentInfo.Name,
            SourceText.From("[*.cs]\nindent_style = tab"),
            filePath: Path.Combine(Path.GetTempPath(), "Project", documentInfo.Name));
    }

    private static DocumentInfo CreateTextDocumentInfo(DocumentId documentId, string name)
    {
        return DocumentInfo.Create(
            documentId,
            name,
            loader: TextLoader.From(TextAndVersion.Create(SourceText.From("Text"), VersionStamp.Default)));
    }

    private WorkspaceSessionSnapshot CreateSession(WorkspaceTransaction? transaction)
    {
        return new WorkspaceSessionSnapshot
        {
            State = WorkspaceLifecycleState.TransactionActive,
            Workspace = new WorkspaceIdentity
            {
                WorkspaceId = "WorkspaceId",
                LoadedPath = "LoadedPath",
            },
            LoadedWorkspace = null!,
            CurrentSolution = transaction?.CurrentSolution ?? _workspace.CurrentSolution,
            Transaction = transaction,
            InputManifest = null!,
            OperationGate = new Mock<IWorkspaceOperationGate>().Object,
        };
    }

    private static WorkspaceTransaction CreateTransaction(Solution solution)
    {
        return new WorkspaceTransaction
        {
            BaselineSolution = solution,
            CurrentRevision = 0,
            MaxRevisions = 3,
        };
    }

    private static WorkspaceOperationResult<MutationStagingOutcome> CreateRejectedResult(string code)
    {
        return new WorkspaceOperationResult<MutationStagingOutcome>
        {
            Status = WorkspaceOperationStatus.Rejected,
            Error = new WorkspaceOperationError
            {
                Code = code,
                Message = "Message",
            },
        };
    }
}
