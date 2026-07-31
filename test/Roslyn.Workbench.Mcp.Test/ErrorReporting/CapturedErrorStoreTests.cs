using Microsoft.Extensions.Options;

namespace Roslyn.Workbench.Mcp.Test.ErrorReporting;

public sealed class CapturedErrorStoreTests
{
    private readonly DateTimeOffset _now = DateTimeOffset.Parse("2000-01-01T00:00:00Z", CultureInfo.InvariantCulture);
    private readonly Mock<TimeProvider> _timeProvider = new Mock<TimeProvider>();

    public CapturedErrorStoreTests()
    {
        _timeProvider.Setup(item => item.GetUtcNow()).Returns(_now);
    }

    [Fact]
    public void GIVEN_RecordWithinLifetime_WHEN_AddingAndReading_THEN_ShouldReturnSameRecord()
    {
        var target = CreateTarget(capacity: 10);
        var record = CreateRecord(Guid.Parse("11111111-1111-1111-1111-111111111111"), _now);

        target.Add(record);
        var found = target.TryGet(record.CorrelationId, out var result);

        found.Should().BeTrue();
        result.Should().BeSameAs(record);
    }

    [Fact]
    public void GIVEN_CapacityReached_WHEN_AddingNewRecord_THEN_ShouldEvictOldestRecord()
    {
        var target = CreateTarget(capacity: 1);
        var oldest = CreateRecord(Guid.Parse("11111111-1111-1111-1111-111111111111"), _now);
        var newest = CreateRecord(Guid.Parse("22222222-2222-2222-2222-222222222222"), _now.AddMinutes(1));

        target.Add(oldest);
        target.Add(newest);

        target.TryGet(oldest.CorrelationId, out _).Should().BeFalse();
        target.TryGet(newest.CorrelationId, out var result).Should().BeTrue();
        result.Should().BeSameAs(newest);
    }

    [Fact]
    public void GIVEN_ExpiredRecord_WHEN_Reading_THEN_ShouldRemoveRecord()
    {
        var target = CreateTarget(capacity: 10);
        var record = CreateRecord(Guid.Parse("11111111-1111-1111-1111-111111111111"), _now);
        target.Add(record);
        _timeProvider.Setup(item => item.GetUtcNow()).Returns(record.ExpiresAt);

        var found = target.TryGet(record.CorrelationId, out _);

        found.Should().BeFalse();
    }

    private CapturedErrorStore CreateTarget(int capacity)
    {
        var options = new ErrorReportingOptions
        {
            CapturedErrorCapacity = capacity,
        };

        return new CapturedErrorStore(Options.Create(options), _timeProvider.Object);
    }

    private static CapturedErrorRecord CreateRecord(Guid correlationId, DateTimeOffset failureTime)
    {
        return new CapturedErrorRecord
        {
            CorrelationId = correlationId,
            FailureTime = failureTime,
            ExpiresAt = failureTime.AddHours(1),
            ToolName = "ToolName",
            ExecutionFamily = "ExecutionFamily",
            PluginClassification = "PluginClassification",
            DurationMilliseconds = 25,
            ServerVersion = "ServerVersion",
            RoslynVersion = "RoslynVersion",
            DotNetVersion = "DotNetVersion",
            OperatingSystem = "OperatingSystem",
            ProcessorArchitecture = "ProcessorArchitecture",
        };
    }
}
