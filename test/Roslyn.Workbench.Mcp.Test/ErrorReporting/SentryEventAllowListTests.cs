using System.Text;
using System.Text.Json;
using Sentry;

namespace Roslyn.Workbench.Mcp.Test.ErrorReporting;

public sealed class SentryEventAllowListTests
{
    [Fact]
    public void GIVEN_SdkEnrichedEvent_WHEN_CreatingAllowedCopy_THEN_ShouldRetainOnlyReviewedFields()
    {
        var source = CreateEvent();
        source.ServerName = "private-workstation";
        source.Release = "Private.Company.Product@1.0.0";
        source.Modules["Private.Company.Plugin"] = "1.0.0";
        source.Contexts["runtime"] = new { Name = ".NET", Version = "10.0.0" };
        source.SetExtra("sourcePath", @"C:\Users\developer\source\PrivatePlugin.cs");
        source.SetTag("privateTag", "privateValue");
        source.User = new SentryUser { Id = "private-user" };
        source.Request = new SentryRequest { Url = "https://private.example.test/report" };
        source.AddBreadcrumb("private breadcrumb");
        var result = SentryEventAllowList.CreateAllowedCopy(source);

        result.EventId.Should().Be(source.EventId);
        result.Timestamp.Should().Be(source.Timestamp);
        var bytes = SentryEventJsonSerializer.Serialize(result);
        using var document = JsonDocument.Parse(bytes.AsMemory());
        var root = document.RootElement;
        root.EnumerateObject().Select(static property => property.Name).Should().BeEquivalentTo(
            "event_id",
            "timestamp",
            "platform",
            "level",
            "logger",
            "fingerprint",
            "logentry",
            "sdk",
            "contexts");
        var sdk = root.GetProperty("sdk");
        sdk.GetProperty("name").GetString().Should().Be("sentry.dotnet");
        sdk.TryGetProperty("packages", out _).Should().BeFalse();
        root.GetProperty("contexts").EnumerateObject().Select(static property => property.Name).Should().Equal(
            "roslyn_workbench");
        var json = Encoding.UTF8.GetString(bytes.AsSpan());
        json.Should().NotContain("private-workstation");
        json.Should().NotContain("Private.Company");
        json.Should().NotContain("PrivatePlugin.cs");
        json.Should().NotContain("private-user");
        json.Should().NotContain("private.example.test");
        json.Should().NotContain("private breadcrumb");
    }

    [Fact]
    public void GIVEN_EventWithoutWorkbenchContext_WHEN_CreatingAllowedCopy_THEN_ShouldOmitContexts()
    {
        var source = CreateEvent();
        source.Contexts.Remove("roslyn_workbench");
        var result = SentryEventAllowList.CreateAllowedCopy(source);

        var bytes = SentryEventJsonSerializer.Serialize(result);
        using var document = JsonDocument.Parse(bytes.AsMemory());
        document.RootElement.TryGetProperty("contexts", out _).Should().BeFalse();
    }

    private static SentryEvent CreateEvent()
    {
        var sentryEvent = new SentryEvent
        {
            Platform = "csharp",
            Level = SentryLevel.Error,
            Logger = "roslyn-workbench-mcp",
            Fingerprint = ["roslyn-workbench"],
            Message = new SentryMessage
            {
                Message = "Message",
                Params = ["Param"],
                Formatted = "Formatted",
            },
        };
        sentryEvent.Contexts["roslyn_workbench"] = JsonSerializer.SerializeToElement(new { reportId = "ReportId" });
        return sentryEvent;
    }
}
