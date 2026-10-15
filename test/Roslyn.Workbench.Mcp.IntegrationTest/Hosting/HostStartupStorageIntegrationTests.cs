using System.Diagnostics;
using System.Runtime.Versioning;
using System.Security.AccessControl;
using System.Security.Principal;

namespace Roslyn.Workbench.Mcp.Test.Hosting;

[Trait("Category", "Integration")]
public sealed class HostStartupStorageIntegrationTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task GIVEN_UnwritableStorage_WHEN_RunningHost_THEN_ShouldFailBeforeTransportWithActionableDiagnostic(bool debugLogging)
    {
        var statePath = Path.Combine(Path.GetTempPath(), $"roslyn-workbench-startup-{Guid.NewGuid():N}");
        var directory = Directory.CreateDirectory(statePath);
        Directory.CreateDirectory(Path.Combine(statePath, "recovery"));
        Action restoreAccess;
        if (OperatingSystem.IsWindows())
        {
            restoreAccess = DenyWindowsWrites(directory);
        }
        else
        {
            restoreAccess = DenyUnixWrites(directory);
        }

        try
        {
            var runtimeConfiguration = Path.ChangeExtension(typeof(HostStartupStorageIntegrationTests).Assembly.Location, ".runtimeconfig.json");
            var startInfo = new ProcessStartInfo("dotnet")
            {
                RedirectStandardError = true,
                RedirectStandardOutput = true,
                UseShellExecute = false,
            };

            startInfo.ArgumentList.Add("exec");
            startInfo.ArgumentList.Add("--runtimeconfig");
            startInfo.ArgumentList.Add(runtimeConfiguration);
            startInfo.ArgumentList.Add(typeof(HostCommandLine).Assembly.Location);
            startInfo.ArgumentList.Add("--state-directory");
            startInfo.ArgumentList.Add(statePath);
            startInfo.Environment["Logging__LogLevel__Default"] = debugLogging ? "Debug" : "Information";

            using var process = new Process
            {
                StartInfo = startInfo,
            };

            process.Start().Should().BeTrue();
            var standardOutput = process.StandardOutput.ReadToEndAsync(TestContext.Current.CancellationToken);
            var standardError = process.StandardError.ReadToEndAsync(TestContext.Current.CancellationToken);

            await process.WaitForExitAsync(TestContext.Current.CancellationToken);
            var output = await standardOutput;
            var error = await standardError;

            process.ExitCode.Should().Be(1);
            output.Should().BeEmpty();
            error.Should().Contain("Cannot write durable recovery storage")
                .And.Contain(statePath)
                .And.Contain("--state-directory")
                .And.NotContain("Hosting failed to start")
                .And.NotContain("Unhandled exception")
                .And.NotContain("transport reading messages");
            if (debugLogging)
            {
                error.Should().Contain("UnauthorizedAccessException");
            }
            else
            {
                error.Should().NotContain("Exception").And.NotContain("   at ");
            }
        }
        finally
        {
            restoreAccess();
            Directory.Delete(statePath, recursive: true);
        }
    }

    [SupportedOSPlatform("windows")]
    private static Action DenyWindowsWrites(DirectoryInfo directory)
    {
        var originalSecurity = directory.GetAccessControl();
        var deniedSecurity = directory.GetAccessControl();
        using var identity = WindowsIdentity.GetCurrent();
        var user = identity.User ?? throw new InvalidOperationException("The current Windows user has no security identifier.");
        var rule = new FileSystemAccessRule(
            user,
            FileSystemRights.Write,
            InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit,
            PropagationFlags.None,
            AccessControlType.Deny);
        deniedSecurity.AddAccessRule(rule);
        directory.SetAccessControl(deniedSecurity);
        return () => directory.SetAccessControl(originalSecurity);
    }

    [UnsupportedOSPlatform("windows")]
    private static Action DenyUnixWrites(DirectoryInfo directory)
    {
        var originalMode = File.GetUnixFileMode(directory.FullName);
        File.SetUnixFileMode(directory.FullName, UnixFileMode.UserRead | UnixFileMode.UserExecute);
        return () => File.SetUnixFileMode(directory.FullName, originalMode);
    }
}
