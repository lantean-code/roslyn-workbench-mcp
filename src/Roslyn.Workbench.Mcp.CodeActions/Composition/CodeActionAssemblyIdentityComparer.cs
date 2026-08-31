using System.Reflection;
using System.Runtime.CompilerServices;

namespace Roslyn.Workbench.Mcp.CodeActions.Composition;

/// <summary>
/// Compares assemblies by case-insensitive full assembly identity.
/// </summary>
internal sealed class CodeActionAssemblyIdentityComparer : IEqualityComparer<Assembly>
{
    /// <summary>
    /// Gets the shared comparer instance.
    /// </summary>
    public static CodeActionAssemblyIdentityComparer Instance { get; } = new();

    /// <summary>
    /// Determines whether two assemblies have the same full identity.
    /// </summary>
    /// <param name="first">The first assembly identity to compare.</param>
    /// <param name="second">The second assembly identity to compare.</param>
    /// <returns><see langword="true"/> when both references identify the same assembly; otherwise, <see langword="false"/>.</returns>
    public bool Equals(Assembly? first, Assembly? second)
    {
        if (ReferenceEquals(first, second))
        {
            return true;
        }

        if (first is null || second is null)
        {
            return false;
        }

        var firstIdentity = first.FullName;
        var secondIdentity = second.FullName;
        return firstIdentity is not null
            && secondIdentity is not null
            && string.Equals(firstIdentity, secondIdentity, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Gets a hash code for an assembly's full identity.
    /// </summary>
    /// <param name="assembly">The assembly to hash.</param>
    /// <returns>A hash code compatible with this comparer's equality rules.</returns>
    public int GetHashCode(Assembly assembly)
    {
        if (assembly.FullName is string identity)
        {
            return StringComparer.OrdinalIgnoreCase.GetHashCode(identity);
        }

        return RuntimeHelpers.GetHashCode(assembly);
    }
}
