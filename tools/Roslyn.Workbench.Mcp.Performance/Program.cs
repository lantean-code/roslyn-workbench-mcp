using Roslyn.Workbench.Mcp.Performance;

using var cancellationSource = new CancellationTokenSource();
Console.CancelKeyPress += (_, eventArgs) =>
{
    eventArgs.Cancel = true;
    cancellationSource.Cancel();
};

return await PerformanceApplication.RunAsync(args, cancellationSource.Token);
