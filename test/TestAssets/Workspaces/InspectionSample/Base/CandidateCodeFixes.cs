namespace Sample;

internal static class CandidateCodeFixes
{
    internal static object CreateAnonymousMember(int value)
    {
        return new { value + 1 };
    }

    internal static int AddExplicitCast(long value)
    {
        return value;
    }

    internal static string FormatConditional(bool enabled)
    {
        return $"{enabled ? "enabled" : "disabled"}";
    }

    internal static void RemoveInKeyword()
    {
        var value = 1;
        AcceptValue(in value);
    }

    internal static bool ReplaceDefaultLiteral(int value)
    {
        return value is default;
    }

    internal static void UseExplicitTypeForConst()
    {
        const var value = 1;
        value;
    }

    internal static char AssignOutParameterAboveReturn(out int value)
    {
        return 'a';
    }

    internal static char AssignOutParameterAtStart(bool enabled, out int value)
    {
        if (enabled)
        {
            value = 1;
        }

        return 'a';
    }

    internal static string DeclareAsNullable()
    {
        return null;
    }

    internal static string DeclareSecondAsNullable()
    {
        return null;
    }

    internal static void DeclareLocalAsNullable()
    {
        string value = null;
        value;
    }

    internal static void DeclareParameterAsNullable()
    {
        AcceptNullableValue(null);
    }

    internal static void FixReturnType()
    {
        return 1;
    }

    internal static async System.Threading.Tasks.Task FixAsyncReturnType()
    {
        await System.Threading.Tasks.Task.Yield();
        return 1;
    }

    internal static void FixExpressionBodyReturnType() => 1;

    private static void AcceptValue(int value)
    {
        value;
    }

    private static void AcceptNullableValue(string value)
    {
        value;
    }
}

[System.Obsolete]
internal class CandidateObsoleteBase
{
}

internal sealed class CandidateObsoleteDerived : CandidateObsoleteBase
{
}

[System.Obsolete("message")]
internal class CandidateObsoleteMessageBase
{
}

internal sealed class CandidateObsoleteMessageDerived : CandidateObsoleteMessageBase
{
}

internal class CandidateObsoleteOverrideBase
{
    [System.Obsolete]
    internal virtual void ObsoleteOverride()
    {
    }
}

internal sealed class CandidateObsoleteOverrideDerived : CandidateObsoleteOverrideBase
{
    internal override void ObsoleteOverride()
    {
    }
}

internal sealed class CandidateObsoleteCollection : System.Collections.Generic.IEnumerable<int>
{
    [System.Obsolete]
    public void Add(int value)
    {
        value;
    }

    public System.Collections.Generic.IEnumerator<int> GetEnumerator()
    {
        yield break;
    }

    System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }
}

internal sealed class CandidateObsoleteMessageCollection : System.Collections.Generic.IEnumerable<int>
{
    [System.Obsolete("message")]
    public void Add(int value)
    {
        value;
    }

    public System.Collections.Generic.IEnumerator<int> GetEnumerator()
    {
        yield break;
    }

    System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }
}

internal static class CandidateObsoleteCollectionConsumer
{
    internal static void CreateObsoleteCollection()
    {
        new CandidateObsoleteCollection { 1 };
    }

    internal static void CreateObsoleteMessageCollection()
    {
        new CandidateObsoleteMessageCollection { 1 };
    }
}

internal sealed class CandidateEnumConstraint<T>
    where T : enum
{
}

internal sealed class CandidateDelegateConstraint<T>
    where T : delegate
{
}

internal sealed class CandidateUninitializedNullable
{
    internal string Value { get; }
}

public class CandidateBase
{
    public virtual void DocumentedMember()
    {
    }
}

public sealed class CandidateDerived : CandidateBase
{
    public override void DocumentedMember()
    {
    }
}

internal sealed class CandidateNewModifier
{
    internal new void RemoveNewModifier()
    {
    }
}

internal class CandidateHideBaseBase
{
    internal void HiddenMember()
    {
    }
}

internal sealed class CandidateHideBaseDerived : CandidateHideBaseBase
{
    internal void HiddenMember()
    {
    }
}

internal static class CandidateIteratorCodeFixes
{
    internal static System.Collections.Generic.IEnumerable<object> AddYieldForImplicitConversion()
    {
        return "value";
    }

    internal static System.Collections.Generic.IEnumerable<object> AddYieldForExplicitConversion()
    {
        return new object();
    }

    internal static object ChangeIteratorReturnType()
    {
        yield return 0;
    }
}

internal sealed class CandidateRequiredMember
{
    internal string RequiredValue { get; set; }
}

internal struct record CandidateRecordKeyword
{
}

internal static class CandidateUnusedLocalFunction
{
    internal static void RemoveUnusedLocalFunction()
    {
        static void UnusedLocalFunction()
        {
        }
    }
}

partial public class CandidateModifierOrder
{
}

internal static class CandidateExplicitArray
{
    internal static System.Linq.Expressions.Expression<System.Func<string>> CreateExpression()
    {
        return () => Format();
    }

    private static string Format(params char[] characters)
    {
        return new string(characters);
    }

    private static string Format(params System.ReadOnlySpan<char> characters)
    {
        return new string(characters);
    }
}