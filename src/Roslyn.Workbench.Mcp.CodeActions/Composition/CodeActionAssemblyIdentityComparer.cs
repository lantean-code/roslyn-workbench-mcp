using System.Reflection;
using System.Runtime.CompilerServices;

namespace Roslyn.Workbench.Mcp.CodeActions.Composition;

internal sealed class CodeActionAssemblyIdentityComparer : IEqualityComparer<Assembly>
{
    public static CodeActionAssemblyIdentityComparer Instance { get; } = new();

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

    public int GetHashCode(Assembly assembly)
    {
        return assembly.FullName is string identity
            ? StringComparer.OrdinalIgnoreCase.GetHashCode(identity)
            : RuntimeHelpers.GetHashCode(assembly);
    }
}
