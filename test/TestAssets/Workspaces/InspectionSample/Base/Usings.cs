using Sample;
using System.Text;
using System;

namespace Sample;

public static class UsingSamples
{
    public static string BuildText()
    {
        StringBuilder builder = new();
        builder.Append(nameof(FormatterBase));
        return builder.ToString();
    }
}