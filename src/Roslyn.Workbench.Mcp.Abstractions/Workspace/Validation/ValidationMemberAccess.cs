using System.Collections;
using System.Reflection;

namespace Roslyn.Workbench.Mcp.Workspace.Validation;

/// <summary>
/// Provides the shared reflection and presence checks used by cross-property validation attributes.
/// </summary>
internal static class ValidationMemberAccess
{
    /// <summary>
    /// Reads a named public property from a validation target.
    /// </summary>
    /// <param name="instance">The object being validated.</param>
    /// <param name="memberName">The public property name.</param>
    /// <returns>The property's current value.</returns>
    public static object? GetValue(object instance, string memberName)
    {
        var property = instance.GetType().GetProperty(memberName, BindingFlags.Instance | BindingFlags.Public);
        if (property is null || property.GetMethod is null)
        {
            throw new InvalidOperationException(
                $"Validation member '{instance.GetType().FullName}.{memberName}' is not a readable public property.");
        }

        return property.GetValue(instance);
    }

    /// <summary>
    /// Determines whether a value supplies meaningful input for conditional validation.
    /// </summary>
    /// <param name="value">The value to inspect.</param>
    /// <returns><see langword="true"/> for non-blank text, non-empty sequences and other non-null values; otherwise <see langword="false"/>.</returns>
    public static bool IsProvided(object? value)
    {
        if (value is null)
        {
            return false;
        }

        if (value is string text)
        {
            return !string.IsNullOrWhiteSpace(text);
        }

        if (value is not IEnumerable values)
        {
            return true;
        }

        var enumerator = values.GetEnumerator();
        try
        {
            return enumerator.MoveNext();
        }
        finally
        {
            (enumerator as IDisposable)?.Dispose();
        }
    }

    /// <summary>
    /// Validates and copies a set of unique member names so attribute configuration cannot change after construction.
    /// </summary>
    /// <param name="memberNames">The member names supplied to a validation attribute.</param>
    /// <param name="parameterName">The constructor parameter name used in configuration exceptions.</param>
    /// <param name="minimumCount">The minimum number of member names required by the attribute.</param>
    /// <returns>An independent copy of the validated member names.</returns>
    public static string[] CaptureMemberNames(string[] memberNames, string parameterName, int minimumCount)
    {
        if (memberNames.Length < minimumCount)
        {
            throw new ArgumentException(
                $"At least {minimumCount} member names must be provided.",
                parameterName);
        }

        var capturedMemberNames = new string[memberNames.Length];
        var uniqueMemberNames = new HashSet<string>(StringComparer.Ordinal);
        for (var index = 0; index < memberNames.Length; index++)
        {
            var memberName = memberNames[index];
            ArgumentException.ThrowIfNullOrWhiteSpace(memberName, parameterName);

            if (!uniqueMemberNames.Add(memberName))
            {
                throw new ArgumentException($"Member name '{memberName}' is duplicated.", parameterName);
            }

            capturedMemberNames[index] = memberName;
        }

        return capturedMemberNames;
    }
}
