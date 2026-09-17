using System.Text.Json;
using Microsoft.Extensions.Options;

namespace Roslyn.Workbench.Mcp.Test.Tools;

public sealed class TransactionCompilerValidationToolTests
{
    [Theory]
    [InlineData(true, true, "transaction-commit", false)]
    [InlineData(true, true, "transaction-commit", true)]
    [InlineData(false, true, "transaction-validate", false)]
    [InlineData(false, true, "transaction-validate", true)]
    [InlineData(false, false, "transaction-rollback", false)]
    [InlineData(false, false, "transaction-rollback", true)]
    public async Task GIVEN_ValidationOutcome_WHEN_Validating_THEN_ShouldRouteAndMapContinuation(
        bool succeeded,
        bool isComplete,
        string expectedNextTool,
        bool includeWorkspace)
    {
        var expectedSnapshot = WorkspaceSnapshotTestFactory.CreatePrecondition(
            Guid.Parse("11111111-1111-1111-1111-111111111111"));

        var transaction = new TransactionInfo { Revision = 2 };
        var validation = new TransactionCompilerValidationOutcome
        {
            IsComplete = isComplete,
            Succeeded = succeeded,
            IncompleteReasons = isComplete
                ? []
                : [TransactionCompilerValidationIncompleteReason.CompilationUnavailable],
            Transaction = transaction,
            BaselineErrorCount = 1,
            StagedErrorCount = succeeded ? 1 : 2,
            IntroducedErrorCount = succeeded ? 0 : 1,
            DurationMilliseconds = 12,
        };

        var validationResult = WorkspaceOperationResult.Succeeded(validation);
        var service = new Mock<ITransactionService>();
        service
            .Setup(item => item.ValidateCompilerImpactAsync(
                ServerOwnedToolTestData.GetWorkspaceId(includeWorkspace),
                ServerOwnedToolTestData.GetWorkspaceAlias(includeWorkspace),
                ServerOwnedToolTestData.GetWorkspacePath(includeWorkspace),
                expectedSnapshot,
                CancellationToken.None))
            .ReturnsAsync(validationResult);

        var protocolFactory = McpToolProtocolFactoryMockFactory.Create();
        var boundRequest = new TransactionCompilerValidationRequest
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

        var startupOptions = new StartupOptions();
        var configuredStartupOptions = Options.Create(startupOptions);
        var target = new TransactionCompilerValidationTool(
            configuredStartupOptions,
            protocolFactory.Object,
            requestBinder.Object,
            service.Object);

        var arguments = ServerOwnedToolTestData.CreateWorkspaceArguments(includeWorkspace);
        arguments["expectedSnapshot"] = JsonSerializer.SerializeToElement(expectedSnapshot);

        var result = await ServerOwnedToolTestSupport.InvokeAsync(
            target,
            ServerOwnedToolRegistration.TransactionValidateName,
            arguments,
            CancellationToken.None);

        result.IsError.Should().BeFalse();
        var data = result.StructuredContent!.Value.GetProperty("data");
        data.GetProperty("succeeded").GetBoolean().Should().Be(succeeded);
        data.GetProperty("durationMilliseconds").GetInt64().Should().Be(12);
        data.GetProperty("continuation").GetProperty("tool").GetString().Should().Be(expectedNextTool);

        var incompleteReasons = data.GetProperty("incompleteReasons");
        incompleteReasons.GetArrayLength().Should().Be(isComplete ? 0 : 1);
        if (!isComplete)
        {
            incompleteReasons[0].GetString().Should().Be("CompilationUnavailable");
        }
    }
}
