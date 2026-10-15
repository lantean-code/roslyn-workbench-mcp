using System.Reflection;

namespace Roslyn.Workbench.Mcp.Hosting;

/// <summary>
/// Handles command-line requests that complete without starting the MCP server.
/// </summary>
internal static class HostCommandLine
{
    /// <summary>
    /// Writes usage and supported options when help is requested, without resolving startup configuration.
    /// </summary>
    /// <param name="arguments">The command-line arguments supplied to the Host.</param>
    /// <param name="output">The writer that receives command output.</param>
    /// <returns><see langword="true"/> when help was requested; otherwise, <see langword="false"/>.</returns>
    public static bool TryWriteHelp(IReadOnlyList<string> arguments, TextWriter output)
    {
        if (!arguments.Contains("--help", StringComparer.Ordinal)
            && !arguments.Contains("-h", StringComparer.Ordinal))
        {
            return false;
        }

        output.WriteLine("""
            Roslyn Workbench MCP
            Usage: roslyn-workbench-mcp [options]

            Runs a local MCP server over standard input and output.

            Commands:
              --help, -h                             Show help and exit without starting the server.
              --version                              Show the product version and exit.

            Server options:
              --operational-mode <mode>               inspection-only (default), transactional,
                                                      approval-required or autonomous-trusted.
              --commit-validation <policy>            none (default) or no-new-compiler-errors.
              --state-directory <path>                Writable directory for durable recovery storage.
              --allowed-workspace-root <path>         Restrict admission to a trusted root; repeatable.
              --external-document-policy <policy>     allow-read-only (default) or reject-workspace.
              --generated-source-policy <policy>      warn (default) or deny.
              --generated-source-exception <pattern>  Exempt a Workspace-relative path; repeatable.
              --enable-plugins                        Enable trusted external plugins; takes no value.
              --plugin-directory <path>               External plugin directory; repeatable.
              --tool-output-schema-mode <mode>        Omit (default) or Full.

            Resource and reporting options:
              --default-max-results <count>
              --code-action-reference-lifetime <duration>
              --workspace-query-cache-size-limit <count>
              --plugin-query-cache-entry-limit <count>
              --code-action-reference-cache-size-limit <count>
              --workspace-query-cache-sliding-expiration <duration>
              --plugin-query-cache-sliding-expiration <duration>
              --max-transaction-revisions <count>
              --max-concurrent-queries <count>
              --error-reporting-consent <never|prompt|always>
              --error-record-capacity <count>
              --error-record-lifetime <duration>
              --error-record-max-bytes <bytes>
              --error-submission-capacity <count>
              --error-submission-lifetime <duration>
              --error-report-max-bytes <bytes>

            Durations use TimeSpan syntax, for example 00:05:00.
            Configuration reference, defaults and equivalent environment variables:
              https://lantean-code.github.io/roslyn-workbench-mcp/latest/configuration.html
            """);
        return true;
    }

    /// <summary>
    /// Writes the exact product version when the command line contains only <c>--version</c>.
    /// </summary>
    /// <param name="arguments">The command-line arguments supplied to the Host.</param>
    /// <param name="output">The writer that receives command output.</param>
    /// <returns><see langword="true"/> when the command was handled; otherwise, <see langword="false"/>.</returns>
    public static bool TryWriteVersion(IReadOnlyList<string> arguments, TextWriter output)
    {
        if (arguments.Count != 1
            || !string.Equals(arguments[0], "--version", StringComparison.Ordinal))
        {
            return false;
        }

        var version = typeof(HostCommandLine).Assembly
            .GetCustomAttributes<AssemblyInformationalVersionAttribute>()
            .Single()
            .InformationalVersion;

        output.WriteLine(version);
        return true;
    }
}
