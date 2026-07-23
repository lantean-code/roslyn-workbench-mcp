using Roslyn.Workbench.Mcp.ScenarioRunner.Application;

namespace Roslyn.Workbench.Mcp.ScenarioRunner;

internal static class Program
{
    public static async Task<int> Main(string[] args)
    {
        using var cancellationSource = new CancellationTokenSource();
        Console.CancelKeyPress += (_, eventArgs) =>
        {
            eventArgs.Cancel = true;
            cancellationSource.Cancel();
        };

        return await ScenarioApplication.RunAsync(args, cancellationSource.Token);
    }
}
