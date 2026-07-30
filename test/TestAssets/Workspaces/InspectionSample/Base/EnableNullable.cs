#nullable enable

namespace Sample;

public sealed class EnableNullableSample
{
    public string? Value { get; set; }

    public int GetLength()
    {
        return Value.Length;
    }
}