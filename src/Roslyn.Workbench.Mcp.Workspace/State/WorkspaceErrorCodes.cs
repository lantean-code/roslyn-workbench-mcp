namespace Roslyn.Workbench.Mcp.Workspace.State;

/// <summary>
/// Defines stable error codes for Workspace lifecycle, selection, snapshot and transaction failures.
/// </summary>
internal static class WorkspaceErrorCodes
{
    /// <summary>
    /// The commit could not be completed after persistence began.
    /// </summary>
    public const string CommitFailed = "CommitFailed";

    /// <summary>
    /// The commit could not prepare its persistence plan.
    /// </summary>
    public const string CommitPreparationFailed = "CommitPreparationFailed";

    /// <summary>
    /// Indicates that source mutation is disabled by Host policy.
    /// </summary>
    public const string SourceMutationDisabled = "SourceMutationDisabled";

    /// <summary>
    /// The selected Workspace cannot currently grant the requested operation lease.
    /// </summary>
    public const string WorkspaceBusy = "WorkspaceBusy";

    /// <summary>
    /// The selected Workspace is not loaded.
    /// </summary>
    public const string WorkspaceNotOpen = "WorkspaceNotOpen";

    /// <summary>
    /// The requested path or alias already identifies a loaded Workspace.
    /// </summary>
    public const string WorkspaceAlreadyOpen = "WorkspaceAlreadyOpen";

    /// <summary>
    /// The requested Workspace kind or configuration is unsupported.
    /// </summary>
    public const string WorkspaceNotSupported = "WorkspaceNotSupported";

    /// <summary>
    /// A monitored input changed and the loaded Workspace requires reload.
    /// </summary>
    public const string WorkspaceOutOfDate = "WorkspaceOutOfDate";

    /// <summary>
    /// The Workspace could not be loaded.
    /// </summary>
    public const string WorkspaceLoadFailed = "WorkspaceLoadFailed";

    /// <summary>
    /// The Host has reached its configured loaded-Workspace capacity.
    /// </summary>
    public const string WorkspaceCapacityReached = "WorkspaceCapacityReached";

    /// <summary>
    /// A loaded Workspace's path or effective root no longer complies with Host authority.
    /// </summary>
    public const string WorkspaceAuthorityChanged = "WorkspaceAuthorityChanged";

    /// <summary>
    /// The request violates a Workspace operation contract.
    /// </summary>
    public const string InvalidRequest = "InvalidRequest";

    /// <summary>
    /// No addressable document matches the selector.
    /// </summary>
    public const string DocumentNotFound = "DocumentNotFound";

    /// <summary>
    /// More than one addressable document matches the selector.
    /// </summary>
    public const string DocumentAmbiguous = "DocumentAmbiguous";

    /// <summary>
    /// No project matches the selector.
    /// </summary>
    public const string ProjectNotFound = "ProjectNotFound";

    /// <summary>
    /// More than one project matches the selector.
    /// </summary>
    public const string ProjectAmbiguous = "ProjectAmbiguous";

    /// <summary>
    /// The project selector is invalid.
    /// </summary>
    public const string ProjectSelectorInvalid = "ProjectSelectorInvalid";

    /// <summary>
    /// No symbol matches the selector.
    /// </summary>
    public const string SymbolNotFound = "SymbolNotFound";

    /// <summary>
    /// More than one symbol matches the selector.
    /// </summary>
    public const string SymbolAmbiguous = "SymbolAmbiguous";

    /// <summary>
    /// The symbol selector is invalid.
    /// </summary>
    public const string SymbolSelectorInvalid = "SymbolSelectorInvalid";

    /// <summary>
    /// No source location matches the selector.
    /// </summary>
    public const string LocationNotFound = "LocationNotFound";

    /// <summary>
    /// More than one source location matches the selector.
    /// </summary>
    public const string LocationAmbiguous = "LocationAmbiguous";

    /// <summary>
    /// The source-location selector is invalid.
    /// </summary>
    public const string LocationSelectorInvalid = "LocationSelectorInvalid";

    /// <summary>
    /// The document selector is invalid.
    /// </summary>
    public const string DocumentSelectorInvalid = "DocumentSelectorInvalid";

    /// <summary>
    /// The operation requires an active transaction.
    /// </summary>
    public const string TransactionRequired = "NoActiveTransaction";

    /// <summary>
    /// The selected Workspace already has an active transaction.
    /// </summary>
    public const string TransactionAlreadyActive = "TransactionAlreadyActive";

    /// <summary>
    /// The active transaction is conflicted and cannot accept ordinary operations.
    /// </summary>
    public const string TransactionConflicted = "TransactionConflicted";

    /// <summary>
    /// Another Workspace owns the process-wide transaction slot.
    /// </summary>
    public const string TransactionOwner = "TransactionOwnedByWorkspace";

    /// <summary>
    /// The requested transaction revision is no longer retained.
    /// </summary>
    public const string TransactionHistoryUnavailable = "TransactionHistoryUnavailable";

    /// <summary>
    /// The transaction has reached its configured revision capacity.
    /// </summary>
    public const string TransactionCapacity = "RevisionCapacityReached";

    /// <summary>
    /// The commit-recovery journal has reached its configured capacity.
    /// </summary>
    public const string CommitRecoveryCapacity = "CommitRecoveryCapacityReached";

    /// <summary>
    /// Linked documents produced incompatible candidate changes.
    /// </summary>
    public const string LinkedDocumentConflict = "LinkedDocumentConflict";

