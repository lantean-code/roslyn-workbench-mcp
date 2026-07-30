namespace Sample;

internal static class CandidateStatementAsynchronous
{
    internal static async System.Threading.Tasks.Task AddAwaitForeach(
        System.Collections.Generic.IAsyncEnumerable<int> values)
    {
        foreach (var value in values)
        {
            value;
        }
    }

    internal static async System.Threading.Tasks.Task AddAwaitUsing(
        System.IAsyncDisposable resource)
    {
        using (resource)
        {
            await System.Threading.Tasks.Task.Yield();
        }
    }
}

internal sealed class CandidateSameVariable
{
    private int Value { get; set; }

    internal void Assign(int value)
    {
        value = value;
    }

    internal bool Compare(int value)
    {
        return value == value;
    }
}

public sealed class CandidateDocumentationComments
{
    /// <summary>Adds a missing parameter node.</summary>
    /// <param name="documented">The documented value.</param>
    public void AddMissingParameter(int documented, int missing)
    {
        documented;
        missing;
    }

    /// <summary>Contains a duplicate parameter node.</summary>
    /// <param name="value">The value.</param>
    /// <param name="value">The duplicate value.</param>
    public void RemoveDuplicateParameter(int value)
    {
        value;
    }

    /// <summary>Contains an unmatched parameter node.</summary>
    /// <param name="missing">The missing value.</param>
    public void RemoveUnmatchedParameter(int value)
    {
        value;
    }
}

/// <summary>Contains a duplicate type parameter node.</summary>
/// <typeparam name="T">The value type.</typeparam>
/// <typeparam name="T">The duplicate value type.</typeparam>
public sealed class CandidateDuplicateTypeParameter<T>
{
}

internal static class CandidateCapturedLocalFunction
{
    internal static void PassCapturedVariable(int captured)
    {
        LocalFunction();

        static void LocalFunction()
        {
            captured;
        }
    }
}

internal static class CandidateStaticType
{
    internal void MakeMemberStatic()
    {
    }
}

internal static class CandidateMethodAsynchronous
{
    internal static int ReturnValue()
    {
        await System.Threading.Tasks.Task.Yield();
        return 1;
    }

    internal static void ReturnVoid()
    {
        await System.Threading.Tasks.Task.Yield();
    }

    internal static void MakeLambdaAsynchronous()
    {
        System.Action action = () =>
        {
            await System.Threading.Tasks.Task.Yield();
        };

        action();
    }
}

internal struct CandidateRefStruct
{
    private System.Span<int> _values;
}

internal class CandidateAbstractType
{
    internal abstract void RequiredMember();
}

partial class CandidatePartialType
{
}

internal class CandidatePartialType
{
}

internal sealed class CandidateSealedBase
{
}

internal class CandidateSealedDerived : CandidateSealedBase
{
}