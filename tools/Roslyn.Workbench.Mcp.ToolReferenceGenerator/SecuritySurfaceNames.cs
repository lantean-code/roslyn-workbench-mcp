using System.Text;

namespace Roslyn.Workbench.Mcp.ToolReferenceGenerator;

/// <summary>
/// Produces stable public names for typed security-surface values.
/// </summary>
internal static class SecuritySurfaceNames
{
    /// <summary>
    /// Converts an enum member name to lower-case kebab form.
    /// </summary>
    /// <typeparam name="TEnum">The enum type.</typeparam>
    /// <param name="value">The enum value.</param>
    /// <returns>The stable surface name.</returns>
    public static string GetEnumName<TEnum>(TEnum value)
        where TEnum : struct, Enum
    {
        return GetEnumName((Enum)(object)value);
    }

    /// <summary>
    /// Converts a runtime enum value to lower-case kebab form.
    /// </summary>
    /// <param name="value">The enum value.</param>
    /// <returns>The stable surface name.</returns>
    public static string GetEnumName(Enum value)
    {
        var source = value.ToString();
        var result = new StringBuilder(source.Length);
        for (var index = 0; index < source.Length; index++)
        {
            var character = source[index];
            if (index > 0 && char.IsUpper(character))
            {
                result.Append('-');
            }

            result.Append(char.ToLowerInvariant(character));
        }

        return result.ToString();
    }
}
