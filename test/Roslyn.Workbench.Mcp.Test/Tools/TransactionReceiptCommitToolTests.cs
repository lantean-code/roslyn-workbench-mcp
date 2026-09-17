using System.Text.Json;
using Microsoft.Extensions.Options;

namespace Roslyn.Workbench.Mcp.Test.Tools;

public sealed class TransactionReceiptCommitToolTests
{
    private readonly Mock<ITransactionService> _transactionService = new();
    private readonly Mock<ITransactionReceiptStore> _receiptStore = new();
    private readonly Mock<IMcpUserInteractionServiceFactory> _interactionServiceFactory = new();
    private readonly Mock<IUserInteractionService> _interactionService = new();
    private readonly Mock<IToolRequestBinder> _requestBinder = new();
    private readonly TransactionReviewIdentity _identity;
    private readonly TransactionReceipt _receipt;
    private readonly TransactionReceiptCommitTool _target;

    public TransactionReceiptCommitToolTests()
    {
        var expectedSnapshot = WorkspaceSnapshotTestFactory.CreatePrecondition(
            Guid.Parse("22222222-2222-2222-2222-222222222222"));

        _identity = new TransactionReviewIdentity
        {
            Algorithm = "sha256-rwcs-v1",
            ChangeSetDigest = new string('a', 64),
            WorkspaceId = expectedSnapshot.WorkspaceId,
            WorkspaceEpoch = expectedSnapshot.WorkspaceEpoch,
            TransactionId = 7,
            SnapshotId = expectedSnapshot.SnapshotId,
            TransactionRevision = expectedSnapshot.TransactionRevision ?? 0,
        };

        var addedDocument = new TransactionReviewDocument
        {
            Path = "Added.cs",
            Operation = WorkspaceFileOperation.Create,
            OriginalExists = false,
        };

        var modifiedDocument = new TransactionReviewDocument
        {
            Path = "Modified.cs",
            Operation = WorkspaceFileOperation.Replace,
            OriginalExists = true,
        };

        var deletedDocument = new TransactionReviewDocument
        {
            Path = "Deleted.cs",
            Operation = WorkspaceFileOperation.Delete,
            OriginalExists = true,
        };

        var transaction = new TransactionInfo { Revision = _identity.TransactionRevision };
        var review = new TransactionReviewOutcome
        {
            Identity = _identity,
            Transaction = transaction,
            Documents = [addedDocument, modifiedDocument, deletedDocument],
        };

        _receipt = new TransactionReceipt
        {
            ReceiptId = "ReceiptId",
            ExpiresAt = new DateTimeOffset(2000, 1, 1, 0, 15, 0, TimeSpan.Zero),
            Review = review,
        };

        var request = new TransactionReceiptCommitRequest
        {
            ReceiptId = _receipt.ReceiptId,
            ExpectedSnapshot = expectedSnapshot,
            Workspace = new WorkspaceSelector
            {
                WorkspaceId = expectedSnapshot.WorkspaceId,
                Alias = "Alias",
                Path = "Path",
            },
        };

        string? errorMessage = null;
        _requestBinder
            .Setup(item => item.TryBind(
                It.IsAny<IDictionary<string, JsonElement>>(),
                out request,
                out errorMessage))
            .Returns(true);

        _receiptStore.Setup(item => item.Resolve(_receipt.ReceiptId)).Returns(TransactionReceiptResolution.Available(_receipt));
        _transactionService
            .Setup(item => item.ValidateReceiptAsync(
                expectedSnapshot.WorkspaceId,
                "Alias",
                "Path",
                expectedSnapshot,
                _identity,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(WorkspaceOperationResult.Succeeded(_identity));

        _interactionServiceFactory.Setup(item => item.Create(It.IsAny<McpServer>())).Returns(_interactionService.Object);

        _target = CreateTarget(CommitValidationPolicy.None);
    }

    [Theory]
    [InlineData(true, WorkspaceErrorCodes.NewCompilerErrors, null, null)]
    [InlineData(false, WorkspaceErrorCodes.CompilerValidationIncomplete, "CallTool", "transaction-rollback")]
    public async Task GIVEN_CompilerValidationFails_WHEN_CommittingReceipt_THEN_ShouldRejectBeforeElicitation(
        bool isComplete,
        string expectedCode,
        string? expectedContinuation,
        string? expectedTool)
    {
        var validation = new TransactionCompilerValidationOutcome
        {
            IsComplete = isComplete,
            Succeeded = false,
            IncompleteReasons = isComplete
                ? []
                : [TransactionCompilerValidationIncompleteReason.CompilationUnavailable],
            Transaction = _receipt.Review.Transaction,
            BaselineErrorCount = 0,
            StagedErrorCount = 1,
            IntroducedErrorCount = 1,
            DurationMilliseconds = 1,
        };

        var validationResult = WorkspaceOperationResult.Succeeded(validation);
        _transactionService
            .Setup(item => item.ValidateCompilerImpactAsync(
                _identity.WorkspaceId,
                "Alias",
                "Path",
                It.IsAny<SnapshotPrecondition>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(validationResult);

        var target = CreateTarget(CommitValidationPolicy.NoNewCompilerErrors);
        var result = await InvokeAsync(target);

        AssertFailure(result, expectedCode, expectedContinuation, expectedTool);
        _interactionService.Verify(
            item => item.RequestAsync(It.IsAny<UserInteractionRequest>(), It.IsAny<CancellationToken>()),
            Times.Never);

        _receiptStore.Verify(
            item => item.Consume(It.IsAny<string>(), It.IsAny<TransactionReviewIdentity>()),
            Times.Never);
    }

    [Fact]
    public async Task GIVEN_CurrentReceiptAndApproval_WHEN_Committing_THEN_ShouldConsumeAndCommitExactIdentity()
    {
        _interactionService
            .Setup(item => item.RequestAsync(
                It.Is<UserInteractionRequest>(request => IsExpectedApproval(request)),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(UserInteractionResult.Accepted("approve-this-receipt"));

        _receiptStore
            .Setup(item => item.Consume(_receipt.ReceiptId, _identity))
            .Returns(TransactionReceiptResolution.Available(_receipt));

        _transactionService
            .Setup(item => item.CommitAsync(
                _identity.WorkspaceId,
                "Alias",
                "Path",
                It.IsAny<SnapshotPrecondition>(),
                It.Is<TransactionReceiptAuthorisation>(authorisation => authorisation.Identity == _identity),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(WorkspaceOperationResult.Succeeded(new TransactionCommitOutcome { Committed = true }));

        var result = await InvokeAsync();

        result.IsError.Should().BeFalse();
        result.StructuredContent!.Value.GetProperty("data").GetProperty("committed").GetBoolean().Should().BeTrue();
        _receiptStore.Verify(item => item.Consume(_receipt.ReceiptId, _identity), Times.Once);
    }

    [Fact]
    public async Task GIVEN_NoMcpRequestContext_WHEN_Committing_THEN_ShouldReportApprovalUnavailable()
    {
        var arguments = new Dictionary<string, JsonElement>();

        var result = await _target.InvokeArgumentsAsync(arguments, TestContext.Current.CancellationToken);

        AssertFailure(result, "ApprovalUnavailable", "RetryRequest", expectedTool: null);
        _receiptStore.Verify(item => item.Resolve(It.IsAny<string>()), Times.Never);
    }

    [Theory]
    [InlineData((int)TransactionReceiptResolutionStatus.Missing)]
    [InlineData((int)TransactionReceiptResolutionStatus.Expired)]
    public async Task GIVEN_UnavailableReceipt_WHEN_Committing_THEN_ShouldRequestNewReviewWithoutPrompt(int statusValue)
    {
        var status = (TransactionReceiptResolutionStatus)statusValue;
        _receiptStore.Setup(item => item.Resolve(_receipt.ReceiptId)).Returns(TransactionReceiptResolution.Unavailable(status));

        var result = await InvokeAsync();

        AssertFailure(result, "TransactionReceiptUnavailable", "CallTool", "transaction-review");
        _interactionService.Verify(
            item => item.RequestAsync(It.IsAny<UserInteractionRequest>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task GIVEN_StaleReceipt_WHEN_Committing_THEN_ShouldReturnWorkspaceConflictWithoutPrompt()
    {
        var error = new WorkspaceOperationError
        {
            Code = WorkspaceErrorCodes.TransactionReceiptMismatch,
            Message = "Message",
            RequiredAction = RequiredAction.ReviewTransaction,
        };

        _transactionService
            .Setup(item => item.ValidateReceiptAsync(
                It.IsAny<Guid?>(),
                It.IsAny<string?>(),
                It.IsAny<string?>(),
                It.IsAny<SnapshotPrecondition?>(),
                It.IsAny<TransactionReviewIdentity>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(WorkspaceOperationResult.Conflict<TransactionReviewIdentity>(error));

        var result = await InvokeAsync();

        AssertFailure(result, WorkspaceErrorCodes.TransactionReceiptMismatch, "CallTool", "transaction-review");
        _interactionService.Verify(
            item => item.RequestAsync(It.IsAny<UserInteractionRequest>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Theory]
    [InlineData((int)UserInteractionOutcome.Declined, "TransactionCommitNotApproved", null)]
    [InlineData((int)UserInteractionOutcome.Cancelled, "TransactionCommitNotApproved", null)]
    [InlineData((int)UserInteractionOutcome.Unavailable, "ApprovalUnavailable", "RetryRequest")]
    [InlineData((int)UserInteractionOutcome.Failed, "ApprovalUnavailable", "RetryRequest")]
    [InlineData((int)UserInteractionOutcome.InvalidResponse, "InvalidApprovalResponse", "RetryRequest")]
    public async Task GIVEN_ApprovalNotAccepted_WHEN_Committing_THEN_ShouldFailClosedWithoutConsumption(
        int outcomeValue,
        string expectedCode,
        string? expectedContinuation)
    {
        var outcome = (UserInteractionOutcome)outcomeValue;
        _interactionService
            .Setup(item => item.RequestAsync(It.IsAny<UserInteractionRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(UserInteractionResult.NotAccepted(outcome));

        var result = await InvokeAsync();

        AssertFailure(result, expectedCode, expectedContinuation, expectedTool: null);
        _receiptStore.Verify(
            item => item.Consume(It.IsAny<string>(), It.IsAny<TransactionReviewIdentity>()),
            Times.Never);
    }

    [Theory]
    [InlineData("do-not-commit", "TransactionCommitNotApproved", null)]
    [InlineData("unsupported", "InvalidApprovalResponse", "RetryRequest")]
    public async Task GIVEN_NonAuthorisingChoice_WHEN_Committing_THEN_ShouldLeaveReceiptAvailable(
        string choice,
        string expectedCode,
        string? expectedContinuation)
    {
        _interactionService
            .Setup(item => item.RequestAsync(It.IsAny<UserInteractionRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(UserInteractionResult.Accepted(choice));

        var result = await InvokeAsync();

        AssertFailure(result, expectedCode, expectedContinuation, expectedTool: null);
        _receiptStore.Verify(
            item => item.Consume(It.IsAny<string>(), It.IsAny<TransactionReviewIdentity>()),
            Times.Never);
    }

    [Fact]
    public async Task GIVEN_ReceiptChangesWhilePromptOpen_WHEN_ApprovalReturns_THEN_ShouldRejectWithoutCommit()
    {
        _interactionService
            .Setup(item => item.RequestAsync(It.IsAny<UserInteractionRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(UserInteractionResult.Accepted("approve-this-receipt"));

        _receiptStore
            .Setup(item => item.Consume(_receipt.ReceiptId, _identity))
            .Returns(TransactionReceiptResolution.Unavailable(TransactionReceiptResolutionStatus.Missing));

        var result = await InvokeAsync();

        AssertFailure(result, "TransactionReceiptUnavailable", "CallTool", "transaction-review");
        _transactionService.Verify(
            item => item.CommitAsync(
                It.IsAny<Guid?>(),
                It.IsAny<string?>(),
                It.IsAny<string?>(),
                It.IsAny<SnapshotPrecondition?>(),
                It.IsAny<TransactionReceiptAuthorisation>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    private Task<CallToolResult> InvokeAsync()
    {
        return InvokeAsync(_target);
    }

    private static Task<CallToolResult> InvokeAsync(TransactionReceiptCommitTool target)
    {
        return ServerOwnedToolTestSupport.InvokeAsync(
            target,
            "transaction-commit",
            cancellationToken: TestContext.Current.CancellationToken);
    }

    private TransactionReceiptCommitTool CreateTarget(CommitValidationPolicy commitValidation)
    {
        var protocolFactory = McpToolProtocolFactoryMockFactory.Create();
        var policy = OperationalPolicyResolver.Resolve(
            OperationalMode.ApprovalRequired,
            commitValidation);

        var startupOptions = new StartupOptions();
        var configuredStartupOptions = Options.Create(startupOptions);
        return new TransactionReceiptCommitTool(
            configuredStartupOptions,
            protocolFactory.Object,
            _requestBinder.Object,
            _transactionService.Object,
            _receiptStore.Object,
            _interactionServiceFactory.Object,
            policy);
    }

    private bool IsExpectedApproval(UserInteractionRequest request)
    {
        return request.Message.Contains(_identity.ChangeSetDigest, StringComparison.Ordinal)
            && request.Message.Contains("3 file(s): 1 added, 1 modified, 1 deleted", StringComparison.Ordinal)
            && request.Choices.Select(static choice => choice.Value).SequenceEqual(["approve-this-receipt", "do-not-commit"]);
    }

    private static void AssertFailure(
        CallToolResult result,
        string expectedCode,
        string? expectedContinuation,
        string? expectedTool)
    {
        result.IsError.Should().BeTrue();
        var content = result.StructuredContent!.Value;
        content.GetProperty("error").GetProperty("code").GetString().Should().Be(expectedCode);
        if (expectedContinuation is null)
        {
            content.TryGetProperty("continuation", out _).Should().BeFalse();
            return;
        }

        var continuation = content.GetProperty("continuation");
        continuation.GetProperty("kind").GetString().Should().Be(expectedContinuation);
        if (expectedTool is not null)
        {
            continuation.GetProperty("tool").GetString().Should().Be(expectedTool);
        }
    }
}
