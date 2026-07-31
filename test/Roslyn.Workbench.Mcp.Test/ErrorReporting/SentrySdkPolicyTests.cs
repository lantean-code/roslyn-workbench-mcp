namespace Roslyn.Workbench.Mcp.Test.ErrorReporting;

public sealed class SentrySdkPolicyTests
{
    [Fact]
    public void GIVEN_ValidSentryDsn_WHEN_CreatingConfiguration_THEN_ShouldDeriveNonSensitiveDestination()
    {
        const string dsn = "https://0123456789abcdef0123456789abcdef@o100000.ingest.us.sentry.io/1000000000000000";

        var result = SentrySdkPolicy.CreateConfiguration(dsn);

        result.Dsn.Should().Be(dsn);
        result.Destination.Should().Be("Sentry project 1000000000000000 at o100000.ingest.us.sentry.io");
    }

    [Theory]
    [InlineData("not-a-uri")]
    [InlineData("http://0123456789abcdef@o100000.ingest.us.sentry.io/1000000000000000")]
    [InlineData("https://o100000.ingest.us.sentry.io/1000000000000000")]
    [InlineData("https://0123456789abcdef@/1000000000000000")]
    public void GIVEN_InvalidSentryEndpoint_WHEN_CreatingConfiguration_THEN_ShouldRejectBuildValue(string dsn)
    {
        var action = () => SentrySdkPolicy.CreateConfiguration(dsn);

        action.Should().Throw<InvalidOperationException>()
            .WithMessage("The build-embedded Sentry DSN must be an absolute HTTPS Sentry DSN with a public key and host.");
    }

    [Theory]
    [InlineData("https://0123456789abcdef@o100000.ingest.us.sentry.io")]
    [InlineData("https://0123456789abcdef@o100000.ingest.us.sentry.io/one/two")]
    public void GIVEN_InvalidSentryProjectPath_WHEN_CreatingConfiguration_THEN_ShouldRejectBuildValue(string dsn)
    {
        var action = () => SentrySdkPolicy.CreateConfiguration(dsn);

        action.Should().Throw<InvalidOperationException>()
            .WithMessage("The build-embedded Sentry DSN must identify exactly one Sentry project.");
    }
}
