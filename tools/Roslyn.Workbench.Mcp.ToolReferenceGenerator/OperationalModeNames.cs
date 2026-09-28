using Roslyn.Workbench.Mcp.Configuration;

namespace Roslyn.Workbench.Mcp.ToolReferenceGenerator;

/// <summary>
/// Converts typed operational modes to their public command-line values.
/// </summary>
internal static class OperationalModeNames
{
    private static readonly OperationalMode[] _all = Enum.GetValues<OperationalMode>();
    private static readonly HashSet<string> _names = _all.Select(GetName).ToHashSet(StringComparer.Ordinal);

    /// <summary>
    /// Gets every supported operational mode in declaration order.
    /// </summary>
    public static IReadOnlyList<OperationalMode> All => _all;

    /// <summary>
    /// Determines whether a value is a supported public operational-mode name.
    /// </summary>
    /// <param name="value">The public value to inspect.</param>
    /// <returns><see langword="true"/> when the value names a supported mode; otherwise, <see langword="false"/>.</returns>
    public static bool IsSupported(string value)
    {
        return _names.Contains(value);
    }

    /// <summary>
    /// Gets the public command-line value for an operational mode.
    /// </summary>
    /// <param name="mode">The mode to format.</param>
    /// <returns>The stable public mode name.</returns>
    public static string GetName(OperationalMode mode)
    {
        return mode switch
        {
            OperationalMode.InspectionOnly => "inspection-only",
            OperationalMode.Transactional => "transactional",
            OperationalMode.ApprovalRequired => "approval-required",
            OperationalMode.AutonomousTrusted => "autonomous-trusted",
            _ => throw new ArgumentOutOfRangeException(nameof(mode)),
        };
    }
}
