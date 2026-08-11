namespace Roslyn.Workbench.Mcp.Test.ErrorReporting.Retention;

public sealed class BoundedExpiringStoreTests
{
    private readonly DateTimeOffset _now = DateTimeOffset.Parse(
        "2000-01-01T00:00:00Z",
        CultureInfo.InvariantCulture);
    private readonly Mock<IBoundedExpiringStorePolicy<Guid, CapturedErrorRecord>> _policy;
    private readonly Mock<TimeProvider> _timeProvider;
    private readonly Mock<ITimer> _expirationTimer;
    private TimerCallback? _timerCallback;

    public BoundedExpiringStoreTests()
    {
        _policy = new Mock<IBoundedExpiringStorePolicy<Guid, CapturedErrorRecord>>();
        _timeProvider = new Mock<TimeProvider>();
        _expirationTimer = new Mock<ITimer>();
        _policy.SetupGet(item => item.Capacity).Returns(10);
        _policy
            .Setup(item => item.GetExpiration(It.IsAny<CapturedErrorRecord>()))
            .Returns((CapturedErrorRecord record) => record.ExpiresAt);
        _timeProvider.Setup(item => item.GetUtcNow()).Returns(_now);
        _timeProvider
            .Setup(item => item.CreateTimer(
                It.IsAny<TimerCallback>(),
                It.IsAny<object?>(),
                Timeout.InfiniteTimeSpan,
                Timeout.InfiniteTimeSpan))
            .Callback<TimerCallback, object?, TimeSpan, TimeSpan>((callback, _, _, _) =>
                _timerCallback = callback)
            .Returns(_expirationTimer.Object);
        _expirationTimer
            .Setup(item => item.Change(It.IsAny<TimeSpan>(), Timeout.InfiniteTimeSpan))
            .Returns(true);
        _expirationTimer
            .Setup(item => item.DisposeAsync())
            .Returns(ValueTask.CompletedTask);
    }

    [Fact]
    public void GIVEN_StoreHasCapacity_WHEN_AddingAndReading_THEN_ShouldReturnEntry()
    {
        using var target = CreateTarget();
        var record = CreateRecord(
            Guid.Parse("11111111-1111-1111-1111-111111111111"),
            _now.AddHours(1));

        target.TryAdd(record.CorrelationId, record).Should().BeTrue();
        var wasFound = target.TryGet(record.CorrelationId, out var result);

        wasFound.Should().BeTrue();
        result.Should().BeSameAs(record);
    }

    [Fact]
    public void GIVEN_ExistingKey_WHEN_AddingAgain_THEN_ShouldRejectDuplicate()
    {
        using var target = CreateTarget();
        var record = CreateRecord(
            Guid.Parse("11111111-1111-1111-1111-111111111111"),
            _now.AddHours(1));
        target.TryAdd(record.CorrelationId, record).Should().BeTrue();

        target.TryAdd(record.CorrelationId, record).Should().BeFalse();
    }

    [Fact]
    public void GIVEN_ExistingKey_WHEN_AddingOrReplacing_THEN_ShouldReplaceEntry()
    {
        using var target = CreateTarget();
        var correlationId = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var original = CreateRecord(correlationId, _now.AddHours(1));
        var replacement = CreateRecord(correlationId, _now.AddHours(2));
        target.AddOrReplace(correlationId, original);

        target.AddOrReplace(correlationId, replacement);

        target.TryGet(correlationId, out var result).Should().BeTrue();
        result.Should().BeSameAs(replacement);
    }

    [Fact]
    public void GIVEN_StoreAtCapacityAndPolicyRejects_WHEN_Adding_THEN_ShouldRejectEntry()
    {
        using var target = CreateTarget(capacity: 1);
        var existing = CreateRecord(
            Guid.Parse("11111111-1111-1111-1111-111111111111"),
            _now.AddHours(1));
        var additional = CreateRecord(
            Guid.Parse("22222222-2222-2222-2222-222222222222"),
            _now.AddHours(1));
        target.TryAdd(existing.CorrelationId, existing).Should().BeTrue();

        target.TryAdd(additional.CorrelationId, additional).Should().BeFalse();
    }

    [Fact]
    public void GIVEN_StoreAtCapacityAndPolicyRejects_WHEN_AddingOrReplacing_THEN_ShouldRejectInvariant()
    {
        using var target = CreateTarget(capacity: 1);
        var existing = CreateRecord(
            Guid.Parse("11111111-1111-1111-1111-111111111111"),
            _now.AddHours(1));
        var additional = CreateRecord(
            Guid.Parse("22222222-2222-2222-2222-222222222222"),
            _now.AddHours(1));
        target.TryAdd(existing.CorrelationId, existing).Should().BeTrue();

        var action = () => target.AddOrReplace(additional.CorrelationId, additional);

        action.Should().Throw<InvalidOperationException>()
            .WithMessage("The bounded expiring store capacity policy rejected an add-or-replace operation.");
        _expirationTimer.Verify(
            item => item.Change(TimeSpan.FromHours(1), Timeout.InfiniteTimeSpan),
            Times.Exactly(2));
    }

