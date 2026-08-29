namespace Microsoft.CodeAnalysis.LookalikePluginFixture;

public static class LookalikePluginFailure
{
    public static void Throw()
    {
        throw new LookalikePluginException();
    }
}
