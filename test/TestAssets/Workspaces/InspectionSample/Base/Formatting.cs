using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sample;

/// <summary>Formats greeting values.</summary>
public interface IMessageFormatter
{
    string Format(string value);
}

public interface IGoo
{
    void Goo1();

    void Goo2();
}

public interface IBar
{
    void Bar();
}

[Obsolete("Use DerivedGreetingFormatter")]
public abstract class FormatterBase
{
    public string Prefix => "prefix";

    public abstract string Format(string value);

    public virtual string Decorate(string value)
    {
        return $"[{value}]";
    }
}

/// <summary>Formats greeting values.</summary>
[Serializable]
public class GreetingFormatter : FormatterBase, IMessageFormatter
{
    /// <summary>Formats a greeting.</summary>
    public override string Format(string value)
    {
        var unused = 42;
        if (value.Length == 0)
        {
            return string.Empty;
        }

        var upper = value.ToUpperInvariant();
        return Decorate(upper);
    }

    public string Format(string value, bool excited)
    {
        var formatted = Format(value);
        return excited ? formatted + "!" : formatted;
    }

    public override string Decorate(string value)
    {
        return $"Hello {value}";
    }
}

public sealed class DerivedGreetingFormatter : GreetingFormatter
{
    public override string Decorate(string value)
    {
        return base.Decorate(value) + " from derived";
    }
}

public static class FormatterCaller
{
    public static string Call()
    {
        var formatter = new GreetingFormatter();
        return formatter.Format("hi");
    }
}

public sealed class AlphaCycle
{
    public BetaCycle? Beta { get; init; }
}

public sealed class BetaCycle
{
    public AlphaCycle? Alpha { get; init; }
}

public static class FormatterCallerTests
{
    public static void GIVEN_FormatterCaller_WHEN_CallingCall_THEN_ShouldReturnFormattedGreeting()
    {
        FormatterCaller.Call();
    }

    public static void Helper()
    {
        new GreetingFormatter().Decorate("helper");
    }
}

public static class FlowSamples
{
    public static string Analyse(string value)
    {
        var trimmed = value.Trim();
        var unusedFlow = trimmed.Length;
        if (trimmed.Length == 0)
        {
            return string.Empty;
        }

        var upper = trimmed.ToUpperInvariant();
        return upper;
    }

    public static string AnalyseExceptional(string? value)
    {
        try
        {
            if (value is null)
            {
                throw new ArgumentNullException(nameof(value));
            }

            return value.Trim();
        }
        catch (ArgumentNullException)
        {
            return string.Empty;
        }
        finally
        {
            value?.Length ?? 0;
        }
    }
}

public sealed class StateHolder
{
    public string Current { get; private set; } = string.Empty;

    public void Set(string value)
    {
        Current = value;
    }

    public string Get()
    {
        return Current;
    }
}

public sealed class FieldHolder
{
    private string _backingField = string.Empty;

    public string Read()
    {
        return _backingField;
    }
}

public static class LinqSamples
{
    public static IEnumerable<int> FilterPositive(IEnumerable<int> numbers)
    {
        foreach (var number in numbers)
        {
            if (number > 0)
            {
                yield return number + 1;
            }
        }
    }

    public static IEnumerable<int> ExpandQuery(IEnumerable<int> numbers)
    {
        return from number in numbers
               where number > 0
               select number + 1;
    }
}

public static class IntroduceVariableSamples
{
    public static int Build()
    {
        return 1 + 1;
    }
}

public abstract class PartialBase
{
    public virtual string DecorateAgain(string value)
    {
        return value;
    }
}

public sealed class OverrideCandidate : PartialBase
{
}

public static class AwaitSamples
{
    private static Task<string> GetValueAsync()
    {
        return Task.FromResult("value");
    }

    public static async Task<string> BuildAsync()
    {
        return GetValueAsync();
    }

    public static async Task<string> BuildAssignmentAsync()
    {
        var value = GetValueAsync();
        return string.Empty;
    }
}

public static class LoopSamples
{
    public static int SumForeach(int[] values)
    {
        var total = 0;
        foreach (var value in values)
        {
            total += value;
        }

        return total;
    }

    public static int SumFor(int[] values)
    {
        var total = 0;
        for (var i = 0; i < values.Length; i++)
        {
            total += values[i];
        }

        return total;
    }
}

public static class ConditionalSamples
{
    public static string DescribeCount(int count)
    {
        return count == 0 ? "zero" : "non-zero";
    }

    public static string DescribeValue(int value)
    {
        if (value == 0)
        {
            return "zero";
        }
        else if (value == 1)
        {
            return "one";
        }
        else
        {
            return "many";
        }
    }

    public static int GuardedAdd(int left, int right)
    {
        if (left > 0)
        {
            return left + right;
        }

        return right;
    }

    public static bool IsInRange(int value)
    {
        return value > 0 && value < 10;
    }
}

public static class TypeStyleSamples
{
    public static string UseExplicit()
    {
        var explicitBuilder = new StringBuilder();
        explicitBuilder.Append("value");
        return explicitBuilder.ToString();
    }

