using Microsoft.Extensions.Options;

namespace Roslyn.Workbench.Mcp.Tools;

/// <summary>
/// Commits the active workspace transaction to disk.
/// </summary>
internal sealed class TransactionCommitTool : TransactionCommitToolBase<TransactionCommitRequest>
{
    private const string _commitOnce = "commit-once";
    private const string _commitForSession = "commit-for-session";
    private const string _doNotCommit = "do-not-commit";
    private static readonly UserInteractionRequest _confirmationRequest = CreateConfirmationRequest();

    private readonly OperationalPolicy _operationalPolicy;
    private readonly IMcpUserInteractionServiceFactory _interactionServiceFactory;
    private readonly ICommitConfirmationState _confirmationState;

    /// <summary>
    /// Initializes a new instance of the <see cref="TransactionCommitTool"/> class.
    /// </summary>
    /// <param name="startupOptions">The options that control server startup.</param>
    /// <param name="protocolFactory">The factory that creates protocol result payloads.</param>
    /// <param name="requestBinder">The binder that converts tool arguments into request values.</param>
    /// <param name="transactionService">The service that owns transaction state and operations.</param>
    /// <param name="operationalPolicy">The effective immutable operational policy.</param>
    /// <param name="interactionServiceFactory">The factory that isolates MCP elicitation behind a neutral interaction service.</param>
    /// <param name="confirmationState">The memory-only transaction confirmation state for this server process.</param>
    public TransactionCommitTool(
        IOptions<StartupOptions> startupOptions,
        IMcpToolProtocolFactory protocolFactory,
        IToolRequestBinder requestBinder,
        ITransactionService transactionService,
        OperationalPolicy operationalPolicy,
        IMcpUserInteractionServiceFactory interactionServiceFactory,
        ICommitConfirmationState confirmationState)
        : base(
            startupOptions,
            protocolFactory,
            requestBinder,
            transactionService,
            "Commits the current staged transaction to disk.")
    {
        _operationalPolicy = operationalPolicy;
        _interactionServiceFactory = interactionServiceFactory;
        _confirmationState = confirmationState;
    }

    /// <inheritdoc/>
    protected override async ValueTask<ToolResult<TransactionCommitData>> ExecuteAsync(
        TransactionCommitRequest request,
        CancellationToken cancellationToken)
    {
        return await CommitAuthorisedAsync(
            request,
            receiptAuthorisation: null,
            cancellationToken);
    }

    /// <inheritdoc/>
    protected override async ValueTask<CallToolResult> InvokeBoundRequestAsync(
        TransactionCommitRequest request,
        RequestContext<CallToolRequestParams> requestContext,
        CancellationToken cancellationToken)
    {
        if (_operationalPolicy.CommitAuthorisation != CommitAuthorisationPolicy.Confirmation
            || _confirmationState.IsApprovedForSession)
        {
            return await base.InvokeBoundRequestAsync(request, requestContext, cancellationToken);
        }

        var interactionService = _interactionServiceFactory.Create(requestContext.Server);
        var confirmationFailure = await ConfirmAsync(interactionService, cancellationToken);
        if (confirmationFailure is null)
        {
            return await base.InvokeBoundRequestAsync(request, requestContext, cancellationToken);
        }

        var content = ToolResultEnvelopeSerializer.CreateFailure(
            confirmationFailure.Error,
            confirmationFailure.RequiredAction);

        return CreateStructuredResult(content, isError: true);
    }

    private async ValueTask<ToolResult<TransactionCommitData>?> ConfirmAsync(
        IUserInteractionService interactionService,
        CancellationToken cancellationToken)
    {
        var confirmation = await interactionService.RequestAsync(_confirmationRequest, cancellationToken);
        if (!confirmation.IsAccepted)
        {
            return CreateConfirmationFailure(confirmation.Outcome);
        }

        switch (confirmation.SelectedValue)
        {
            case _commitOnce:
                return null;

            case _commitForSession:
                _confirmationState.ApproveForSession();
                return null;

            case _doNotCommit:
                return CreateNotApprovedFailure();

            default:
                return CreateInvalidResponseFailure();
        }
    }

    private static UserInteractionRequest CreateConfirmationRequest()
    {
        var choices = new UserInteractionChoice[]
        {
            new UserInteractionChoice
            {
                Value = _commitOnce,
                Title = "Yes, commit once",
            },
            new UserInteractionChoice
            {
                Value = _commitForSession,
                Title = "Yes, for this session",
            },
            new UserInteractionChoice
            {
                Value = _doNotCommit,
                Title = "No, don't commit",
            },
        };

        return new UserInteractionRequest
        {
            Message = "Commit the current staged transaction to source files?",
            Title = "Transaction commit confirmation",
            Description = "Choose whether to permit this commit or later transaction commits in the current server session.",
            Choices = Array.AsReadOnly(choices),
        };
    }

    private static ToolResult<TransactionCommitData> CreateConfirmationFailure(UserInteractionOutcome outcome)
    {
        if (outcome is UserInteractionOutcome.Declined or UserInteractionOutcome.Cancelled)
        {
            return CreateNotApprovedFailure();
        }

        if (outcome is UserInteractionOutcome.Unavailable or UserInteractionOutcome.Failed)
        {
            return CreateUnavailableFailure();
        }

        return CreateInvalidResponseFailure();
    }

    private static ToolResult<TransactionCommitData> CreateNotApprovedFailure()
    {
        return CreateCommitFailure(
            "TransactionCommitNotApproved",
            "No files were persisted because transaction commit was not approved. The transaction remains active. If no prompt was displayed, the client may have blocked MCP elicitation; enable interactive MCP requests before a deliberate retry.",
            requiredAction: null);
    }

    private static ToolResult<TransactionCommitData> CreateUnavailableFailure()
    {
        return CreateCommitFailure(
            "ApprovalUnavailable",
            "No files were persisted because the connected MCP client could not complete commit confirmation. The transaction remains active. Enable interactive MCP requests before a deliberate retry.",
            RequiredAction.Retry);
    }

    private static ToolResult<TransactionCommitData> CreateInvalidResponseFailure()
    {
        return CreateCommitFailure(
            "InvalidApprovalResponse",
            "No files were persisted because the client returned an invalid commit-confirmation response. The transaction remains active.",
            RequiredAction.Retry);
    }
}
