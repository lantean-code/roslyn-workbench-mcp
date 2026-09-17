using System.Text.Json;

using Microsoft.Extensions.Options;

namespace Roslyn.Workbench.Mcp.Test.Tools;

public sealed class TransactionReviewToolTests
{
    [Fact]
    public async Task GIVEN_ValidatedReview_WHEN_ReviewingTransaction_THEN_ShouldStoreAndReturnReceiptProjection()
    {
        var workspaceId = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var snapshotId = Guid.Parse("22222222-2222-2222-2222-222222222222");
        var expectedSnapshot = WorkspaceSnapshotTestFactory.CreatePrecondition(snapshotId);
        var request = new TransactionReviewRequest
        {
            Workspace = ServerOwnedToolTestData.CreateWorkspaceSelector(),
            ExpectedSnapshot = expectedSnapshot,
            IncludeDiff = true,
            ContextLines = 5,
            Document = new DocumentSelector { Path = "Sample.cs" },
        };

        var identity = new TransactionReviewIdentity
        {
            Algorithm = "sha256-rwcs-v1",
            ChangeSetDigest = new string('a', 64),
            WorkspaceId = workspaceId,
            WorkspaceEpoch = 3,
            TransactionId = 7,
            SnapshotId = snapshotId,
            TransactionRevision = 2,
        };

        var documents = new TransactionReviewDocument[]
        {
            new() { Path = "Added.cs", Operation = WorkspaceFileOperation.Create, OriginalExists = false },
            new() { Path = "Modified.cs", Operation = WorkspaceFileOperation.Replace, OriginalExists = true },
            new() { Path = "Deleted.cs", Operation = WorkspaceFileOperation.Delete, OriginalExists = true },
        };
        var provider = new MutationProviderIdentity
        {
            TypeName = "Provider.Type",
            AssemblyName = "Provider.Assembly",
            AssemblyVersion = "1.0.0.0",
        };
        var codeAction = new CodeActionMutationProvenance
        {
            Kind = CodeActionMutationKind.CodeFix,
            Provider = provider,
        };
        var provenance = new TransactionMutationProvenance
        {
            Revision = 2,
            Operation = "stage-code-action",
            Summary = "Summary",
            CodeAction = codeAction,
        };

        var review = new TransactionReviewOutcome
        {
            Identity = identity,
            Transaction = new TransactionInfo { Revision = 2 },
            Documents = documents,
            Provenance = [provenance],
        };

        var expiresAt = new DateTimeOffset(2000, 1, 1, 0, 15, 0, TimeSpan.Zero);
        var receipt = new TransactionReceipt
        {
            ReceiptId = "ReceiptId",
            ExpiresAt = expiresAt,
            Review = review,
        };

        var transactionService = new Mock<ITransactionService>();
        transactionService
            .Setup(item => item.ReviewAsync(
                ServerOwnedToolTestData.GetWorkspaceId(includeWorkspace: true),
                ServerOwnedToolTestData.GetWorkspaceAlias(includeWorkspace: true),
                ServerOwnedToolTestData.GetWorkspacePath(includeWorkspace: true),
                expectedSnapshot,
                request.Document,
                true,
                5,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(WorkspaceOperationResult.Succeeded(review));

        var receiptStore = new Mock<ITransactionReceiptStore>();
        receiptStore.Setup(item => item.CreateOrGet(review)).Returns(receipt);
        string? errorMessage = null;
        var requestBinder = new Mock<IToolRequestBinder>();
        requestBinder
            .Setup(item => item.TryBind(
                It.IsAny<IDictionary<string, JsonElement>>(),
                out request,
                out errorMessage))
            .Returns(true);

        var protocolFactory = McpToolProtocolFactoryMockFactory.Create();
        var target = new TransactionReviewTool(
            Options.Create(new StartupOptions()),
            protocolFactory.Object,
            requestBinder.Object,
            transactionService.Object,
            receiptStore.Object);

        var result = await ServerOwnedToolTestSupport.InvokeAsync(
            target,
            "transaction-review",
            cancellationToken: TestContext.Current.CancellationToken);

        result.IsError.Should().BeFalse();
        var data = result.StructuredContent!.Value.GetProperty("data");
        data.GetProperty("receiptId").GetString().Should().Be("ReceiptId");
        data.GetProperty("changeSetDigest").GetString().Should().Be(identity.ChangeSetDigest);
        data.GetProperty("workspaceId").GetGuid().Should().Be(workspaceId);
        data.GetProperty("addedDocumentCount").GetInt32().Should().Be(1);
        data.GetProperty("modifiedDocumentCount").GetInt32().Should().Be(1);
        data.GetProperty("deletedDocumentCount").GetInt32().Should().Be(1);
        data.GetProperty("provenance")[0]
            .GetProperty("codeAction")
            .GetProperty("providerId")
            .GetString()
            .Should()
            .Be("p1");
        data.GetProperty("providers")
            .GetProperty("p1")
            .GetProperty("typeName")
            .GetString()
            .Should()
            .Be("Provider.Type");
        data.GetProperty("continuation").GetProperty("kind").GetString().Should().Be("CallTool");
        data.GetProperty("continuation").GetProperty("tool").GetString().Should().Be("transaction-commit");
        receiptStore.Verify(item => item.CreateOrGet(review), Times.Once);
    }

    [Fact]
    public async Task GIVEN_ReviewFailure_WHEN_ReviewingTransaction_THEN_ShouldReturnWorkspaceFailureWithoutReceipt()
    {
        var expectedSnapshot = WorkspaceSnapshotTestFactory.CreatePrecondition(
            Guid.Parse("22222222-2222-2222-2222-222222222222"));

        var request = new TransactionReviewRequest { ExpectedSnapshot = expectedSnapshot };
        var error = new WorkspaceOperationError
        {
            Code = "Code",
            Message = "Message",
        };

        var failure = WorkspaceOperationResult.Rejected<TransactionReviewOutcome>(error);
        var transactionService = new Mock<ITransactionService>();
        transactionService
            .Setup(item => item.ReviewAsync(
                null,
                null,
                null,
                expectedSnapshot,
                null,
                false,
                3,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(failure);

        var receiptStore = new Mock<ITransactionReceiptStore>();
        string? errorMessage = null;
        var requestBinder = new Mock<IToolRequestBinder>();
        requestBinder
            .Setup(item => item.TryBind(
                It.IsAny<IDictionary<string, JsonElement>>(),
                out request,
                out errorMessage))
            .Returns(true);

        var protocolFactory = McpToolProtocolFactoryMockFactory.Create();
        var target = new TransactionReviewTool(
            Options.Create(new StartupOptions()),
            protocolFactory.Object,
            requestBinder.Object,
            transactionService.Object,
            receiptStore.Object);

        var result = await ServerOwnedToolTestSupport.InvokeAsync(
            target,
            "transaction-review",
            cancellationToken: TestContext.Current.CancellationToken);

        result.IsError.Should().BeTrue();
        receiptStore.Verify(item => item.CreateOrGet(It.IsAny<TransactionReviewOutcome>()), Times.Never);
    }
}
