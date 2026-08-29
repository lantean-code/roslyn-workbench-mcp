namespace Microsoft.CodeAnalysis.LookalikePluginFixture;

public sealed class LookalikePluginException : Exception
{
    public LookalikePluginException()
        : this("Lookalike plugin failure.")
    {
    }

    public LookalikePluginException(string message)
        : base(message)
    {
    }

    public LookalikePluginException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
