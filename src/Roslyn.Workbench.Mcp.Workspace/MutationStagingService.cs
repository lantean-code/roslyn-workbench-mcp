using Roslyn.Workbench.Mcp.Contracts.Results;
using Roslyn.Workbench.Mcp.Plugins;

namespace Roslyn.Workbench.Mcp.Workspace;

internal sealed class MutationStagingService : IMutationStagingService
{
    private readonly IWorkspaceSessionStore _sessionStore;

    public MutationStagingService(IWorkspaceSessionStore sessionStore)
    {
        _sessionStore = sessionStore;
    }

    public async ValueTask<PluginExecutionResult<MutationData>> StageAsync(
        RegisteredTool tool,
        MutationProposal proposal,
        IReadOnlyList<DiagnosticInfo> diagnostics,
        IReadOnlyList<WarningInfo> warnings,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var hostSnapshot = _sessionStore.ReadSnapshot();
        if (string.IsNullOrWhiteSpace(hostSnapshot.TransactionOwnerWorkspaceId))
        {
            return PluginExecutionResult<MutationData>.Rejected(
                CreateToolError(WorkspaceErrorCodes.TransactionRequired, "Start a transaction before invoking mutation tools."),
                RequiredAction.StartTransaction);
        }

        var session = _sessionStore.ReadSession(hostSnapshot.TransactionOwnerWorkspaceId!);
        if (session?.Transaction is null || session.CurrentSolution is null)
        {
            return PluginExecutionResult<MutationData>.Rejected(
                CreateToolError(WorkspaceErrorCodes.TransactionRequired, "Start a transaction before invoking mutation tools."),
                RequiredAction.StartTransaction);
        }

        var validationError = ValidateMutationProposal(session.CurrentSolution, proposal);
        if (validationError is not null)
        {
            return PluginExecutionResult<MutationData>.Rejected(validationError.Value.Error, validationError.Value.RequiredAction, diagnostics, warnings);
        }

        var transaction = session.Transaction;
        var stagedChanges = await WorkspaceDiffBuilder.CreateChangeSummaryAsync(
            transaction.BaselineSolution,
            proposal.CandidateSolution!,
            new WorkspaceResolver(proposal.CandidateSolution!, session.Workspace, transaction.CurrentRevision + 1),
            cancellationToken);
        var retainedRevisions = transaction.Revisions.Take(transaction.CurrentRevision).ToArray();
        var revision = new WorkspaceTransactionRevision
        {
            Solution = proposal.CandidateSolution!,
            Changes = stagedChanges,
            Operation = tool.Metadata.Name,
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

        return PluginExecutionResult<MutationData>.Success(
            new MutationData
            {
                Operation = tool.Metadata.Name,
                Summary = proposal.Summary,
                Transaction = updatedTransaction.ToInfo(conflicted: false),
                Preview = revision.Preview,
            },
            stagedChanges,
            diagnostics,
            warnings.Concat(proposal.Warnings).ToArray());
    }

    private static (ToolError Error, RequiredAction? RequiredAction)? ValidateMutationProposal(Solution currentSolution, MutationProposal proposal)
    {
        if (proposal.CandidateSolution is null)
        {
            return (CreateToolError("InvalidMutationProposal", "Mutation proposals must provide a candidate solution."), null);
        }

        if (!ReferenceEquals(proposal.CandidateSolution.Workspace, currentSolution.Workspace))
        {
            return (CreateToolError("InvalidMutationProposal", "Mutation proposals must belong to the current workspace."), null);
        }

        if (!string.Equals(proposal.CandidateSolution.FilePath, currentSolution.FilePath, StringComparison.Ordinal))
        {
            return (CreateToolError("InvalidMutationProposal", "Mutation proposals must target the current workspace solution."), null);
        }

        if (proposal.CandidateSolution.ProjectIds.Count != currentSolution.ProjectIds.Count)
        {
            return (CreateToolError("UnsupportedChange", "Mutation proposals must not add or remove projects."), null);
        }

        foreach (var projectId in currentSolution.ProjectIds)
        {
            var currentProject = currentSolution.GetProject(projectId);
            var candidateProject = proposal.CandidateSolution.GetProject(projectId);
            if (currentProject is null || candidateProject is null)
            {
                return (CreateToolError("UnsupportedChange", "Mutation proposals must not alter project identity."), null);
            }

            if (!string.Equals(candidateProject.FilePath, currentProject.FilePath, StringComparison.Ordinal)
                || !string.Equals(candidateProject.Name, currentProject.Name, StringComparison.Ordinal)
                || !string.Equals(candidateProject.AssemblyName, currentProject.AssemblyName, StringComparison.Ordinal)
                || !string.Equals(candidateProject.DefaultNamespace, currentProject.DefaultNamespace, StringComparison.Ordinal)
                || !Equals(candidateProject.CompilationOptions, currentProject.CompilationOptions)
                || !Equals(candidateProject.ParseOptions, currentProject.ParseOptions))
            {
                return (CreateToolError("UnsupportedChange", "Mutation proposals must not alter project identity or options."), null);
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
                return (CreateToolError("UnsupportedChange", "Mutation proposals must not alter project references or non-source documents."), null);
            }

            var textChangedDocuments = projectChanges.GetChangedDocuments(onlyGetDocumentsWithTextChanges: true).ToHashSet();
            if (projectChanges.GetChangedDocuments().Except(textChangedDocuments).Any())
            {
                return (CreateToolError("UnsupportedChange", "Mutation proposals must not alter source document metadata."), null);
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

    private static (ToolError Error, RequiredAction? RequiredAction)? TryValidateSourceDocuments(
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
                return (CreateToolError("UnsupportedChange", $"Mutation proposals must use regular source documents for {operation} files."), null);
            }

            var projectDirectory = Path.GetDirectoryName(project.FilePath ?? string.Empty);
            if (string.IsNullOrWhiteSpace(projectDirectory)
                || !IsPathWithinDirectory(document.FilePath, projectDirectory))
            {
                return (CreateToolError("UnsupportedChange", "Mutation proposals must keep source files within the owning project directory."), null);
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

    private static ToolError CreateToolError(string code, string message)
    {
        return new ToolError
        {
            Code = code,
            Message = message,
        };
    }
}
