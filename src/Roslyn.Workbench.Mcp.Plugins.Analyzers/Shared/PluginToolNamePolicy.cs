namespace Roslyn.Workbench.Mcp.Plugins.Validation;

/// <summary>
/// Defines the MCP-compatible character and length rules applied to plugin tool names.
/// </summary>
internal static class PluginToolNamePolicy
{
    /// <summary>
    /// The maximum number of characters permitted in a plugin tool name.
    /// </summary>
    public const int MaximumLength = 128;

    /// <summary>
    /// Determines whether a tool name is non-empty, within the protocol length bound and contains only supported ASCII characters.
    /// </summary>
    /// <param name="name">The proposed tool name.</param>
    /// <returns><see langword="true"/> when the name satisfies the protocol policy; otherwise <see langword="false"/>.</returns>
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