    [Fact]
    public void GIVEN_StoreAtCapacityAndPolicySelectsEntry_WHEN_Adding_THEN_ShouldEvictSelectedEntry()
    {
        using var target = CreateTarget(capacity: 1);
        var existing = CreateRecord(
            Guid.Parse("11111111-1111-1111-1111-111111111111"),
            _now.AddHours(1));
        var additional = CreateRecord(
            Guid.Parse("22222222-2222-2222-2222-222222222222"),
            _now.AddHours(1));
        var evictionKey = existing.CorrelationId;
        _policy
            .Setup(item => item.TrySelectEvictionKey(
                It.IsAny<IReadOnlyDictionary<Guid, CapturedErrorRecord>>(),
                out evictionKey))
            .Returns(true);
        target.TryAdd(existing.CorrelationId, existing).Should().BeTrue();

        target.TryAdd(additional.CorrelationId, additional).Should().BeTrue();

        target.TryGet(existing.CorrelationId, out _).Should().BeFalse();
        target.TryGet(additional.CorrelationId, out _).Should().BeTrue();
    }

    [Fact]
    public void GIVEN_PolicySelectsMissingEntry_WHEN_AddingAtCapacity_THEN_ShouldRejectEntry()
    {
        using var target = CreateTarget(capacity: 1);
        var existing = CreateRecord(
            Guid.Parse("11111111-1111-1111-1111-111111111111"),
            _now.AddHours(1));
        var additional = CreateRecord(
            Guid.Parse("22222222-2222-2222-2222-222222222222"),
            _now.AddHours(1));
        var evictionKey = Guid.Parse("33333333-3333-3333-3333-333333333333");
        _policy
            .Setup(item => item.TrySelectEvictionKey(
                It.IsAny<IReadOnlyDictionary<Guid, CapturedErrorRecord>>(),
                out evictionKey))
            .Returns(true);
        target.TryAdd(existing.CorrelationId, existing).Should().BeTrue();

        target.TryAdd(additional.CorrelationId, additional).Should().BeFalse();
    }

    [Fact]
    public void GIVEN_Entry_WHEN_Updating_THEN_ShouldReturnOriginalAndUpdatedValues()
    {
        using var target = CreateTarget();
        var original = CreateRecord(
            Guid.Parse("11111111-1111-1111-1111-111111111111"),
            _now.AddHours(1));
        var replacement = CreateRecord(original.CorrelationId, _now.AddHours(2));
        target.TryAdd(original.CorrelationId, original).Should().BeTrue();

        var result = target.Update(original.CorrelationId, _ => replacement);

        result.WasFound.Should().BeTrue();
        result.OriginalValue.Should().BeSameAs(original);
        result.UpdatedValue.Should().BeSameAs(replacement);
    }

    [Fact]
    public void GIVEN_UnknownKey_WHEN_Updating_THEN_ShouldReturnNotFound()
    {
        using var target = CreateTarget();

        var result = target.Update(
            Guid.Parse("11111111-1111-1111-1111-111111111111"),
            static record => record);

        result.WasFound.Should().BeFalse();
        result.OriginalValue.Should().BeNull();
        result.UpdatedValue.Should().BeNull();
    }

    [Fact]
    public void GIVEN_Entry_WHEN_Removing_THEN_ShouldReturnRemovalResult()
    {
        using var target = CreateTarget();
        var record = CreateRecord(
            Guid.Parse("11111111-1111-1111-1111-111111111111"),
            _now.AddHours(1));
        target.TryAdd(record.CorrelationId, record).Should().BeTrue();

        target.Remove(record.CorrelationId).Should().BeTrue();
        target.Remove(record.CorrelationId).Should().BeFalse();
    }

    [Fact]
    public void GIVEN_ExpiredEntry_WHEN_Reading_THEN_ShouldRemoveEntryLazily()
    {
        using var target = CreateTarget();
        var record = CreateRecord(
            Guid.Parse("11111111-1111-1111-1111-111111111111"),
            _now.AddHours(1));
        target.TryAdd(record.CorrelationId, record).Should().BeTrue();
        _timeProvider.Setup(item => item.GetUtcNow()).Returns(record.ExpiresAt);

        target.TryGet(record.CorrelationId, out _).Should().BeFalse();
    }

