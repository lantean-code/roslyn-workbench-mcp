namespace Roslyn.Workbench.Mcp.ToolReferenceGenerator;

/// <summary>
/// Identifies the output and authored-example inputs used for one documentation generation run.
/// </summary>
internal sealed class ToolReferenceGeneratorOptions
{
    /// <summary>
    /// Gets the directory that receives generated reference files.
    /// </summary>
    public required string OutputDirectory { get; init; }

    /// <summary>
    /// Gets the file containing canonical tool-call examples.
    /// </summary>
    public required string ExamplesFile { get; init; }

    /// <summary>
    /// Gets the optional security-reference generation settings.
    /// </summary>
    public SecurityReferenceGeneratorOptions? SecurityReference { get; init; }

    /// <summary>
    /// Parses and validates generator command-line arguments.
    /// </summary>
    /// <param name="args">The arguments supplied to the generator.</param>
    /// <returns>The validated generator options.</returns>
    public static ToolReferenceGeneratorOptions Parse(IReadOnlyList<string> args)
    {
        string? outputDirectory = null;
        string? examplesFile = null;
        string? securityOutputDirectory = null;
        string? securityBaselineFile = null;
        string? securityManifestFile = null;
        string? repositoryRoot = null;
        string? sourceRevision = null;
        var updateSecurityBaseline = false;

        for (var index = 0; index < args.Count; index++)
        {
            var argument = args[index];
            if (argument == "--update-security-baseline")
            {
                updateSecurityBaseline = true;
                continue;
            }

            if (index + 1 >= args.Count)
            {
                throw new ArgumentException($"Generator option '{argument}' requires a value.", nameof(args));
            }

            var value = args[++index];
            switch (argument)
            {
                case "--output":
                    outputDirectory = value;
                    break;
                case "--examples":
                    examplesFile = value;
                    break;
                case "--security-output":
                    securityOutputDirectory = value;
                    break;
                case "--security-baseline":
                    securityBaselineFile = value;
                    break;
                case "--security-manifest":
                    securityManifestFile = value;
                    break;
                case "--repository-root":
                    repositoryRoot = value;
                    break;
                case "--source-revision":
                    sourceRevision = value;
                    break;
                default:
                    throw new ArgumentException($"Generator option '{argument}' is not supported.", nameof(args));
            }
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(outputDirectory);
        ArgumentException.ThrowIfNullOrWhiteSpace(examplesFile);

        var securityValues = new[]
        {
            securityOutputDirectory,
            securityBaselineFile,
            securityManifestFile,
            repositoryRoot,
            sourceRevision,
        };

        if (securityValues.Any(static value => value is not null)
            && securityValues.Any(static value => value is null))
        {
            throw new ArgumentException("Security-reference generation requires --security-output, --security-baseline, --security-manifest, --repository-root and --source-revision.", nameof(args));
        }

        if (updateSecurityBaseline && securityOutputDirectory is null)
        {
            throw new ArgumentException("--update-security-baseline requires complete security-reference generation options.", nameof(args));
        }

        if (sourceRevision is not null && !IsCommitHash(sourceRevision))
        {
            throw new ArgumentException("--source-revision must be a 40- or 64-character Git commit hash.", nameof(args));
        }

        SecurityReferenceGeneratorOptions? securityReference = null;
        if (securityOutputDirectory is not null
            && securityBaselineFile is not null
            && securityManifestFile is not null
            && repositoryRoot is not null
            && sourceRevision is not null)
        {
            securityReference = CreateSecurityReferenceOptions(
                securityOutputDirectory,
                securityBaselineFile,
                securityManifestFile,
                repositoryRoot,
                sourceRevision,
                updateSecurityBaseline);
        }

        return new ToolReferenceGeneratorOptions
        {
            OutputDirectory = Path.GetFullPath(outputDirectory),
            ExamplesFile = Path.GetFullPath(examplesFile),
            SecurityReference = securityReference,
        };
    }

    private static SecurityReferenceGeneratorOptions CreateSecurityReferenceOptions(
        string outputDirectory,
        string baselineFile,
        string manifestFile,
        string repositoryRoot,
        string sourceRevision,
        bool updateBaseline)
    {
        return new SecurityReferenceGeneratorOptions
        {
            OutputDirectory = Path.GetFullPath(outputDirectory),
            BaselineFile = Path.GetFullPath(baselineFile),
            ManifestFile = Path.GetFullPath(manifestFile),
            RepositoryRoot = Path.GetFullPath(repositoryRoot),
            SourceRevision = sourceRevision,
            UpdateBaseline = updateBaseline,
        };
    }

    private static bool IsCommitHash(string value)
    {
        return value.Length is 40 or 64
            && value.All(static character => char.IsAsciiHexDigit(character));
    }
}
