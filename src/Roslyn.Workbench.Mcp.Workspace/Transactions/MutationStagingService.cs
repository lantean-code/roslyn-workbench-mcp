using Roslyn.Workbench.Mcp.Workspace.Contracts.Results;
namespace Roslyn.Workbench.Mcp.Workspace.Transactions;

internal sealed class MutationStagingService : IMutationStagingService
{
    private readonly IWorkspaceOperationResultFactory _resultFactory;
    private readonly IWorkspaceSessionStore _sessionStore;
    private readonly IWorkspaceDiffBuilder _diffBuilder;
    private readonly IWorkspaceResolverFactory _resolverFactory;
    private readonly IWorkspaceInstanceStatusPublisher _instanceStatusPublisher;

    public MutationStagingService(
        IWorkspaceOperationResultFactory resultFactory,
        IWorkspaceSessionStore sessionStore,
        IWorkspaceDiffBuilder diffBuilder,
        IWorkspaceResolverFactory resolverFactory,
        IWorkspaceInstanceStatusPublisher instanceStatusPublisher)
    {
        _resultFactory = resultFactory;
        _sessionStore = sessionStore;
        _diffBuilder = diffBuilder;
        _resolverFactory = resolverFactory;
        _instanceStatusPublisher = instanceStatusPublisher;
    }

    public async ValueTask<WorkspaceOperationResult<MutationStagingOutcome>> StageAsync(
        string operationName,
        WorkspaceMutationProposal proposal,
        IReadOnlyList<DiagnosticInfo> diagnostics,
        IReadOnlyList<WarningInfo> warnings,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var hostSnapshot = _sessionStore.ReadSnapshot();
        if (string.IsNullOrWhiteSpace(hostSnapshot.TransactionOwnerWorkspaceId))
        {
            return _resultFactory.Rejected<MutationStagingOutcome>(
                WorkspaceErrorCodes.TransactionRequired,
                "Start a transaction before invoking mutation tools.",
                RequiredAction.StartTransaction);
        }

        var session = _sessionStore.ReadSession(hostSnapshot.TransactionOwnerWorkspaceId!);
        if (session?.Transaction is null)
        {
            return _resultFactory.Rejected<MutationStagingOutcome>(
                WorkspaceErrorCodes.TransactionRequired,
                "Start a transaction before invoking mutation tools.",
                RequiredAction.StartTransaction);
        }

        var validationError = ValidateMutationProposal(session.CurrentSolution, proposal);
        if (validationError is not null)
        {
            return _resultFactory.Rejected<MutationStagingOutcome>(
                validationError,
                diagnostics: diagnostics,
                warnings: warnings);
        }

        var transaction = session.Transaction;
        var stagedChanges = await _diffBuilder.CreateChangeSummaryAsync(
            transaction.BaselineSolution,
            proposal.CandidateSolution!,
            _resolverFactory.Create(proposal.CandidateSolution!, session.Workspace, transaction.CurrentRevision + 1),
            cancellationToken);
        var retainedRevisions = transaction.Revisions.Take(transaction.CurrentRevision).ToArray();
        var revision = new WorkspaceTransactionRevision
        {
            Solution = proposal.CandidateSolution!,
            Changes = stagedChanges,
            Operation = operationName,
            Summary = proposal.Summary,
            Preview = new MutationPreview
            {
                Summary = proposal.Summary,
            },
        };
        var updatedRevisions = retainedRevisions.Concat([revision]).ToArray();
        var updatedTransaction = transaction with
        {
            Revisions = updatedRevisions,
            CurrentRevision = updatedRevisions.Length,
        };
        var updatedSession = session with
        {
            Transaction = updatedTransaction,
            CurrentSolution = updatedTransaction.CurrentSolution,
        };

        _sessionStore.ReplaceSession(updatedSession);
        await _instanceStatusPublisher.UpdateAsync(
            updatedSession.Workspace.WorkspaceId,
            updatedSession.State,
            updatedSession.Transaction?.CurrentRevision,
            null,
            null).ConfigureAwait(false);

        return _resultFactory.Succeeded(
            new MutationStagingOutcome
            {
                Operation = operationName,
                Summary = proposal.Summary,
                Transaction = updatedTransaction.ToInfo(conflicted: false),
                Preview = revision.Preview,
                Changes = stagedChanges,
            },
            diagnostics: diagnostics,
            warnings: warnings.Concat(proposal.Warnings).ToArray());
    }

