using System.Reflection;

namespace Roslyn.Workbench.Mcp.Hosting;

/// <summary>
/// Handles command-line requests that complete without starting the MCP server.
/// </summary>
internal static class HostCommandLine
{
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
