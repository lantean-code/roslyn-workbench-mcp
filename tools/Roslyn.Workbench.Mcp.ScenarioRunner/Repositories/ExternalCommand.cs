using System.Diagnostics;

namespace Roslyn.Workbench.Mcp.ScenarioRunner.Repositories;

internal static class ExternalCommand
{
    public static async Task<ExternalCommandResult> RunAsync(
        string fileName,
        IReadOnlyList<string> arguments,
        string workingDirectory,
        CancellationToken cancellationToken,
        IReadOnlyDictionary<string, string>? environment = null)
    {
        var startInfo = CreateStartInfo(fileName, arguments, workingDirectory, environment);
        using var process = new Process { StartInfo = startInfo };

        if (!process.Start())
        {
            throw new InvalidOperationException($"Unable to start '{fileName}'.");
        }

        var standardOutput = process.StandardOutput.ReadToEndAsync(cancellationToken);
        var standardError = process.StandardError.ReadToEndAsync(cancellationToken);
        try
        {
            await process.WaitForExitAsync(cancellationToken);
        }
        catch
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
                await process.WaitForExitAsync();
            }

            throw;
        }

        return new ExternalCommandResult
        {
            ExitCode = process.ExitCode,
            StandardOutput = await standardOutput,
            StandardError = await standardError,
        };
    }

    public static Process Start(
        string fileName,
        IReadOnlyList<string> arguments,
        string workingDirectory,
        IReadOnlyDictionary<string, string>? environment = null)
    {
        var process = new Process
        {
            StartInfo = CreateStartInfo(fileName, arguments, workingDirectory, environment),
        };

        if (!process.Start())
        {
            process.Dispose();
            throw new InvalidOperationException($"Unable to start '{fileName}'.");
        }

        return process;
    }

    private static ProcessStartInfo CreateStartInfo(
        string fileName,
        IReadOnlyList<string> arguments,
        string workingDirectory,
        IReadOnlyDictionary<string, string>? environment)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = fileName,
            WorkingDirectory = workingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };

        foreach (var argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        if (environment is not null)
        {
            foreach (var (name, value) in environment)
            {
                startInfo.Environment[name] = value;
            }
        }

        return startInfo;
    }
}
