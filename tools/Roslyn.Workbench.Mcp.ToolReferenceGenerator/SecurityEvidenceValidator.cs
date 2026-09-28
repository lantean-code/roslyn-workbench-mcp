using Microsoft.Build.Locator;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.MSBuild;
using BuildProjectCollection = Microsoft.Build.Evaluation.ProjectCollection;
using BuildProjectItem = Microsoft.Build.Evaluation.ProjectItem;

namespace Roslyn.Workbench.Mcp.ToolReferenceGenerator;

/// <summary>
/// Validates that authored evidence identifies discoverable xUnit tests compiled by the referenced projects.
/// </summary>
internal sealed class SecurityEvidenceValidator
{
    private static readonly Lock _registrationLock = new();

    private readonly string _repositoryRoot;

    /// <summary>
    /// Initializes a validator rooted at the repository containing the referenced evidence.
    /// </summary>
    /// <param name="repositoryRoot">The repository root.</param>
    public SecurityEvidenceValidator(string repositoryRoot)
    {
        EnsureMsBuildRegistered();
        _repositoryRoot = Path.TrimEndingDirectorySeparator(Path.GetFullPath(repositoryRoot));
    }

    private static StringComparer PathComparer => OperatingSystem.IsWindows()
        ? StringComparer.OrdinalIgnoreCase
        : StringComparer.Ordinal;

    private static StringComparison PathComparison => OperatingSystem.IsWindows()
        ? StringComparison.OrdinalIgnoreCase
        : StringComparison.Ordinal;

    private static Dictionary<string, string> CreateGlobalProperties()
    {
        var properties = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (IsWindowsSubsystemForLinux())
        {
            properties["ArtifactsPath"] = "/tmp/artifacts/roslyn-workbench-mcp";
        }

        return properties;
    }

