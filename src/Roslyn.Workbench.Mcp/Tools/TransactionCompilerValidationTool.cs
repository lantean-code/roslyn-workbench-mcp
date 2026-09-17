using Microsoft.Extensions.Options;

namespace Roslyn.Workbench.Mcp.Tools;

/// <summary>
/// Compares compiler errors in an active transaction baseline and staged snapshot.
/// </summary>
internal sealed class TransactionCompilerValidationTool : ServerOwnedToolBase<TransactionCompilerValidationRequest, TransactionCompilerValidationData>
{
    private readonly ITransactionService _transactionService;

    /// <summary>
    /// Initializes a new instance of the <see cref="TransactionCompilerValidationTool"/> class.
    /// </summary>
    /// <param name="startupOptions">The options that control server startup.</param>
    /// <param name="protocolFactory">The factory that creates protocol tool metadata.</param>
    /// <param name="requestBinder">The binder that validates and converts tool arguments.</param>
    /// <param name="transactionService">The service owning transaction compiler validation.</param>
    public TransactionCompilerValidationTool(
        IOptions<StartupOptions> startupOptions,
        IMcpToolProtocolFactory protocolFactory,
        IToolRequestBinder requestBinder,
        ITransactionService transactionService)
        : base(
            startupOptions,
            protocolFactory,
            requestBinder,
            ServerOwnedToolRegistration.TransactionValidateName,
            "Transaction Compiler Validation",
            "Compares compiler errors in the staged transaction with its captured baseline across affected loaded project evaluations.",
            readOnly: true,
            destructive: false)
    {
        _transactionService = transactionService;
    }

    /// <inheritdoc/>
    protected override async ValueTask<ToolResult<TransactionCompilerValidationData>> ExecuteAsync(
        TransactionCompilerValidationRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _transactionService.ValidateCompilerImpactAsync(
            request.Workspace?.WorkspaceId,
            request.Workspace?.Alias,
            request.Workspace?.Path,
            request.ExpectedSnapshot,
            cancellationToken);

        return WorkspaceToolResultMapper.Map(result, CreateData);
    }

    private static TransactionCompilerValidationData CreateData(
        TransactionCompilerValidationOutcome validation)
    {
        ToolContinuation continuation;
        if (validation.Succeeded)
        {
            continuation = ToolContinuation.CallTool(
                ServerOwnedToolRegistration.TransactionCommitName,
                "Commit the unchanged transaction snapshot; commit will authoritatively revalidate this result.");
        }
        else if (!validation.IsComplete)
        {
            continuation = ToolContinuation.CallTool(
                ServerOwnedToolRegistration.TransactionRollbackName,
                "Roll back the transaction, reload the Workspace, and start a new transaction before validating again.");
        }
        else
        {
            continuation = ToolContinuation.CallTool(
                ServerOwnedToolRegistration.TransactionValidateName,
                "Correct or restage the transaction, then validate the returned snapshot again.");
        }

        return new TransactionCompilerValidationData
        {
            Algorithm = validation.Algorithm,
            IsComplete = validation.IsComplete,
            Succeeded = validation.Succeeded,
            IncompleteReasons = validation.IncompleteReasons,
            Transaction = validation.Transaction,
            BaselineErrorCount = validation.BaselineErrorCount,
            StagedErrorCount = validation.StagedErrorCount,
            IntroducedErrorCount = validation.IntroducedErrorCount,
            DurationMilliseconds = validation.DurationMilliseconds,
            Projects = validation.Projects,
            IntroducedDiagnostics = validation.IntroducedDiagnostics,
            Limitations = validation.Limitations,
            Continuation = continuation,
        };
    }
}
