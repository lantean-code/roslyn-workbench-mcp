using System.Text.Json;
using Microsoft.Extensions.Options;

namespace Roslyn.Workbench.Mcp.Test.Tools;

public sealed class TransactionCommitToolTests
{
    [Fact]
    public async Task GIVEN_CompilerValidationFails_WHEN_Committing_THEN_ShouldRejectBeforeElicitation()
    {
        var expectedSnapshot = WorkspaceSnapshotTestFactory.CreatePrecondition(
            Guid.Parse("11111111-1111-1111-1111-111111111111"));

        var transaction = new TransactionInfo { Revision = 1 };
        var validation = new TransactionCompilerValidationOutcome
        {
            IsComplete = true,
            Succeeded = false,
            Transaction = transaction,
            BaselineErrorCount = 0,
            StagedErrorCount = 1,
            IntroducedErrorCount = 1,
            DurationMilliseconds = 1,
        };

        var validationResult = WorkspaceOperationResult.Succeeded(validation);
        var service = new Mock<ITransactionService>();
        service
            .Setup(item => item.ValidateCompilerImpactAsync(
                null,
                null,
                null,
                expectedSnapshot,
                CancellationToken.None))
            .ReturnsAsync(validationResult);

        var boundRequest = new TransactionCommitRequest
        {
            ExpectedSnapshot = expectedSnapshot,
        };

        string? errorMessage = null;
        var requestBinder = new Mock<IToolRequestBinder>();
        requestBinder
            .Setup(item => item.TryBind(
                It.IsAny<IDictionary<string, JsonElement>>(),
                out boundRequest,
                out errorMessage))
            .Returns(true);

        var interactionServiceFactory = new Mock<IMcpUserInteractionServiceFactory>();
        var confirmationState = new Mock<ICommitConfirmationState>();
        var startupOptions = new StartupOptions();
        var configuredStartupOptions = Options.Create(startupOptions);
        var protocolFactory = McpToolProtocolFactoryMockFactory.Create();
        var policy = OperationalPolicyResolver.Resolve(
            OperationalMode.Transactional,
            CommitValidationPolicy.NoNewCompilerErrors);

        var target = new TransactionCommitTool(
            configuredStartupOptions,
            protocolFactory.Object,
            requestBinder.Object,
            service.Object,
            policy,
            interactionServiceFactory.Object,
            confirmationState.Object);

        var arguments = new Dictionary<string, JsonElement>
        {
            ["expectedSnapshot"] = JsonSerializer.SerializeToElement(expectedSnapshot),
        };

        var result = await ServerOwnedToolTestSupport.InvokeAsync(
            target,
            ServerOwnedToolRegistration.TransactionCommitName,
            arguments,
            CancellationToken.None);

        result.IsError.Should().BeTrue();
        interactionServiceFactory.Verify(
            item => item.Create(It.IsAny<McpServer>()),
            Times.Never);

        service.Verify(
            item => item.CommitAsync(
                It.IsAny<Guid?>(),
                It.IsAny<string?>(),
                It.IsAny<string?>(),
                It.IsAny<SnapshotPrecondition?>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task GIVEN_ValidatedUnchangedTransaction_WHEN_Committing_THEN_ShouldPreserveNoChangeOutcome()
    {
        var expectedSnapshot = WorkspaceSnapshotTestFactory.CreatePrecondition(
            Guid.Parse("11111111-1111-1111-1111-111111111111"),
            transactionRevision: 0);

        var transaction = new TransactionInfo { Revision = 0 };
        var service = new Mock<ITransactionService>();
        service
            .Setup(item => item.ValidateCompilerImpactAsync(
                null,
                null,
                null,
                expectedSnapshot,
                CancellationToken.None))
            .ReturnsAsync(WorkspaceOperationResult.Succeeded(new TransactionCompilerValidationOutcome
            {
                IsComplete = true,
                Succeeded = true,
                Transaction = transaction,
                BaselineErrorCount = 0,
                StagedErrorCount = 0,
                IntroducedErrorCount = 0,
                DurationMilliseconds = 0,
            }));

        service
            .Setup(item => item.CommitAsync(
                null,
                null,
                null,
                expectedSnapshot,
                CancellationToken.None))
            .ReturnsAsync(WorkspaceOperationResult.NoChange(new TransactionCommitOutcome
            {
                Committed = false,
                Transaction = transaction,
            }));

        var boundRequest = new TransactionCommitRequest
        {
            ExpectedSnapshot = expectedSnapshot,
        };

        string? errorMessage = null;
        var requestBinder = new Mock<IToolRequestBinder>();
        requestBinder
            .Setup(item => item.TryBind(
                It.IsAny<IDictionary<string, JsonElement>>(),
                out boundRequest,
                out errorMessage))
            .Returns(true);

        var target = new TransactionCommitTool(
            Options.Create(new StartupOptions()),
            McpToolProtocolFactoryMockFactory.Create().Object,
            requestBinder.Object,
            service.Object,
            OperationalPolicyResolver.Resolve(
                OperationalMode.AutonomousTrusted,
                CommitValidationPolicy.NoNewCompilerErrors),
            Mock.Of<IMcpUserInteractionServiceFactory>(),
            Mock.Of<ICommitConfirmationState>());

        var arguments = new Dictionary<string, JsonElement>
        {
            ["expectedSnapshot"] = JsonSerializer.SerializeToElement(expectedSnapshot),
        };

        var result = await ServerOwnedToolTestSupport.InvokeAsync(
            target,
            ServerOwnedToolRegistration.TransactionCommitName,
            arguments,
            CancellationToken.None);

        result.IsError.Should().BeFalse();
        var data = result.StructuredContent!.Value.GetProperty("data");
        data.GetProperty("committed").GetBoolean().Should().BeFalse();
        data.GetProperty("transaction").GetProperty("revision").GetInt32().Should().Be(0);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task GIVEN_OptionalWorkspace_WHEN_CommittingTransaction_THEN_ShouldRouteAndMapResult(bool includeWorkspace)
    {
        var service = new Mock<ITransactionService>();
        service
            .Setup(item => item.CommitAsync(
                ServerOwnedToolTestData.GetWorkspaceId(includeWorkspace),
                ServerOwnedToolTestData.GetWorkspaceAlias(includeWorkspace),
                ServerOwnedToolTestData.GetWorkspacePath(includeWorkspace),
                It.IsAny<SnapshotPrecondition>(),
                CancellationToken.None))
            .ReturnsAsync(WorkspaceOperationResult.Succeeded(new TransactionCommitOutcome
            {
                Committed = true,
                Transaction = new TransactionInfo
                {
                    Revision = 4,
                },
            }));

        var protocolFactory = McpToolProtocolFactoryMockFactory.Create();
        var expectedSnapshot = WorkspaceSnapshotTestFactory.CreatePrecondition(
            Guid.Parse("11111111-1111-1111-1111-111111111111"));

        var boundRequest = new TransactionCommitRequest
        {
            Workspace = includeWorkspace ? ServerOwnedToolTestData.CreateWorkspaceSelector() : null,
            ExpectedSnapshot = expectedSnapshot,
        };

        string? errorMessage = null;
        var requestBinder = new Mock<IToolRequestBinder>();
        requestBinder
            .Setup(item => item.TryBind(
                It.IsAny<IDictionary<string, JsonElement>>(),
                out boundRequest,
                out errorMessage))
            .Returns(true);

        var interactionServiceFactory = new Mock<IMcpUserInteractionServiceFactory>();
        var confirmationState = new Mock<ICommitConfirmationState>();
        var target = new TransactionCommitTool(
            Options.Create(new StartupOptions()),
            protocolFactory.Object,
            requestBinder.Object,
            service.Object,
            OperationalPolicyResolver.Resolve(OperationalMode.AutonomousTrusted),
            interactionServiceFactory.Object,
            confirmationState.Object);

        var arguments = ServerOwnedToolTestData.CreateWorkspaceArguments(includeWorkspace);
        arguments["expectedSnapshot"] = JsonSerializer.SerializeToElement(WorkspaceSnapshotTestFactory.CreatePrecondition(Guid.Parse("11111111-1111-1111-1111-111111111111")));

        var result = await ServerOwnedToolTestSupport.InvokeAsync(
            target,
            "transaction-commit",
            arguments,
            CancellationToken.None);

        result.IsError.Should().BeFalse();
        var data = result.StructuredContent!.Value.GetProperty("data");
        data.GetProperty("committed").GetBoolean().Should().BeTrue();
        data.GetProperty("transaction").GetProperty("revision").GetInt32().Should().Be(4);
        service.Verify(item => item.CommitAsync(
            ServerOwnedToolTestData.GetWorkspaceId(includeWorkspace),
            ServerOwnedToolTestData.GetWorkspaceAlias(includeWorkspace),
            ServerOwnedToolTestData.GetWorkspacePath(includeWorkspace),
            It.IsAny<SnapshotPrecondition>(),
            CancellationToken.None), Times.Once);
    }
}