    /// <summary>
    /// A mutation candidate changed after it was proposed.
    /// </summary>
    public const string MutationCandidateChanged = "MutationCandidateChanged";

    /// <summary>
    /// A mutation targets generated-looking checked-in source denied by Host policy.
    /// </summary>
    public const string GeneratedSourceMutationDenied = "GeneratedSourceMutationDenied";

    /// <summary>
    /// Indicates that transaction receipt review is not enabled by Host policy.
    /// </summary>
    public const string TransactionReviewUnavailable = "TransactionReviewUnavailable";

    /// <summary>
    /// Indicates that commit did not receive a current matching receipt authorisation.
    /// </summary>
    public const string TransactionReceiptRequired = "TransactionReceiptRequired";

    /// <summary>
    /// Indicates that receipt authorisation does not identify the current persistence set.
    /// </summary>
    public const string TransactionReceiptMismatch = "TransactionReceiptMismatch";

    /// <summary>
    /// Indicates that compiler-impact validation is not enabled by Host policy.
    /// </summary>
    public const string CompilerValidationUnavailable = "CompilerValidationUnavailable";

    /// <summary>
    /// Indicates that one or more compiler evaluations were incomplete.
    /// </summary>
    public const string CompilerValidationIncomplete = "CompilerValidationIncomplete";

    /// <summary>
    /// Indicates that the staged transaction introduced compiler errors.
    /// </summary>
    public const string NewCompilerErrors = "NewCompilerErrors";

    /// <summary>
    /// The supplied snapshot precondition does not match the current Workspace snapshot.
    /// </summary>
    public const string SnapshotMismatch = "SnapshotMismatch";

    /// <summary>
    /// The supplied Workspace MSBuild properties are invalid.
    /// </summary>
    public const string WorkspaceMsBuildPropertiesInvalid = "WorkspaceMsBuildPropertiesInvalid";

    /// <summary>
    /// The requested Workspace path is invalid.
    /// </summary>
    public const string WorkspacePathInvalid = "WorkspacePathInvalid";

    /// <summary>
    /// The requested Workspace path is outside Host authority.
    /// </summary>
    public const string WorkspacePathNotAllowed = "WorkspacePathNotAllowed";

    /// <summary>
    /// The requested Workspace root is invalid.
    /// </summary>
    public const string WorkspaceRootInvalid = "WorkspaceRootInvalid";

    /// <summary>
    /// The requested Workspace root would widen Host authority.
    /// </summary>
    public const string WorkspaceRootNotAllowed = "WorkspaceRootNotAllowed";

    /// <summary>
    /// The supplied Workspace selector is invalid.
    /// </summary>
    public const string WorkspaceSelectorInvalid = "WorkspaceSelectorInvalid";

    /// <summary>
    /// The supplied Workspace selector identifies conflicting Workspaces.
    /// </summary>
    public const string WorkspaceSelectorMismatch = "WorkspaceSelectorMismatch";

    /// <summary>
    /// The supplied Workspace selector does not identify a loaded Workspace.
    /// </summary>
    public const string WorkspaceSelectorNotFound = "WorkspaceSelectorNotFound";

    /// <summary>
    /// A Workspace selector is required because more than one Workspace is loaded.
    /// </summary>
    public const string WorkspaceSelectorRequired = "WorkspaceSelectorRequired";

    /// <summary>
    /// Recovery must be completed before another Workspace can be opened.
    /// </summary>
    public const string RecoveryPending = "RecoveryPending";

    /// <summary>
    /// A transaction must be completed before the Workspace can be closed.
    /// </summary>
    public const string TransactionOpen = "TransactionOpen";

    /// <summary>
    /// Transaction ownership changed while an operation was in progress.
    /// </summary>
    public const string TransactionOwnershipChanged = "TransactionOwnershipChanged";

    /// <summary>
    /// Reload is blocked by the current Workspace state.
    /// </summary>
    public const string WorkspaceReloadBlocked = "WorkspaceReloadBlocked";

    /// <summary>
    /// The Workspace does not currently require a reload.
    /// </summary>
    public const string WorkspaceReloadNotRequired = "WorkspaceReloadNotRequired";

    /// <summary>
    /// The mutation proposal is invalid for the current Workspace.
    /// </summary>
    public const string InvalidMutationProposal = "InvalidMutationProposal";

    /// <summary>
    /// The requested source change is not supported by the transaction pipeline.
    /// </summary>
    public const string UnsupportedChange = "UnsupportedChange";

    /// <summary>
    /// The commit lock could not be acquired or inspected safely.
    /// </summary>
    public const string CommitLockFailed = "CommitLockFailed";

    /// <summary>
    /// A loaded project is outside the effective Workspace root.
    /// </summary>
    public const string WorkspaceProjectOutsideRoot = "WorkspaceProjectOutsideRoot";

    /// <summary>
    /// Workspace inputs could not be evaluated safely.
    /// </summary>
    public const string WorkspaceInputEvaluationFailed = "WorkspaceInputEvaluationFailed";

    /// <summary>
    /// Workspace inputs changed while the Workspace was loading.
    /// </summary>
    public const string WorkspaceChangedDuringLoad = "WorkspaceChangedDuringLoad";

    /// <summary>
    /// The Workspace contains an evaluated document rejected by external-document policy.
    /// </summary>
    public const string WorkspaceExternalDocumentRejected = "WorkspaceExternalDocumentRejected";
}