    private static WorkspaceOperationError? ValidateMutationProposal(Solution currentSolution, WorkspaceMutationProposal proposal)
    {
        if (proposal.CandidateSolution is null)
        {
            return CreateError("InvalidMutationProposal", "Mutation proposals must provide a candidate solution.");
        }

        if (!ReferenceEquals(proposal.CandidateSolution.Workspace, currentSolution.Workspace))
        {
            return CreateError("InvalidMutationProposal", "Mutation proposals must belong to the current workspace.");
        }

        if (proposal.CandidateSolution.ProjectIds.Count != currentSolution.ProjectIds.Count)
        {
            return CreateError("UnsupportedChange", "Mutation proposals must not add or remove projects.");
        }

        foreach (var currentProject in currentSolution.Projects)
        {
            var candidateProject = proposal.CandidateSolution.GetProject(currentProject.Id);
            if (candidateProject is null)
            {
                return CreateError("UnsupportedChange", "Mutation proposals must not alter project identity.");
            }

            if (!string.Equals(candidateProject.FilePath, currentProject.FilePath, StringComparison.Ordinal)
                || !string.Equals(candidateProject.Name, currentProject.Name, StringComparison.Ordinal)
                || !string.Equals(candidateProject.AssemblyName, currentProject.AssemblyName, StringComparison.Ordinal)
                || !string.Equals(candidateProject.DefaultNamespace, currentProject.DefaultNamespace, StringComparison.Ordinal))
            {
                return CreateError("UnsupportedChange", "Mutation proposals must not alter project identity or options.");
            }

            var projectChanges = candidateProject.GetChanges(currentProject);
            if (projectChanges.GetAddedMetadataReferences().Any()
                || projectChanges.GetRemovedMetadataReferences().Any()
                || projectChanges.GetAddedProjectReferences().Any()
                || projectChanges.GetRemovedProjectReferences().Any()
                || projectChanges.GetAddedAnalyzerReferences().Any()
                || projectChanges.GetRemovedAnalyzerReferences().Any()
                || projectChanges.GetAddedAdditionalDocuments().Any()
                || projectChanges.GetChangedAdditionalDocuments().Any()
                || projectChanges.GetRemovedAdditionalDocuments().Any()
                || projectChanges.GetAddedAnalyzerConfigDocuments().Any()
                || projectChanges.GetChangedAnalyzerConfigDocuments().Any()
                || projectChanges.GetRemovedAnalyzerConfigDocuments().Any())
            {
                return CreateError("UnsupportedChange", "Mutation proposals must not alter project references or non-source documents.");
            }

            if (!Equals(candidateProject.CompilationOptions, currentProject.CompilationOptions)
                || !Equals(candidateProject.ParseOptions, currentProject.ParseOptions))
            {
                return CreateError("UnsupportedChange", "Mutation proposals must not alter project identity or options.");
            }

            var textChangedDocuments = projectChanges.GetChangedDocuments(onlyGetDocumentsWithTextChanges: true).ToHashSet();
            if (projectChanges.GetChangedDocuments().Except(textChangedDocuments).Any())
            {
                return CreateError("UnsupportedChange", "Mutation proposals must not alter source document metadata.");
            }

            if (TryValidateSourceDocuments(currentProject, projectChanges.GetRemovedDocuments(), "deleted") is { } removedValidationError)
            {
                return removedValidationError;
            }

            if (TryValidateSourceDocuments(candidateProject, projectChanges.GetAddedDocuments(), "created") is { } addedValidationError)
            {
                return addedValidationError;
            }

            if (TryValidateSourceDocuments(candidateProject, textChangedDocuments, "changed") is { } changedValidationError)
            {
                return changedValidationError;
            }
        }

        return null;
    }

    private static WorkspaceOperationError? TryValidateSourceDocuments(
        Project project,
        IEnumerable<DocumentId> documentIds,
        string operation)
    {
        foreach (var documentId in documentIds)
        {
            var document = project.GetDocument(documentId);
            if (document is null
                || document.SourceCodeKind != SourceCodeKind.Regular
                || string.IsNullOrWhiteSpace(document.FilePath))
            {
                return CreateError("UnsupportedChange", $"Mutation proposals must use regular source documents for {operation} files.");
            }

            var projectDirectory = Path.GetDirectoryName(project.FilePath ?? string.Empty);
            if (string.IsNullOrWhiteSpace(projectDirectory)
                || !IsPathWithinDirectory(document.FilePath, projectDirectory))
            {
                return CreateError("UnsupportedChange", "Mutation proposals must keep source files within the owning project directory.");
            }
        }

        return null;
    }

    private static bool IsPathWithinDirectory(string candidatePath, string directoryPath)
    {
        var normalizedDirectory = Path.TrimEndingDirectorySeparator(Path.GetFullPath(directoryPath));
        var normalizedCandidate = Path.GetFullPath(candidatePath);
        var directoryPrefix = normalizedDirectory + Path.DirectorySeparatorChar;
        var altDirectoryPrefix = normalizedDirectory + Path.AltDirectorySeparatorChar;

        return normalizedCandidate.StartsWith(directoryPrefix, StringComparison.Ordinal)
            || normalizedCandidate.StartsWith(altDirectoryPrefix, StringComparison.Ordinal);
    }

    private static WorkspaceOperationError CreateError(string code, string message)
    {
        return new WorkspaceOperationError
        {
            Code = code,
            Message = message,
        };
    }
}
