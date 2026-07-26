using System.Text;
using System;

namespace Sample;

internal sealed class ConstructorParameterCandidate
{
    private readonly int _count;
    private readonly string _name;

    public ConstructorParameterCandidate()
    {
    }
}

internal readonly struct ComparisonOperatorCandidate : IComparable<ComparisonOperatorCandidate>
{
    public ComparisonOperatorCandidate(int value)
    {
        Value = value;
    }

    public int Value { get; }

    public int CompareTo(ComparisonOperatorCandidate other)
    {
        return Value.CompareTo(other.Value);
    }
}

internal interface ICandidateFormatter
{
    string Format(int value);
}

internal sealed class InterfaceImplementationCandidate : ICandidateFormatter
{

}

internal sealed class MethodPropertyCandidate
{
    private int _value;

    public int GetValue()
    {
        return _value;
    }

    public void SetValue(int value)
    {
        _value = value;
    }

    public int Read()
    {
        return GetValue();
    }

    public void Write(int value)
    {
        SetValue(value);
    }
}

internal sealed class PropertyMethodCandidate
{
    public int Value { get; set; }

    public int Read()
    {
        return Value;
    }

    public void Write(int value)
    {
        Value = value;
    }
}

internal static class CandidateImportOrdering
{
    public static string Format(int value)
    {
        var builder = new StringBuilder();
        builder.Append(value);
        return builder.ToString();
    }
}