    public static string UseImplicit()
    {
        StringBuilder implicitBuilder = new StringBuilder();
        implicitBuilder.Append("value");
        return implicitBuilder.ToString();
    }
}

public static class DisposableSamples
{
    public static int Build()
    {
        var stream = new MemoryStream();
        var length = stream.Length;
        return (int)length;
    }
}

public static class NamedArgumentSamples
{
    public static int Sum(int left, int right)
    {
        return left + right;
    }

    public static int Build()
    {
        return Sum(1, 2);
    }
}

public sealed class PatternPerson
{
    public string Name { get; init; } = string.Empty;
}

public sealed class PatternCounter
{
    public int C { get; init; }
}

public static class PatternSamples
{
    public static bool IsAlpha(PatternPerson? person)
    {
        return person != null && person.Name == "Alpha";
    }
}

public sealed class PatternFieldHolder
{
    private PatternCounter? cf;

    public bool HasNonZeroCount()
    {
        if (cf != null && cf.C != 0)
        {
            return true;
        }

        return false;
    }
}

public static class LocalFunctionSamples
{
    public static int Build()
    {
        var increment = 1;

        int Local(int value)
        {
            return value + increment;
        }

        return Local(1);
    }
}

public static class MoveDeclarationSamples
{
    public static int Build()
    {
        var result = 10;
        Console.WriteLine("prefix");
        Console.WriteLine("middle");
        return result;
    }

    public static void BuildNearest()
    {
        int moved;
        Console.WriteLine("prefix");
        Console.WriteLine(moved);
    }
}

public static class QualifiedTypeSamples
{
    public static string Build()
    {
        System.Net.Http.HttpClient client = new();
        return client.BaseAddress?.ToString() ?? string.Empty;
    }
}

public static class StringLiteralSamples
{
    public static string BuildRegular()
    {
        return "C:\\temp\\logs";
    }

    public static string BuildInterpolated(string value)
    {
        return $"C:\\temp\\{value}";
    }
}

public static class CastSamples
{
    public static object Box()
    {
        return (object)1;
    }

    public static string? Unbox(object value)
    {
        return value as string;
    }
}

public static class ConditionalRewriteSamples
{
    public static int Build(bool enabled)
    {
        var value = enabled ? 1 : 2;
        return value;
    }

    public static int BuildAssignment(bool enabled)
    {
        int value;
        value = enabled ? 1 : 2;
        return value;
    }
}

public static class DuplicateCodeSamples
{
    public static int ComputeOne(int value)
    {
        var adjusted = value + 1;
        adjusted *= 2;
        return adjusted - 3;
    }

    public static int ComputeTwo(int value)
    {
        var adjusted = value + 1;
        adjusted *= 2;
        return adjusted - 3;
    }
}

public static class TupleSamples
{
    public static (int Sum, int Count) Build()
    {
        return (1 + 1, 2);
    }
}

public static class AnonymousTypeSamples
{
    public static object Build()
    {
        var item = new { Name = "Alpha", Count = 1 };
        return item;
    }
}

public sealed class AutoPropertySamples
{
    public int Goo { get; set; }
}

public sealed class FullPropertySamples
{
    private int _score;

    public int Score
    {
        get
        {
            return _score;
        }
        set
        {
            _score = value;
        }
    }
}

public class ConvertibleToRecord
{
    public int Id { get; init; }
}

/// <summary>Use System.IDisposable for the returned value.</summary>
public static class DocCommentSamples
{
    public static string Build(string value)
    {
        return value;
    }
}

public sealed class PrimaryConstructorSamples(int value)
{
    public int Value => value;
}

public static class NumericLiteralSamples
{
    public static int Build()
    {
        return 42;
    }
}

public static class ExpressionBodySamples
{
    public static int Square(int value)
    {
        return value * value;
    }

    public static Func<int, int> CreateLambda()
    {
        return value =>
        {
            return value + 1;
        };
    }
}

public static class InlineMethodSamples
{
    public static int Caller()
    {
        return AddOne(1);
    }

    private static int AddOne(int value)
    {
        return value + 1;
    }
}

public sealed class ParameterInitializationSamples
{
    private readonly string _name;

    public ParameterInitializationSamples(string name)
    {
    }
}

public sealed class ExplicitInterfaceSamples : IGoo, IBar
{
    public void Goo1()
    {
    }

    public void Goo2()
    {
    }

    public void Bar()
    {
    }
}

public sealed class ImplicitInterfaceSamples : IGoo, IBar
{
    void IGoo.Goo1()
    {
    }

    void IGoo.Goo2()
    {
    }

    void IBar.Bar()
    {
    }
}

public static class IfRewriteSamples
{
    public static int MergeNested(bool left, bool right)
    {
        if (left)
        {
            if (right)
            {
                return 1;
            }
        }

        return 0;
    }

    public static int MergeConsecutive(bool left, bool right)
    {
        if (left)
        {
            return 1;
        }

        if (right)
        {
            return 1;
        }

        return 0;
    }

    public static int SplitNested(bool left, bool right)
    {
        if (left && right)
        {
            return 1;
        }

        return 0;
    }

    public static int SplitConsecutive(bool left, bool right)
    {
        if (left || right)
        {
            return 1;
        }

        return 0;
    }
}