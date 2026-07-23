namespace Roslyn.Workbench.Mcp.ScenarioRunner;

internal sealed class ScenarioOptions
{
    private const int _defaultIterations = 5;
    private const int _defaultWarmups = 1;
    private static readonly TimeSpan _defaultCancellationDelay = TimeSpan.FromMilliseconds(50);
    private static readonly TimeSpan _defaultProfileDuration = TimeSpan.FromSeconds(30);

    public required ScenarioCommand Command { get; init; }

    public string? Repository { get; init; }

    public string? Scenario { get; init; }

    public string? HostPath { get; init; }

    public string? CacheDirectory { get; init; }

    public string? OutputDirectory { get; init; }

    public string? FrameworkRoot { get; init; }

    public int Iterations { get; init; } = _defaultIterations;

    public int Warmups { get; init; } = _defaultWarmups;

    public TimeSpan ProfileDuration { get; init; } = _defaultProfileDuration;

    public TimeSpan CancellationDelay { get; init; } = _defaultCancellationDelay;

    public ProfileKind Profile { get; init; } = ProfileKind.Trace;

    public bool SkipPreparation { get; init; }

    public bool CaptureTrace { get; init; }

    public static ScenarioOptions Parse(IReadOnlyList<string> arguments)
    {
        if (arguments.Count == 0 || IsHelp(arguments[0]))
        {
            return new ScenarioOptions { Command = ScenarioCommand.Help };
        }

        var command = ParseCommand(arguments[0]);
        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var switches = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        for (var index = 1; index < arguments.Count; index++)
        {
            var argument = arguments[index];
            if (!argument.StartsWith("--", StringComparison.Ordinal))
            {
                throw new ArgumentException($"Unexpected argument '{argument}'. Options must start with '--'.");
            }

            if (string.Equals(argument, "--skip-prepare", StringComparison.OrdinalIgnoreCase)
                || string.Equals(argument, "--capture-trace", StringComparison.OrdinalIgnoreCase))
            {
                switches.Add(argument);
                continue;
            }

            if (++index == arguments.Count)
            {
                throw new ArgumentException($"Option '{argument}' requires a value.");
            }

            values[argument] = arguments[index];
        }

        return new ScenarioOptions
        {
            Command = command,
            Repository = GetValue(values, "--repository"),
            Scenario = GetValue(values, "--scenario"),
            HostPath = GetValue(values, "--host"),
            CacheDirectory = GetValue(values, "--cache"),
            OutputDirectory = GetValue(values, "--output"),
            FrameworkRoot = GetValue(values, "--framework-root"),
            Iterations = ParsePositiveInteger(values, "--iterations", _defaultIterations),
            Warmups = ParseNonNegativeInteger(values, "--warmups", _defaultWarmups),
            ProfileDuration = ParseDuration(values, "--duration", _defaultProfileDuration),
            CancellationDelay = ParseDuration(values, "--cancel-after", _defaultCancellationDelay),
            Profile = ParseProfile(GetValue(values, "--profile")),
            SkipPreparation = switches.Contains("--skip-prepare"),
            CaptureTrace = switches.Contains("--capture-trace"),
        };
    }

    private static bool IsHelp(string value)
    {
        return value is "help" or "--help" or "-h";
    }

    private static ScenarioCommand ParseCommand(string value)
    {
        if (string.Equals(value, "list", StringComparison.OrdinalIgnoreCase))
        {
            return ScenarioCommand.List;
        }

        if (string.Equals(value, "prepare", StringComparison.OrdinalIgnoreCase))
        {
            return ScenarioCommand.Prepare;
        }

        if (string.Equals(value, "measure", StringComparison.OrdinalIgnoreCase))
        {
            return ScenarioCommand.Measure;
        }

        if (string.Equals(value, "commit", StringComparison.OrdinalIgnoreCase))
        {
            return ScenarioCommand.Commit;
        }

        if (string.Equals(value, "commit-cancellation", StringComparison.OrdinalIgnoreCase))
        {
            return ScenarioCommand.CommitCancellation;
        }

        if (string.Equals(value, "conflict", StringComparison.OrdinalIgnoreCase))
        {
            return ScenarioCommand.Conflict;
        }

        if (string.Equals(value, "crash-recovery", StringComparison.OrdinalIgnoreCase))
        {
            return ScenarioCommand.CrashRecovery;
        }

        if (string.Equals(value, "state-sequence", StringComparison.OrdinalIgnoreCase))
        {
            return ScenarioCommand.StateSequence;
        }

        if (string.Equals(value, "cancel", StringComparison.OrdinalIgnoreCase))
        {
            return ScenarioCommand.Cancel;
        }

        if (string.Equals(value, "profile", StringComparison.OrdinalIgnoreCase))
        {
            return ScenarioCommand.Profile;
        }

        throw new ArgumentException($"Unknown command '{value}'.");
    }

    private static ProfileKind ParseProfile(string? value)
    {
        if (value is null || string.Equals(value, "trace", StringComparison.OrdinalIgnoreCase))
        {
            return ProfileKind.Trace;
        }

        if (string.Equals(value, "counters", StringComparison.OrdinalIgnoreCase))
        {
            return ProfileKind.Counters;
        }

        if (string.Equals(value, "gcdump", StringComparison.OrdinalIgnoreCase))
        {
            return ProfileKind.GcDump;
        }

        throw new ArgumentException($"Unknown profile '{value}'. Use trace, counters, or gcdump.");
    }

    private static string? GetValue(Dictionary<string, string> values, string name)
    {
        return values.TryGetValue(name, out var value) ? value : null;
    }

    private static int ParsePositiveInteger(Dictionary<string, string> values, string name, int defaultValue)
    {
        var value = ParseNonNegativeInteger(values, name, defaultValue);
        if (value == 0)
        {
            throw new ArgumentOutOfRangeException(name, "The value must be greater than zero.");
        }

        return value;
    }

    private static int ParseNonNegativeInteger(Dictionary<string, string> values, string name, int defaultValue)
    {
        if (!values.TryGetValue(name, out var text))
        {
            return defaultValue;
        }

        if (!int.TryParse(text, CultureInfo.InvariantCulture, out var value) || value < 0)
        {
            throw new ArgumentOutOfRangeException(name, $"'{text}' is not a non-negative integer.");
        }

        return value;
    }

    private static TimeSpan ParseDuration(Dictionary<string, string> values, string name, TimeSpan defaultValue)
    {
        if (!values.TryGetValue(name, out var text))
        {
            return defaultValue;
        }

        if (!TimeSpan.TryParse(text, CultureInfo.InvariantCulture, out var value) || value <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(name, $"'{text}' is not a positive duration.");
        }

        return value;
    }
}
