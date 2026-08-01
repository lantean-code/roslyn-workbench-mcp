using System.Collections;
using System.Reflection;

namespace Roslyn.Workbench.Mcp.Workspace.Validation;

internal static class ValidationMemberAccess
{
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
