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
        _ = value;
    }

    private static void AcceptValue(int value)
    {
        _ = value;
    }
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
