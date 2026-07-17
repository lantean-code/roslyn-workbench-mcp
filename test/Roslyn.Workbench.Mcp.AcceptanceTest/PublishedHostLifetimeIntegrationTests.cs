using System.Diagnostics;

namespace Roslyn.Workbench.Mcp.AcceptanceTest;

public sealed class PublishedHostLifetimeIntegrationTests
{
    private static readonly TimeSpan _exitTimeout = TimeSpan.FromSeconds(10);

    [Fact]
    public async Task GIVEN_PublishedHost_WHEN_StdinReachesEndOfStream_THEN_ShouldExitGracefully()
    {
        var executablePath = PublishedHostExecutable.ResolveFromEnvironment();
        var scenarioRoot = Path.Combine(
            Path.GetTempPath(),
            "roslyn-workbench-mcp-acceptance-lifetime",
            Guid.NewGuid().ToString("N"));
        var workspaceRoot = Path.Combine(scenarioRoot, "workspace");
        var stateRoot = Path.Combine(scenarioRoot, "state");
        Directory.CreateDirectory(workspaceRoot);
        Directory.CreateDirectory(stateRoot);

        using var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = executablePath,
                WorkingDirectory = workspaceRoot,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            },
        };
        process.StartInfo.ArgumentList.Add("--state-directory");
        process.StartInfo.ArgumentList.Add(stateRoot);

        try
        {
            process.Start().Should().BeTrue();
            var standardOutputTask = process.StandardOutput.ReadToEndAsync(TestContext.Current.CancellationToken);
            var standardErrorTask = process.StandardError.ReadToEndAsync(TestContext.Current.CancellationToken);

            process.StandardInput.Close();

            using var timeoutSource = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
            timeoutSource.CancelAfter(_exitTimeout);
            await process.WaitForExitAsync(timeoutSource.Token);
            await standardOutputTask;
            var standardError = await standardErrorTask;

            process.ExitCode.Should().Be(0, $"the published Host should stop gracefully after stdin EOF; stderr: {standardError}");
        }
        finally
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
                await process.WaitForExitAsync(CancellationToken.None);
            }

            Directory.Delete(scenarioRoot, recursive: true);
        }
    }
}
