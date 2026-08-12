using Microsoft.Extensions.Options;

namespace Roslyn.Workbench.Mcp.Test.ErrorReporting.Capture;

public sealed class CapturedErrorRetentionPolicyTests
{
    [Fact]
    public void GIVEN_ConfiguredCapacity_WHEN_CreatingPolicy_THEN_ShouldExposeCapacityAndExpiration()
    {
        var options = new ErrorReportingOptions
        {
            CapturedErrorCapacity = 20,
        };
        var target = new CapturedErrorRetentionPolicy(Options.Create(options));
        var record = CreateRecord(
            Guid.Parse("11111111-1111-1111-1111-111111111111"),
            DateTimeOffset.Parse("2000-01-01T00:00:00Z", CultureInfo.InvariantCulture));

        target.Capacity.Should().Be(20);
        target.GetExpiration(record).Should().Be(record.ExpiresAt);
    }

    [Fact]
    public void GIVEN_EntriesWithDifferentFailureTimes_WHEN_SelectingEviction_THEN_ShouldSelectOldest()
    {
        var target = new CapturedErrorRetentionPolicy(Options.Create(new ErrorReportingOptions()));
        var middle = CreateRecord(
            Guid.Parse("11111111-1111-1111-1111-111111111111"),
            DateTimeOffset.Parse("2000-01-01T00:01:00Z", CultureInfo.InvariantCulture));
        var newest = CreateRecord(
            Guid.Parse("22222222-2222-2222-2222-222222222222"),
            DateTimeOffset.Parse("2000-01-01T00:02:00Z", CultureInfo.InvariantCulture));
        var oldest = CreateRecord(
            Guid.Parse("33333333-3333-3333-3333-333333333333"),
            DateTimeOffset.Parse("2000-01-01T00:00:00Z", CultureInfo.InvariantCulture));
        IReadOnlyDictionary<Guid, CapturedErrorRecord> entries =
            new Dictionary<Guid, CapturedErrorRecord>
            {
                [middle.CorrelationId] = middle,
                [newest.CorrelationId] = newest,
                [oldest.CorrelationId] = oldest,
            };

        var wasSelected = target.TrySelectEvictionKey(entries, out var key);

        wasSelected.Should().BeTrue();
        key.Should().Be(oldest.CorrelationId);
    }

    private static CapturedErrorRecord CreateRecord(
        Guid correlationId,
        DateTimeOffset failureTime)
    {
        return new CapturedErrorRecord
        {
            CorrelationId = correlationId,
            FailureTime = failureTime,
            ExpiresAt = failureTime.AddHours(1),
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
