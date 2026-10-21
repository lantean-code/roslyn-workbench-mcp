using System.Diagnostics;

namespace Roslyn.Workbench.Mcp.AcceptanceTest;

public sealed class FatalHostFailureIntegrationTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task GIVEN_FatalStartupFailure_WHEN_RunningPublishedHost_THEN_ShouldWriteLocalDiagnosticsToStderrAndExitOne(bool loggingFailure)
    {
        var executable = PublishedHostExecutable.ResolveFromEnvironment();
        var startInfo = new ProcessStartInfo(executable)
        {
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        if (loggingFailure)
        {
            startInfo.Environment["Logging__LogLevel__Default"] = "invalid-fatal-probe-level";
        }
        else
        {
            startInfo.ArgumentList.Add("--operational-mode");
            startInfo.ArgumentList.Add("inspection-only");
            startInfo.ArgumentList.Add("--commit-validation");
            startInfo.ArgumentList.Add("no-new-compiler-errors");
        }

        using var process = new Process { StartInfo = startInfo };
        process.Start().Should().BeTrue();
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        cancellation.CancelAfter(TimeSpan.FromSeconds(20));
        var stdout = process.StandardOutput.ReadToEndAsync(cancellation.Token);
        var stderr = process.StandardError.ReadToEndAsync(cancellation.Token);

        try
        {
            await process.WaitForExitAsync(cancellation.Token);
            var output = await stdout;
            var diagnostic = await stderr;

            process.ExitCode.Should().Be(1);
            output.Should().BeEmpty();
            diagnostic.Should().Contain("Roslyn Workbench terminated with an error. Diagnostic ID: ");
            var marker = diagnostic.IndexOf("Diagnostic ID: ", StringComparison.Ordinal) + "Diagnostic ID: ".Length;
            Guid.TryParse(diagnostic.AsSpan(marker, 36), out var id).Should().BeTrue();
            id.Should().NotBe(Guid.Empty);
            if (loggingFailure)
            {
                diagnostic.Should().Contain("invalid-fatal-probe-level");
            }
            else
            {
                diagnostic.Should().Contain("OptionsValidationException");
            }

            diagnostic.Should().Contain("Roslyn.Workbench.Mcp.Program.Main");
        }
        finally
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
                await process.WaitForExitAsync(TestContext.Current.CancellationToken);
            }
        }
    }
}
