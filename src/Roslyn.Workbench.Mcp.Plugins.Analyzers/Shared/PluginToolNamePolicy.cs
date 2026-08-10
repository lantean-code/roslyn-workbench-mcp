namespace Roslyn.Workbench.Mcp.Plugins.Validation;

internal static class PluginToolNamePolicy
{
    public const int MaximumLength = 128;

    public static bool IsValid(string? name)
    {
        if (name is null || name.Length == 0 || name.Length > MaximumLength)
        {
            return false;
        }

        foreach (var character in name)
        {
            if (!IsAllowedCharacter(character))
            {
                return false;
            }
        }

        return true;
    }

    private static bool IsAllowedCharacter(char character)
    {
        return character is >= 'A' and <= 'Z'
            or >= 'a' and <= 'z'
            or >= '0' and <= '9'
            or '_'
            or '-'
            or '.';
    }
}