    private static bool IsWindowsSubsystemForLinux()
    {
        if (!OperatingSystem.IsLinux())
        {
            return false;
        }

        const string releaseFile = "/proc/sys/kernel/osrelease";
        return File.Exists(releaseFile)
            && File.ReadAllText(releaseFile).Contains("microsoft", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Validates every evidence reference against its evaluated test project and semantic compilation.
    /// </summary>
    /// <param name="evidenceReferences">The evidence references to validate.</param>
    /// <param name="cancellationToken">The token used to cancel project evaluation.</param>
    /// <returns>A task that completes when every reference has been validated.</returns>
    public async Task ValidateAsync(
        IEnumerable<SecurityEvidenceReference> evidenceReferences,
        CancellationToken cancellationToken)
    {
        var evidenceByProject = evidenceReferences.GroupBy(
            evidence => ResolveRepositoryPath(evidence.Project),
            PathComparer);

        foreach (var projectEvidence in evidenceByProject)
        {
            await ValidateProjectAsync(projectEvidence.Key, projectEvidence.ToArray(), cancellationToken);
        }
    }

    private async Task ValidateProjectAsync(
        string projectPath,
        SecurityEvidenceReference[] evidenceReferences,
        CancellationToken cancellationToken)
    {
        var authoredProjectPath = evidenceReferences[0].Project;
        if (!File.Exists(projectPath))
        {
            throw new InvalidOperationException($"Evidence project '{authoredProjectPath}' does not exist.");
        }

        var workspaceFailures = new List<string>();
        var globalProperties = CreateGlobalProperties();
        ValidateEvaluatedTestProject(projectPath, authoredProjectPath, globalProperties);

        using var workspace = MSBuildWorkspace.Create(globalProperties);
        workspace.RegisterWorkspaceFailedHandler(args =>
        {
            if (args.Diagnostic.Kind == WorkspaceDiagnosticKind.Failure)
            {
                workspaceFailures.Add(args.Diagnostic.Message);
            }
        });

        var project = await workspace.OpenProjectAsync(projectPath, cancellationToken: cancellationToken);
        if (workspaceFailures.Count > 0)
        {
            var failures = string.Join("; ", workspaceFailures);
            throw new InvalidOperationException($"Evidence project '{authoredProjectPath}' could not be evaluated: {failures}");
        }

        var compilation = await project.GetCompilationAsync(cancellationToken)
            ?? throw new InvalidOperationException($"Evidence project '{authoredProjectPath}' did not produce a compilation.");

        ValidateCompilation(compilation, authoredProjectPath, cancellationToken);

        foreach (var evidence in evidenceReferences)
        {
            await ValidateReferenceAsync(evidence, project, cancellationToken);
        }
    }

    private async Task ValidateReferenceAsync(
        SecurityEvidenceReference evidence,
        Project project,
        CancellationToken cancellationToken)
    {
        ValidateLevel(evidence);
        var sourcePath = ResolveRepositoryPath(evidence.Source);

        if (!File.Exists(sourcePath))
        {
            throw new InvalidOperationException($"Evidence source '{evidence.Source}' does not exist.");
        }

        var separator = evidence.Test.LastIndexOf('.');
        if (separator <= 0 || separator == evidence.Test.Length - 1)
        {
            throw new InvalidOperationException($"Evidence test '{evidence.Test}' must be fully qualified.");
        }

        var document = project.Documents.SingleOrDefault(document =>
            document.FilePath is not null
            && PathComparer.Equals(Path.GetFullPath(document.FilePath), sourcePath));

        if (document is null)
        {
            throw new InvalidOperationException($"Evidence source '{evidence.Source}' is not compiled by project '{evidence.Project}'.");
        }

        var root = await document.GetSyntaxRootAsync(cancellationToken);
        var semanticModel = await document.GetSemanticModelAsync(cancellationToken);
        if (root is null || semanticModel is null)
        {
            throw new InvalidOperationException($"Evidence source '{evidence.Source}' could not be compiled for semantic validation.");
        }

        var matchingSymbols = root.DescendantNodes()
            .OfType<MethodDeclarationSyntax>()
            .Select(method => semanticModel.GetDeclaredSymbol(method, cancellationToken))
            .OfType<IMethodSymbol>()
            .Where(symbol => GetFullyQualifiedMethodName(symbol) == evidence.Test)
            .ToArray();

        if (matchingSymbols.Length == 0)
        {
            throw new InvalidOperationException($"Evidence test '{evidence.Test}' was not found in '{evidence.Source}'.");
        }

        if (matchingSymbols.Length > 1)
        {
            throw new InvalidOperationException($"Evidence test '{evidence.Test}' is ambiguous in '{evidence.Source}'.");
        }

        if (!XunitEvidenceMethodValidator.TryValidate(matchingSymbols[0], out var rejectionReason))
        {
            throw new InvalidOperationException($"Evidence test '{evidence.Test}' is not an executable, unconditional xUnit Fact: {rejectionReason}.");
        }
    }

    /// <summary>
    /// Verifies that an evaluated evidence project includes the repository's operative xUnit test infrastructure.
    /// </summary>
    /// <param name="projectPath">The absolute project path to evaluate.</param>
    /// <param name="authoredProjectPath">The authored repository-relative project path.</param>
    /// <param name="globalProperties">The global properties used for the production evaluation.</param>
    public static void ValidateEvaluatedTestProject(
        string projectPath,
        string authoredProjectPath,
        IReadOnlyDictionary<string, string>? globalProperties = null)
    {
        Dictionary<string, string>? evaluationProperties = null;
        if (globalProperties is not null)
        {
            evaluationProperties = new Dictionary<string, string>(globalProperties, StringComparer.OrdinalIgnoreCase);
        }

        using var projectCollection = new BuildProjectCollection(evaluationProperties);
        var project = projectCollection.LoadProject(projectPath);
        var isTestProject = string.Equals(
            project.GetPropertyValue("IsTestProject"),
            "true",
            StringComparison.OrdinalIgnoreCase);

        var hasXunitRunner = project.GetItems("PackageReference")
            .Any(static item => string.Equals(
                    item.EvaluatedInclude,
                    "xunit.v3.mtp-v2",
                    StringComparison.OrdinalIgnoreCase)
                && IncludesRunnerAssets(item));

        if (!isTestProject || !hasXunitRunner)
        {
            throw new InvalidOperationException(
                $"Evidence project '{authoredProjectPath}' must declare IsTestProject=true and the xunit.v3.mtp-v2 runner.");
        }
    }

    private static bool IncludesRunnerAssets(BuildProjectItem packageReference)
    {
        var excludedAssets = SplitAssets(packageReference.GetMetadataValue("ExcludeAssets"));
        if (excludedAssets.Contains("all")
            || excludedAssets.Contains("build")
            || excludedAssets.Contains("buildtransitive"))
        {
            return false;
        }

        var includedAssets = SplitAssets(packageReference.GetMetadataValue("IncludeAssets"));
        return includedAssets.Count == 0
            || includedAssets.Contains("all")
            || includedAssets.Contains("build")
            || includedAssets.Contains("buildtransitive");
    }

    private static HashSet<string> SplitAssets(string assets)
    {
        return assets.Split([';', ','], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
    }

    private string ResolveRepositoryPath(string relativePath)
    {
        if (Path.IsPathFullyQualified(relativePath))
        {
            throw new InvalidOperationException($"Evidence path '{relativePath}' must be repository-relative.");
        }

        var resolved = Path.GetFullPath(Path.Combine(_repositoryRoot, relativePath));
        if (!resolved.StartsWith(_repositoryRoot + Path.DirectorySeparatorChar, PathComparison)
            && !PathComparer.Equals(resolved, _repositoryRoot))
        {
            throw new InvalidOperationException($"Evidence path '{relativePath}' escapes the repository root.");
        }

        return resolved;
    }

    /// <summary>
    /// Verifies that a referenced evidence project has no compilation errors.
    /// </summary>
    /// <param name="compilation">The semantic compilation produced for the evidence project.</param>
    /// <param name="authoredProjectPath">The authored repository-relative project path.</param>
    /// <param name="cancellationToken">The token used to cancel diagnostic collection.</param>
    public static void ValidateCompilation(
        Compilation compilation,
        string authoredProjectPath,
        CancellationToken cancellationToken)
    {
        var compilationErrors = compilation.GetDiagnostics(cancellationToken)
            .Where(static diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)
            .Select(static diagnostic => diagnostic.ToString())
            .ToArray();

        if (compilationErrors.Length == 0)
        {
            return;
        }

        var errors = string.Join("; ", compilationErrors);
        throw new InvalidOperationException($"Evidence project '{authoredProjectPath}' contains compilation errors: {errors}");
    }

    private static void ValidateLevel(SecurityEvidenceReference evidence)
    {
        if (evidence.Level is not ("unit" or "contract" or "integration" or "acceptance"))
        {
            throw new InvalidOperationException($"Evidence '{evidence.Test}' uses unsupported level '{evidence.Level}'.");
        }
    }

    private static string GetFullyQualifiedMethodName(IMethodSymbol symbol)
    {
        return $"{symbol.ContainingType.ToDisplayString()}.{symbol.Name}";
    }

    private static void EnsureMsBuildRegistered()
    {
        lock (_registrationLock)
        {
            if (!MSBuildLocator.IsRegistered)
            {
                MSBuildLocator.RegisterDefaults();
            }
        }
    }
}
