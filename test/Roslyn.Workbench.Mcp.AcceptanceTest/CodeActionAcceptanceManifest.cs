namespace Roslyn.Workbench.Mcp.AcceptanceTest;

internal static class CodeActionAcceptanceManifest
{
    public static string[] LoadToolNames()
    {
        var path = Path.Combine(
            AppContext.BaseDirectory,
            "TestAssets",
            "CodeActionAcceptanceToolNames.txt");

        return File.ReadAllLines(path)
            .Where(static line => !string.IsNullOrWhiteSpace(line))
            .ToArray();
    }
}
