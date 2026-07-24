namespace Roslyn.Workbench.Mcp.Configuration;

internal static class StartupOptionsRules
{
    public static TimeSpan MaximumCodeActionTokenLifetime { get; } = TimeSpan.FromDays(1);

    public static bool IsPositive(int value)
    {
        return value > 0;
    }

    public static bool IsPositive(TimeSpan value)
    {
        return value > TimeSpan.Zero;
    }

    public static bool IsSupportedCodeActionTokenLifetime(TimeSpan value)
    {
        return IsPositive(value) && value <= MaximumCodeActionTokenLifetime;
    }

    public static bool IsSupported(ToolOutputSchemaMode value)
    {
        return Enum.IsDefined(value);
    }

    public static bool IsValidStateDirectory(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        try
        {
            _ = Path.GetFullPath(value);
            return true;
        }
        catch (Exception exception) when (exception is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return false;
        }
    }

    public static bool AreValidPluginDirectories(IReadOnlyList<string> values)
    {
        return values.All(static value => !string.IsNullOrWhiteSpace(value));
    }
}
