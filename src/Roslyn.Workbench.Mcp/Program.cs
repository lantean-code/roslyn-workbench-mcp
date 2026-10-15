namespace Roslyn.Workbench.Mcp;

/// <summary>
/// Hosts the Roslyn Workbench MCP server process.
/// </summary>
internal static class Program
{
    private static async Task<int> Main(string[] args)
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
}
