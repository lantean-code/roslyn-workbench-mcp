namespace Roslyn.Workbench.Mcp;

internal static class Program
{
    private static async Task Main(string[] args)
    {
        var builder = Host.CreateApplicationBuilder(args);
        builder.AddRoslynWorkbench(args);

        using var host = builder.Build();
        await host.RunAsync();
    }
}
