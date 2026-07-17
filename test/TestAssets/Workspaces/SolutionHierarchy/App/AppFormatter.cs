namespace Sample;

public sealed class AppFormatter : IMessageFormatter
{
    public string Format(string value)
    {
        return value.Trim();
    }
}

public static class AppCaller
{
    public static string Call()
    {
        return new AppFormatter().Format("value");
    }
}
