using System.Text.Json;
using Microsoft.Extensions.Options;

namespace Roslyn.Workbench.Mcp.Test.Tools;

public sealed class TransactionCommitConfirmationTests
{
    private readonly Mock<ITransactionService> _transactionService;
    private readonly Mock<IMcpUserInteractionServiceFactory> _interactionServiceFactory;
    private readonly Mock<IUserInteractionService> _interactionService;
    private readonly Mock<ICommitConfirmationState> _confirmationState;
    private readonly Mock<IToolRequestBinder> _requestBinder;

    public TransactionCommitConfirmationTests()
    {
        _transactionService = new Mock<ITransactionService>();
        _interactionServiceFactory = new Mock<IMcpUserInteractionServiceFactory>();
        _interactionService = new Mock<IUserInteractionService>();
        _confirmationState = new Mock<ICommitConfirmationState>();
        _requestBinder = new Mock<IToolRequestBinder>();

        var request = new TransactionCommitRequest
        {
            ExpectedSnapshot = WorkspaceSnapshotTestFactory.CreatePrecondition(
                Guid.Parse("11111111-1111-1111-1111-111111111111")),
        };

        string? errorMessage = null;
        _requestBinder
            .Setup(item => item.TryBind(
                It.IsAny<IDictionary<string, JsonElement>>(),
                out request,
                out errorMessage))
            .Returns(true);

        _interactionServiceFactory
            .Setup(item => item.Create(It.IsAny<McpServer>()))
            .Returns(_interactionService.Object);

        _transactionService
            .Setup(item => item.CommitAsync(
                null,
                null,
                null,
                It.IsAny<SnapshotPrecondition>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(WorkspaceOperationResult.Succeeded(new TransactionCommitOutcome
            {
                Committed = true,
            }));
    }

    [Fact]
    public async Task GIVEN_AutonomousMode_WHEN_Committing_THEN_ShouldBypassConfirmation()
    {
        var target = CreateTarget(OperationalMode.AutonomousTrusted);

        var result = await InvokeAsync(target);

        result.IsError.Should().BeFalse();
        _interactionServiceFactory.Verify(item => item.Create(It.IsAny<McpServer>()), Times.Never);
    }

    [Fact]
    public async Task GIVEN_TransactionalSessionApproval_WHEN_Committing_THEN_ShouldBypassConfirmation()
    {
        _confirmationState.SetupGet(item => item.IsApprovedForSession).Returns(true);
        var target = CreateTarget(OperationalMode.Transactional);

        var result = await InvokeAsync(target);

        result.IsError.Should().BeFalse();
        _interactionServiceFactory.Verify(item => item.Create(It.IsAny<McpServer>()), Times.Never);
    }

    [Fact]
    public async Task GIVEN_CommitOnceApproval_WHEN_Committing_THEN_ShouldCommitWithoutChangingSessionState()
    {
        _interactionService
            .Setup(item => item.RequestAsync(
                It.Is<UserInteractionRequest>(request => IsExpectedConfirmation(request)),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(UserInteractionResult.Accepted("commit-once"));

        var target = CreateTarget(OperationalMode.Transactional);

        var result = await InvokeAsync(target);

        result.IsError.Should().BeFalse();
        _confirmationState.Verify(item => item.ApproveForSession(), Times.Never);
        _transactionService.Verify(item => item.CommitAsync(
            null,
            null,
            null,
            It.IsAny<SnapshotPrecondition>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GIVEN_SessionApprovalChoice_WHEN_Committing_THEN_ShouldApproveSessionAndCommit()
    {
        _interactionService
            .Setup(item => item.RequestAsync(It.IsAny<UserInteractionRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(UserInteractionResult.Accepted("commit-for-session"));

        var target = CreateTarget(OperationalMode.Transactional);

        var result = await InvokeAsync(target);

        result.IsError.Should().BeFalse();
        _confirmationState.Verify(item => item.ApproveForSession(), Times.Once);
        _transactionService.Verify(item => item.CommitAsync(
            null,
            null,
            null,
            It.IsAny<SnapshotPrecondition>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Theory]
    [InlineData((int)UserInteractionOutcome.Declined)]
    [InlineData((int)UserInteractionOutcome.Cancelled)]
    public async Task GIVEN_ConfirmationNotApproved_WHEN_Committing_THEN_ShouldLeaveTransactionActive(int outcomeValue)
    {
        var outcome = (UserInteractionOutcome)outcomeValue;
        _interactionService
            .Setup(item => item.RequestAsync(It.IsAny<UserInteractionRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(UserInteractionResult.NotAccepted(outcome));

        var target = CreateTarget(OperationalMode.Transactional);

        var result = await InvokeAsync(target);

        AssertFailure(result, "TransactionCommitNotApproved", expectedContinuationKind: null);
        result.StructuredContent!.Value.GetProperty("error").GetProperty("message").GetString()
            .Should().Contain("transaction remains active");

        _transactionService.Verify(item => item.CommitAsync(
            It.IsAny<Guid?>(),
            It.IsAny<string?>(),
            It.IsAny<string?>(),
            It.IsAny<SnapshotPrecondition?>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Theory]
    [InlineData((int)UserInteractionOutcome.Unavailable)]
    [InlineData((int)UserInteractionOutcome.Failed)]
    public async Task GIVEN_ConfirmationUnavailable_WHEN_Committing_THEN_ShouldReturnRetryGuidance(int outcomeValue)
    {
        var outcome = (UserInteractionOutcome)outcomeValue;
        _interactionService
            .Setup(item => item.RequestAsync(It.IsAny<UserInteractionRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(UserInteractionResult.NotAccepted(outcome));

        var target = CreateTarget(OperationalMode.Transactional);

        var result = await InvokeAsync(target);

        AssertFailure(result, "ApprovalUnavailable", "RetryRequest");
        result.StructuredContent!.Value.GetProperty("error").GetProperty("message").GetString()
            .Should().Contain("Enable interactive MCP requests");
    }

    [Fact]
    public async Task GIVEN_InvalidConfirmationResponse_WHEN_Committing_THEN_ShouldRejectCommit()
    {
        _interactionService
            .Setup(item => item.RequestAsync(It.IsAny<UserInteractionRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(UserInteractionResult.NotAccepted(UserInteractionOutcome.InvalidResponse));

        var target = CreateTarget(OperationalMode.Transactional);

        var result = await InvokeAsync(target);

        AssertFailure(result, "InvalidApprovalResponse", "RetryRequest");
    }

    [Theory]
    [InlineData("do-not-commit", "TransactionCommitNotApproved", null)]
    [InlineData("unsupported", "InvalidApprovalResponse", "RetryRequest")]
    public async Task GIVEN_AcceptedConfirmationChoice_WHEN_ChoiceDoesNotAuthoriseCommit_THEN_ShouldReject(
        string selectedValue,
        string expectedCode,
        string? expectedContinuationKind)
    {
        _interactionService
            .Setup(item => item.RequestAsync(It.IsAny<UserInteractionRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(UserInteractionResult.Accepted(selectedValue));

        var target = CreateTarget(OperationalMode.Transactional);

        var result = await InvokeAsync(target);

        AssertFailure(result, expectedCode, expectedContinuationKind);
        _transactionService.Verify(item => item.CommitAsync(
            It.IsAny<Guid?>(),
            It.IsAny<string?>(),
            It.IsAny<string?>(),
            It.IsAny<SnapshotPrecondition?>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    private TransactionCommitTool CreateTarget(OperationalMode mode)
    {
        var protocolFactory = McpToolProtocolFactoryMockFactory.Create();
        var policy = OperationalPolicyResolver.Resolve(mode);

        return new TransactionCommitTool(
            Options.Create(new StartupOptions()),
            protocolFactory.Object,
            _requestBinder.Object,
            _transactionService.Object,
            policy,
            _interactionServiceFactory.Object,
            _confirmationState.Object);
    }

    private static Task<CallToolResult> InvokeAsync(TransactionCommitTool target)
    {
        return ServerOwnedToolTestSupport.InvokeAsync(
            target,
            "transaction-commit",
            cancellationToken: TestContext.Current.CancellationToken);
    }

    private static bool IsExpectedConfirmation(UserInteractionRequest request)
    {
        return request.Message.Contains("Commit", StringComparison.Ordinal)
            && request.Title == "Transaction commit confirmation"
            && request.Choices.Select(static choice => choice.Value).SequenceEqual(
            [
                "commit-once",
                "commit-for-session",
                "do-not-commit",
            ]);
    }

    private static void AssertFailure(
        CallToolResult result,
        string expectedCode,
        string? expectedContinuationKind)
    {
        result.IsError.Should().BeTrue();
        var structuredContent = result.StructuredContent!.Value;
        structuredContent.GetProperty("error").GetProperty("code").GetString().Should().Be(expectedCode);

        if (expectedContinuationKind is null)
        {
            structuredContent.TryGetProperty("continuation", out _).Should().BeFalse();
            return;
        }

        structuredContent.GetProperty("continuation").GetProperty("kind").GetString().Should().Be(expectedContinuationKind);
    }
}
