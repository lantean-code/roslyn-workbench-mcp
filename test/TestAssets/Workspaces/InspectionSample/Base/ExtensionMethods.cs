namespace Sample;

public static class ExtensionMethodsSample
{
    public static string ToGreeting(this string value)
    {
        return "Hello " + value;
    }
}