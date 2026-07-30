namespace Sample;

public static class MissingUsingSamples
{
    public static string Build()
    {
        StringBuilder builder = new();
        builder.Append("value");
        return builder.ToString();
    }
}