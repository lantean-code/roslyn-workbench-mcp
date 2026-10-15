using System.Reflection;

namespace Roslyn.Workbench.Mcp.Test.Hosting;

public sealed class HostCommandLineTests
{
    [Theory]
    [InlineData("--help")]
    [InlineData("-h")]
    public void GIVEN_HelpArgument_WHEN_TryingToWriteHelp_THEN_ShouldWriteUsageAndOptions(string argument)
    {
        using var output = new StringWriter();

        var handled = HostCommandLine.TryWriteHelp([argument], output);

        handled.Should().BeTrue();
        output.ToString().Should().Contain("Usage: roslyn-workbench-mcp [options]")
            .And.Contain("--state-directory")
            .And.Contain("--operational-mode")
            .And.Contain("--enable-plugins")
            .And.Contain("--error-reporting-consent");
    }

    [Theory]
    [InlineData("--version")]
    [InlineData("--HELP")]
    public void GIVEN_NoHelpArgument_WHEN_TryingToWriteHelp_THEN_ShouldNotHandleCommand(string argument)
    {
        using var output = new StringWriter();

        var handled = HostCommandLine.TryWriteHelp([argument], output);

        handled.Should().BeFalse();
        output.ToString().Should().BeEmpty();
    }

    [Fact]
    public void GIVEN_HelpWithOtherOptions_WHEN_TryingToWriteHelp_THEN_ShouldHandleCommand()
    {
        using var output = new StringWriter();

        var handled = HostCommandLine.TryWriteHelp(["--operational-mode", "invalid", "--help"], output);

        handled.Should().BeTrue();
        output.ToString().Should().Contain("Usage:");
    }

    [Fact]
    public void GIVEN_NoArguments_WHEN_TryingToWriteVersion_THEN_ShouldNotHandleCommand()
    {
        using var output = new StringWriter();

        var handled = HostCommandLine.TryWriteVersion([], output);

        handled.Should().BeFalse();
        output.ToString().Should().BeEmpty();
    }

    [Fact]
    public void GIVEN_AnotherArgument_WHEN_TryingToWriteVersion_THEN_ShouldNotHandleCommand()
    {
        using var output = new StringWriter();

        var handled = HostCommandLine.TryWriteVersion(["--help"], output);

        handled.Should().BeFalse();
        output.ToString().Should().BeEmpty();
    }

    [Fact]
    public void GIVEN_VersionArgumentWithAnotherArgument_WHEN_TryingToWriteVersion_THEN_ShouldNotHandleCommand()
    {
        using var output = new StringWriter();

        var handled = HostCommandLine.TryWriteVersion(["--version", "--help"], output);

        handled.Should().BeFalse();
        output.ToString().Should().BeEmpty();
    }

    [Fact]
    public void GIVEN_VersionArgument_WHEN_TryingToWriteVersion_THEN_ShouldWriteExactInformationalVersion()
    {
        using var output = new StringWriter();
        var expectedVersion = typeof(HostCommandLine).Assembly
            .GetCustomAttributes<AssemblyInformationalVersionAttribute>()
            .Single()
            .InformationalVersion;

        var handled = HostCommandLine.TryWriteVersion(["--version"], output);

        handled.Should().BeTrue();
        output.ToString().Should().Be(expectedVersion + Environment.NewLine);
    }
}
