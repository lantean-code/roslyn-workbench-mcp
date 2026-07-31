namespace Roslyn.Workbench.Mcp.ErrorReporting.Dispatch;

internal sealed record SentryProviderConfiguration
{
    public string Dsn { get; }

    public string Destination { get; }

    public SentryProviderConfiguration(string dsn, string destination)
    {
        Dsn = dsn;
        Destination = destination;
    }
}
