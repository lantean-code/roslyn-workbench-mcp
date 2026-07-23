using Roslyn.Workbench.Mcp.ScenarioRunner;

using var cancellationSource = new CancellationTokenSource();
Console.CancelKeyPress += (_, eventArgs) =>
{
    eventArgs.Cancel = true;
    cancellationSource.Cancel();
};

return await ScenarioApplication.RunAsync(args, cancellationSource.Token);
