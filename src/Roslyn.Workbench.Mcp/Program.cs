using Roslyn.Workbench.Mcp.Hosting;

namespace Roslyn.Workbench.Mcp;

/// <summary>
/// Hosts the Roslyn Workbench MCP server process.
/// </summary>
internal static class Program
{
    private static async Task Main(string[] args)
    {
        if (HostCommandLine.TryWriteVersion(args, Console.Out))
        {
            return;
        }

        Console.SetOut(Console.Error);

        var builder = Host.CreateApplicationBuilder(args);
        builder.AddRoslynWorkbench(args);

        using var host = builder.Build();
        await host.RunAsync();
    }
}
