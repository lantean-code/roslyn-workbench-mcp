namespace Roslyn.Workbench.Mcp.ScenarioRunner;

internal static class GitCommand
{
    public static Task<ExternalCommandResult> RunAsync(
        IReadOnlyList<string> arguments,
        string workingDirectory,
        CancellationToken cancellationToken)
    {
        return ExternalCommand.RunAsync(
            "git",
            ConfigureArguments(arguments),
            workingDirectory,
            cancellationToken);
    }

    public static IReadOnlyList<string> ConfigureArguments(IReadOnlyList<string> arguments)
    {
        return ["-c", "core.longpaths=true", .. arguments];
    }
}
