namespace Roslyn.Workbench.Mcp.Test.Transactions;

public sealed class TransactionReceiptStoreTests
{
    private static readonly DateTimeOffset _now = new(2000, 1, 1, 0, 0, 0, TimeSpan.Zero);

    [Fact]
    public void GIVEN_SameCurrentReview_WHEN_CreatingTwice_THEN_ShouldReuseReceipt()
    {
        var timeProvider = CreateTimeProvider(_now);
        var target = new TransactionReceiptStore(timeProvider.Object);
        var review = CreateReview(Guid.Parse("11111111-1111-1111-1111-111111111111"), revision: 1);

        var first = target.CreateOrGet(review);
        var second = target.CreateOrGet(review);

        second.Should().BeSameAs(first);
        first.ExpiresAt.Should().Be(_now.AddMinutes(15));
    }

    [Fact]
    public void GIVEN_NewReviewForWorkspace_WHEN_Creating_THEN_ShouldReplacePreviousReceipt()
    {
        var timeProvider = CreateTimeProvider(_now);
        var target = new TransactionReceiptStore(timeProvider.Object);
        var firstReview = CreateReview(Guid.Parse("11111111-1111-1111-1111-111111111111"), revision: 1);
        var secondReview = CreateReview(Guid.Parse("11111111-1111-1111-1111-111111111111"), revision: 2);
        var first = target.CreateOrGet(firstReview);

        var second = target.CreateOrGet(secondReview);

        second.ReceiptId.Should().NotBe(first.ReceiptId);
        target.Resolve(first.ReceiptId).Status.Should().Be(TransactionReceiptResolutionStatus.Missing);
        target.Resolve(second.ReceiptId).Receipt.Should().BeSameAs(second);
    }

    [Fact]
    public void GIVEN_ExpiredReceipt_WHEN_Resolving_THEN_ShouldRemoveIt()
    {
        var timeProvider = new Mock<TimeProvider>();
        timeProvider.SetupSequence(item => item.GetUtcNow()).Returns(_now).Returns(_now.AddMinutes(15));
        var target = new TransactionReceiptStore(timeProvider.Object);
        var review = CreateReview(Guid.Parse("11111111-1111-1111-1111-111111111111"), revision: 1);
        var receipt = target.CreateOrGet(review);

        var expired = target.Resolve(receipt.ReceiptId);
        var removed = target.Resolve(receipt.ReceiptId);

        expired.Status.Should().Be(TransactionReceiptResolutionStatus.Expired);
        expired.IsAvailable.Should().BeFalse();
        removed.Status.Should().Be(TransactionReceiptResolutionStatus.Missing);
    }

    [Fact]
    public void GIVEN_CurrentReceipt_WHEN_Consuming_THEN_ShouldReturnItOnce()
    {
        var timeProvider = CreateTimeProvider(_now);
        var target = new TransactionReceiptStore(timeProvider.Object);
        var review = CreateReview(Guid.Parse("11111111-1111-1111-1111-111111111111"), revision: 1);
        var receipt = target.CreateOrGet(review);

        var consumed = target.Consume(receipt.ReceiptId, review.Identity);
        var replay = target.Consume(receipt.ReceiptId, review.Identity);

        consumed.Receipt.Should().BeSameAs(receipt);
        replay.Status.Should().Be(TransactionReceiptResolutionStatus.Missing);
    }

    [Fact]
    public void GIVEN_IdentityChangedDuringApproval_WHEN_Consuming_THEN_ShouldRejectWithoutRemovingReceipt()
    {
        var timeProvider = CreateTimeProvider(_now);
        var target = new TransactionReceiptStore(timeProvider.Object);
        var review = CreateReview(Guid.Parse("11111111-1111-1111-1111-111111111111"), revision: 1);
        var changedReview = CreateReview(review.Identity.WorkspaceId, revision: 2);
        var receipt = target.CreateOrGet(review);

        var rejected = target.Consume(receipt.ReceiptId, changedReview.Identity);

        rejected.Status.Should().Be(TransactionReceiptResolutionStatus.Missing);
        target.Resolve(receipt.ReceiptId).Receipt.Should().BeSameAs(receipt);
    }

    [Fact]
    public void GIVEN_AvailableStatusWithoutReceipt_WHEN_CreatingUnavailableResolution_THEN_ShouldRejectInvalidState()
    {
        var action = () => TransactionReceiptResolution.Unavailable(TransactionReceiptResolutionStatus.Available);

        action.Should().Throw<ArgumentOutOfRangeException>();
    }

    private static Mock<TimeProvider> CreateTimeProvider(DateTimeOffset now)
    {
        var timeProvider = new Mock<TimeProvider>();
        timeProvider.Setup(item => item.GetUtcNow()).Returns(now);
        return timeProvider;
    }

    private static TransactionReviewOutcome CreateReview(Guid workspaceId, int revision)
    {
        var identity = new TransactionReviewIdentity
        {
            Algorithm = "Algorithm",
            ChangeSetDigest = "ChangeSetDigest",
            WorkspaceId = workspaceId,
            WorkspaceEpoch = 1,
            TransactionId = 1,
            SnapshotId = Guid.Parse("22222222-2222-2222-2222-222222222222"),
            TransactionRevision = revision,
        };

        return new TransactionReviewOutcome
        {
            Identity = identity,
            Transaction = new TransactionInfo { Revision = revision },
        };
    }
}
