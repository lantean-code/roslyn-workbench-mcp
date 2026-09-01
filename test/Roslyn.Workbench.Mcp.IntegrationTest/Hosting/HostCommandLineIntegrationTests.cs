using System.Diagnostics;
using System.Reflection;

namespace Roslyn.Workbench.Mcp.Test.Hosting;

[Trait("Category", "Integration")]
public sealed class HostCommandLineIntegrationTests
{
    [Fact]
    public async Task GIVEN_VersionArgument_WHEN_RunningHostProcess_THEN_ShouldWriteOnlyExactInformationalVersion()
    {
        var hostAssembly = typeof(HostCommandLine).Assembly;
        var expectedVersion = hostAssembly
            .GetCustomAttributes<AssemblyInformationalVersionAttribute>()
            .Single()
            .InformationalVersion;
        var runtimeConfiguration = Path.ChangeExtension(typeof(HostCommandLineIntegrationTests).Assembly.Location, ".runtimeconfig.json");
        var startInfo = new ProcessStartInfo("dotnet")
        {
            RedirectStandardError = true,
            RedirectStandardOutput = true,
            UseShellExecute = false,
        };

        startInfo.ArgumentList.Add("exec");
        startInfo.ArgumentList.Add("--runtimeconfig");
        startInfo.ArgumentList.Add(runtimeConfiguration);
        startInfo.ArgumentList.Add(hostAssembly.Location);
        startInfo.ArgumentList.Add("--version");

        using var process = new Process
        {
            StartInfo = startInfo,
        };

        process.Start().Should().BeTrue();
        var standardOutput = process.StandardOutput.ReadToEndAsync(TestContext.Current.CancellationToken);
        var standardError = process.StandardError.ReadToEndAsync(TestContext.Current.CancellationToken);

        await process.WaitForExitAsync(TestContext.Current.CancellationToken);

        process.ExitCode.Should().Be(0);
        (await standardOutput).Should().Be(expectedVersion + Environment.NewLine);
        (await standardError).Should().BeEmpty();
    }
}
