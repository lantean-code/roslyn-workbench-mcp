using System.Diagnostics.CodeAnalysis;

namespace Roslyn.Workbench.Mcp;

/// <summary>
/// Hosts the Roslyn Workbench MCP server process.
/// </summary>
internal static class Program
{
    [SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "The process boundary reports otherwise unhandled failures to stderr and exits with failure, including before logging services are available.")]
    private static async Task<int> Main(string[] args)
    {
        try
        {
            if (HostCommandLine.TryWriteHelp(args, Console.Out)
                || HostCommandLine.TryWriteVersion(args, Console.Out))
            {
                return 0;
            }

            Console.SetOut(Console.Error);

            var builder = Host.CreateApplicationBuilder(args);
            builder.AddRoslynWorkbench(args);

            using var host = builder.Build();
            var stateDirectory = host.Services.GetRequiredService<IWorkspaceStateDirectory>();
            var logger = host.Services.GetRequiredService<ILoggerFactory>().CreateLogger(typeof(HostStartupStorage));
            if (!HostStartupStorage.TryInitialize(stateDirectory, logger, Console.Error))
            {
                return 1;
            }

            await host.RunAsync();
            return 0;
        }
        catch (Exception exception)
        {
            var diagnosticId = Guid.NewGuid();
            await Console.Error.WriteLineAsync($"Roslyn Workbench terminated with an error. Diagnostic ID: {diagnosticId:D}.");
            await Console.Error.WriteLineAsync(exception.ToString());
            return 1;
        }
    }
}