    [Fact]
    public void GIVEN_EntriesExpireAtDifferentTimes_WHEN_TimerFires_THEN_ShouldRemoveExpiredAndReschedule()
    {
        using var target = CreateTarget();
        var expired = CreateRecord(
            Guid.Parse("11111111-1111-1111-1111-111111111111"),
            _now.AddHours(1));
        var remaining = CreateRecord(
            Guid.Parse("22222222-2222-2222-2222-222222222222"),
            _now.AddMinutes(90));
        target.TryAdd(expired.CorrelationId, expired).Should().BeTrue();
        target.TryAdd(remaining.CorrelationId, remaining).Should().BeTrue();
        _timeProvider.Setup(item => item.GetUtcNow()).Returns(expired.ExpiresAt);

        InvokeTimerCallback();

        _expirationTimer.Verify(
            item => item.Change(TimeSpan.FromMinutes(30), Timeout.InfiniteTimeSpan),
            Times.Once);
        _timeProvider.Setup(item => item.GetUtcNow()).Returns(_now);
        target.TryGet(expired.CorrelationId, out _).Should().BeFalse();
        target.TryGet(remaining.CorrelationId, out _).Should().BeTrue();
    }

    [Fact]
    public void GIVEN_LastEntryExpires_WHEN_TimerFires_THEN_ShouldDisableTimer()
    {
        using var target = CreateTarget();
        var record = CreateRecord(
            Guid.Parse("11111111-1111-1111-1111-111111111111"),
            _now.AddHours(1));
        target.TryAdd(record.CorrelationId, record).Should().BeTrue();
        _timeProvider.Setup(item => item.GetUtcNow()).Returns(record.ExpiresAt);

        InvokeTimerCallback();

        _expirationTimer.Verify(
            item => item.Change(Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan),
            Times.Once);
    }

    [Fact]
    public void GIVEN_EntryAlreadyExpired_WHEN_Added_THEN_ShouldScheduleImmediateCleanup()
    {
        using var target = CreateTarget();
        var record = CreateRecord(
            Guid.Parse("11111111-1111-1111-1111-111111111111"),
            _now);

        target.TryAdd(record.CorrelationId, record).Should().BeTrue();

        _expirationTimer.Verify(
            item => item.Change(TimeSpan.Zero, Timeout.InfiniteTimeSpan),
            Times.Once);
    }

    [Fact]
    public void GIVEN_EarlierEntryIsAddedLast_WHEN_Scheduling_THEN_ShouldUseEarlierExpiration()
    {
        using var target = CreateTarget();
        var later = CreateRecord(
            Guid.Parse("11111111-1111-1111-1111-111111111111"),
            _now.AddHours(2));
        var earlier = CreateRecord(
            Guid.Parse("22222222-2222-2222-2222-222222222222"),
            _now.AddHours(1));
        target.TryAdd(later.CorrelationId, later).Should().BeTrue();

        target.TryAdd(earlier.CorrelationId, earlier).Should().BeTrue();

        _expirationTimer.Verify(
            item => item.Change(TimeSpan.FromHours(1), Timeout.InfiniteTimeSpan),
            Times.Once);
    }

    [Fact]
    public void GIVEN_Store_WHEN_DisposedRepeatedly_THEN_ShouldDisposeTimerOnce()
    {
        var target = CreateTarget();

        target.Dispose();
        target.Dispose();

        _expirationTimer.Verify(item => item.Dispose(), Times.Once);
        _expirationTimer.Verify(item => item.DisposeAsync(), Times.Never);
        InvokeTimerCallback();
        _expirationTimer.Verify(
            item => item.Change(It.IsAny<TimeSpan>(), It.IsAny<TimeSpan>()),
            Times.Never);
    }

    [Fact]
    public async Task GIVEN_Store_WHEN_DisposedAsynchronouslyRepeatedly_THEN_ShouldDisposeTimerOnce()
    {
        var target = CreateTarget();

        await target.DisposeAsync();
        await target.DisposeAsync();

        _expirationTimer.Verify(item => item.DisposeAsync(), Times.Once);
        _expirationTimer.Verify(item => item.Dispose(), Times.Never);
        InvokeTimerCallback();
        _expirationTimer.Verify(
            item => item.Change(It.IsAny<TimeSpan>(), It.IsAny<TimeSpan>()),
            Times.Never);
    }

    private BoundedExpiringStore<Guid, CapturedErrorRecord> CreateTarget(int capacity = 10)
    {
        _policy.SetupGet(item => item.Capacity).Returns(capacity);
        return new BoundedExpiringStore<Guid, CapturedErrorRecord>(
            _policy.Object,
            _timeProvider.Object);
    }

    private void InvokeTimerCallback()
    {
        if (_timerCallback is null)
        {
            throw new InvalidOperationException("The expiration timer callback was not registered.");
        }

        _timerCallback(null);
    }

    private CapturedErrorRecord CreateRecord(
        Guid correlationId,
        DateTimeOffset expiresAt)
    {
        return new CapturedErrorRecord
        {
            CorrelationId = correlationId,
            FailureTime = _now,
            ExpiresAt = expiresAt,
            ToolName = "ToolName",
            ExecutionFamily = "ExecutionFamily",
            PluginClassification = "PluginClassification",
            DurationMilliseconds = 10,
            ServerVersion = "ServerVersion",
            RoslynVersion = "RoslynVersion",
            DotNetVersion = "DotNetVersion",
            OperatingSystem = "OperatingSystem",
            ProcessorArchitecture = "ProcessorArchitecture",
        };
    }
}
