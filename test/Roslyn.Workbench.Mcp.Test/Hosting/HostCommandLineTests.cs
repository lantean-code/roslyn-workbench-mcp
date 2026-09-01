using System.Reflection;

namespace Roslyn.Workbench.Mcp.Test.Hosting;

public sealed class HostCommandLineTests
{
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
