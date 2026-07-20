using System.Runtime.InteropServices;

namespace Roslyn.Workbench.Mcp.Performance;

internal sealed record RunEnvironmentInfo
{
    public required string HostPath { get; init; }

    public required string FrameworkDescription { get; init; }

    public required string OperatingSystem { get; init; }

    public required string ProcessArchitecture { get; init; }

    public required int ProcessorCount { get; init; }

    public static RunEnvironmentInfo Capture(string hostPath)
    {
        return new RunEnvironmentInfo
        {
            HostPath = hostPath,
            FrameworkDescription = RuntimeInformation.FrameworkDescription,
            OperatingSystem = RuntimeInformation.OSDescription,
            ProcessArchitecture = RuntimeInformation.ProcessArchitecture.ToString(),
            ProcessorCount = Environment.ProcessorCount,
        };
    }
}
