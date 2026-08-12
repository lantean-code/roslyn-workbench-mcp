namespace Roslyn.Workbench.Mcp.Test.ErrorReporting.Capture;

public sealed class CapturedErrorStoreTests
{
    private readonly Mock<IBoundedExpiringStore<Guid, CapturedErrorRecord>> _entries;
    private readonly CapturedErrorStore _target;

    public CapturedErrorStoreTests()
    {
        _entries = new Mock<IBoundedExpiringStore<Guid, CapturedErrorRecord>>();
        _target = new CapturedErrorStore(_entries.Object);
    }

    [Fact]
    public void GIVEN_CapturedError_WHEN_Adding_THEN_ShouldStoreByCorrelationId()
    {
        var record = CreateRecord();

        _target.Add(record);

        _entries.Verify(item => item.AddOrReplace(record.CorrelationId, record), Times.Once);
    }

    [Fact]
    public void GIVEN_CorrelationId_WHEN_Reading_THEN_ShouldReturnStoreResult()
    {
        var record = CreateRecord();
        CapturedErrorRecord? storedRecord = record;
        _entries
            .Setup(item => item.TryGet(record.CorrelationId, out storedRecord))
            .Returns(true);

        var wasFound = _target.TryGet(record.CorrelationId, out var result);

        wasFound.Should().BeTrue();
        result.Should().BeSameAs(record);
    }

    private static CapturedErrorRecord CreateRecord()
    {
        return new CapturedErrorRecord
        {
            CorrelationId = Guid.Parse("11111111-1111-1111-1111-111111111111"),
            FailureTime = DateTimeOffset.Parse("2000-01-01T00:00:00Z", CultureInfo.InvariantCulture),
            ExpiresAt = DateTimeOffset.Parse("2000-01-01T01:00:00Z", CultureInfo.InvariantCulture),
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
