using Roslyn.Workbench.Mcp.ScenarioRunner.Diagnostics;

namespace Roslyn.Workbench.Mcp.ScenarioRunner.Application;

internal sealed class ScenarioOptions
{
    private const int _defaultIterations = 5;
    private const int _defaultParallelism = 4;
    private const int _defaultWarmups = 1;
    private static readonly TimeSpan _defaultCancellationDelay = TimeSpan.FromMilliseconds(50);
    private static readonly TimeSpan _defaultProfileDuration = TimeSpan.FromSeconds(30);

    public ScenarioCommand Command { get; init; }

    public string? Repository { get; init; }

    public string? Scenario { get; init; }

    public string? HostPath { get; init; }

    public string? CacheDirectory { get; init; }

    public string? OutputDirectory { get; init; }

    public string? FrameworkRoot { get; init; }

    public string? PluginDirectory { get; init; }

    public int Iterations { get; init; } = _defaultIterations;

    public int Parallelism { get; init; } = _defaultParallelism;

    public int Warmups { get; init; } = _defaultWarmups;

    public TimeSpan ProfileDuration { get; init; } = _defaultProfileDuration;

    public TimeSpan CancellationDelay { get; init; } = _defaultCancellationDelay;

    public ProfileKind Profile { get; init; } = ProfileKind.Trace;

    public bool SkipPreparation { get; init; }

    public bool CaptureTrace { get; init; }

    public static ScenarioOptions Parse(IReadOnlyList<string> arguments)
    {
        if (arguments.Count == 0)
        {
            return new ScenarioOptions { Command = ScenarioCommand.Help };
        }

        if (IsHelp(arguments[0]))
        {
            if (arguments.Count > 1)
            {
                throw new ArgumentException("The help command does not accept options.");
            }

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

            if (!IsKnownOption(argument))
            {
                throw new ArgumentException($"Unknown option '{argument}'.");
            }

            if (!IsAllowedForCommand(command, argument))
            {
                throw new ArgumentException($"Option '{argument}' is not valid for the '{arguments[0]}' command.");
            }

            if (IsSwitch(argument))
            {
                if (!switches.Add(argument))
                {
                    throw new ArgumentException($"Option '{argument}' was specified more than once.");
                }

                continue;
            }

            if (++index == arguments.Count
                || arguments[index].StartsWith("--", StringComparison.Ordinal))
            {
                throw new ArgumentException($"Option '{argument}' requires a value.");
            }

            if (!values.TryAdd(argument, arguments[index]))
            {
                throw new ArgumentException($"Option '{argument}' was specified more than once.");
            }
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
            PluginDirectory = GetValue(values, "--plugin-directory"),
            Iterations = ParsePositiveInteger(values, "--iterations", _defaultIterations),
            Parallelism = ParsePositiveInteger(values, "--parallelism", _defaultParallelism),
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

    private static bool IsKnownOption(string value)
    {
        return value.Equals("--repository", StringComparison.OrdinalIgnoreCase)
            || value.Equals("--scenario", StringComparison.OrdinalIgnoreCase)
            || value.Equals("--host", StringComparison.OrdinalIgnoreCase)
            || value.Equals("--cache", StringComparison.OrdinalIgnoreCase)
            || value.Equals("--output", StringComparison.OrdinalIgnoreCase)
            || value.Equals("--framework-root", StringComparison.OrdinalIgnoreCase)
            || value.Equals("--plugin-directory", StringComparison.OrdinalIgnoreCase)
            || value.Equals("--iterations", StringComparison.OrdinalIgnoreCase)
            || value.Equals("--parallelism", StringComparison.OrdinalIgnoreCase)
            || value.Equals("--warmups", StringComparison.OrdinalIgnoreCase)
            || value.Equals("--duration", StringComparison.OrdinalIgnoreCase)
            || value.Equals("--cancel-after", StringComparison.OrdinalIgnoreCase)
            || value.Equals("--profile", StringComparison.OrdinalIgnoreCase)
            || IsSwitch(value);
    }

    private static bool IsSwitch(string value)
    {
        return value.Equals("--skip-prepare", StringComparison.OrdinalIgnoreCase)
            || value.Equals("--capture-trace", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsAllowedForCommand(ScenarioCommand command, string option)
    {
        if (command == ScenarioCommand.List)
        {
            return false;
        }

        if (option.Equals("--repository", StringComparison.OrdinalIgnoreCase)
            || option.Equals("--cache", StringComparison.OrdinalIgnoreCase)
            || option.Equals("--framework-root", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (option.Equals("--parallelism", StringComparison.OrdinalIgnoreCase))
        {
            return command == ScenarioCommand.Concurrency;
        }

        if (option.Equals("--duration", StringComparison.OrdinalIgnoreCase)
            || option.Equals("--profile", StringComparison.OrdinalIgnoreCase))
        {
            return command == ScenarioCommand.Profile;
        }

        if (option.Equals("--cancel-after", StringComparison.OrdinalIgnoreCase))
        {
            return command == ScenarioCommand.Cancel;
        }

        if (option.Equals("--capture-trace", StringComparison.OrdinalIgnoreCase))
        {
            return command == ScenarioCommand.Commit;
        }

        return command != ScenarioCommand.Prepare;
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

        if (string.Equals(value, "concurrency", StringComparison.OrdinalIgnoreCase))
        {
            return ScenarioCommand.Concurrency;
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
