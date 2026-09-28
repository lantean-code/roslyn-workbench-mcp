using System.Reflection;
using System.Text.Json;
using Roslyn.Workbench.Mcp.ToolReferenceGenerator;
using Roslyn.Workbench.Mcp.Workspace.Transactions;

namespace Roslyn.Workbench.Mcp.Test.ToolReference;

[Collection(ToolReferenceGenerationCollectionDefinition.Name)]
[Trait("Category", "Integration")]
public sealed class SecurityReferenceGeneratorIntegrationTests
{
    private const string _sourceRevision = "0123456789abcdef0123456789abcdef01234567";

    [Fact]
    public async Task GIVEN_CheckedInSurfaceAndManifest_WHEN_GeneratingSecurityReference_THEN_ShouldProduceResolvedDocumentation()
    {
        using var directory = TemporaryDirectory.Create("roslyn-workbench-security-reference-tests");
        var repositoryRoot = GetRepositoryRoot();
        var outputDirectory = Path.Combine(directory.DirectoryPath, "content", "reference", "security");
        var options = new SecurityReferenceGeneratorOptions
        {
            OutputDirectory = outputDirectory,
            BaselineFile = Path.Combine(repositoryRoot, "docs", "security", "security-surface-v1.json"),
            ManifestFile = Path.Combine(repositoryRoot, "docs", "security", "security-invariants.json"),
            RepositoryRoot = repositoryRoot,
            SourceRevision = _sourceRevision,
        };

        await SecurityReferenceGenerator.GenerateAsync(options, TestContext.Current.CancellationToken);

        var publicationFile = Path.Combine(outputDirectory, "security-reference.json");
        File.Exists(publicationFile).Should().BeTrue();
        File.Exists(Path.Combine(outputDirectory, "index.md")).Should().BeTrue();
        var publicationJson = await File.ReadAllTextAsync(publicationFile, TestContext.Current.CancellationToken);
        using var publication = JsonDocument.Parse(publicationJson);

        publication.RootElement.GetProperty("format").GetString().Should().Be("roslyn-workbench-security-reference/v1");
        publication.RootElement.GetProperty("entries").GetArrayLength().Should().BeGreaterThan(0);
        var markdown = await File.ReadAllTextAsync(
            Path.Combine(outputDirectory, "index.md"),
            TestContext.Current.CancellationToken);

        markdown.Should().Contain($"/blob/{_sourceRevision}/");
    }

    [Fact]
    public async Task GIVEN_ProductionHostMatrix_WHEN_CollectingSecuritySurface_THEN_ShouldMatchEveryToolAvailabilityFact()
    {
        using var directory = TemporaryDirectory.Create("roslyn-workbench-security-surface-tests");
        var surface = await SecuritySurfaceCollector.CollectAsync(
            directory.DirectoryPath,
            TestContext.Current.CancellationToken);

        var toolItems = surface
            .Where(static item => item.Category == "tool")
            .ToDictionary(static item => item.Key["tool:".Length..], StringComparer.Ordinal);

        foreach (var mode in OperationalModeNames.All)
        {
            foreach (var validation in Enum.GetValues<CommitValidationPolicy>())
            {
                if (mode == OperationalMode.InspectionOnly
                    && validation == CommitValidationPolicy.NoNewCompilerErrors)
                {
                    continue;
                }

                foreach (var reportingEnabled in new[] { false, true })
                {
                    var actual = await ProductionToolCatalogueComposer.ComposeAsync(
                        directory.DirectoryPath,
                        mode,
                        validation,
                        reportingEnabled,
                        TestContext.Current.CancellationToken);

                    var configuration = string.Join(
                        ';',
                        OperationalModeNames.GetName(mode),
                        SecuritySurfaceNames.GetEnumName(validation),
                        reportingEnabled ? "reporting-enabled" : "reporting-disabled");

                    var expectedNames = toolItems
                        .Where(item => item.Value.Facts["availability"]!.AsArray()
                            .Any(value => value!.GetValue<string>() == configuration))
                        .Select(static item => item.Key)
                        .Order(StringComparer.Ordinal);

                    actual.Select(static tool => tool.Name)
                        .Order(StringComparer.Ordinal)
                        .Should()
                        .Equal(expectedNames);
                }
            }
        }
    }

    [Fact]
    public async Task GIVEN_SecurityRelevantContracts_WHEN_CollectingSecuritySurface_THEN_ShouldIncludeDerivedCompatibilityFacts()
    {
        using var directory = TemporaryDirectory.Create("roslyn-workbench-security-surface-tests");
        var surface = await SecuritySurfaceCollector.CollectAsync(
            directory.DirectoryPath,
            TestContext.Current.CancellationToken);

        surface.Should().Contain(item => item.Key == "compiler-error-identity:compiler-error-identity-v1");
        surface.Count(static item => item.Category == "recovery-operation")
            .Should().Be(Enum.GetValues<WorkspaceFileOperation>().Length);

        surface.Count(static item => item.Category == "required-action")
            .Should().Be(Enum.GetValues<RequiredAction>().Length);

        surface.Count(static item => item.Category == "effective-policy")
            .Should().Be(7);

        surface.Should().Contain(item => item.Key == "contract-shape:roslyn-workbench-mcp-workspace-results-recoverystatus");
        surface.Should().Contain(item => item.Key == "contract-shape:roslyn-workbench-mcp-protocol-results-toolcontinuation");
        surface.Should().Contain(item => item.Key == "contract-shape:roslyn-workbench-mcp-protocol-results-toolerror");
        surface.Should().Contain(item => item.Key == "failure-envelope:standard");
        surface.Should().Contain(item => item.Key == "error:UnhandledException");
        surface.Should().Contain(item => item.Key == "error:WorkspacePathInvalid");
        surface.Should().Contain(item => item.Key == "error:WorkspaceSelectorRequired");
        surface.Should().Contain(item => item.Key == "error:CodeActionsUnavailable");

        var inspectionPublication = surface.Single(static item => item.Key == "plugin-publication:inspection-only");
        inspectionPublication.Facts["queryPublished"]!.GetValue<bool>().Should().BeTrue();
        inspectionPublication.Facts["mutationPublished"]!.GetValue<bool>().Should().BeFalse();

        var transactionalPublication = surface.Single(static item => item.Key == "plugin-publication:transactional");
        transactionalPublication.Facts["queryPublished"]!.GetValue<bool>().Should().BeTrue();
        transactionalPublication.Facts["mutationPublished"]!.GetValue<bool>().Should().BeTrue();
    }

    [Fact]
    public async Task GIVEN_ProductionToolMatrix_WHEN_CollectingResponseContracts_THEN_ShouldLockEverySuccessAndRuntimeFailureShape()
    {
        using var directory = TemporaryDirectory.Create("roslyn-workbench-security-surface-tests");
        var surface = await SecuritySurfaceCollector.CollectAsync(
            directory.DirectoryPath,
            TestContext.Current.CancellationToken);

        var successItem = surface.Single(static item => item.Key == "success-envelope:all-published-tools");
        var successTools = successItem.Facts["tools"]!.AsObject();
        var publishedTools = surface.Count(static item => item.Category == "tool");

        successTools.Count.Should().Be(publishedTools);
        successTools.Should().ContainKey("transaction-review");
        successTools.Should().ContainKey("transaction-validate");

        var failureItem = surface.Single(static item => item.Key == "failure-envelope:standard");
        var runtimeEnvelopes = failureItem.Facts["runtimeEnvelopes"]!.AsObject();
        var handledProperties = runtimeEnvelopes["handled"]!.AsObject().Select(static property => property.Key);
        var unhandledProperties = runtimeEnvelopes["unhandled"]!.AsObject().Select(static property => property.Key);

        handledProperties.Should().Equal("ok", "error", "continuation", "diagnostics", "warnings");
        unhandledProperties.Should().Equal("ok", "error", "diagnostics", "reporting");
        runtimeEnvelopes["unhandled"]!["reporting"]!["canPrepare"]!.GetValue<bool>().Should().BeTrue();
    }

    [Fact]
    public async Task GIVEN_ExplicitBaselineUpdate_WHEN_GeneratingSecurityReference_THEN_ShouldWriteCanonicalBaseline()
    {
        using var directory = TemporaryDirectory.Create("roslyn-workbench-security-update-tests");
        var repositoryRoot = GetRepositoryRoot();
        var securityDirectory = Path.Combine(directory.DirectoryPath, "security");
        var schemasDirectory = Path.Combine(securityDirectory, "schemas");
        Directory.CreateDirectory(schemasDirectory);
        File.Copy(
            Path.Combine(repositoryRoot, "docs", "security", "schemas", "security-surface.schema.json"),
            Path.Combine(schemasDirectory, "security-surface.schema.json"));

        File.Copy(
            Path.Combine(repositoryRoot, "docs", "security", "schemas", "security-invariants.schema.json"),
            Path.Combine(schemasDirectory, "security-invariants.schema.json"));

        File.Copy(
            Path.Combine(repositoryRoot, "docs", "security", "security-invariants.json"),
            Path.Combine(securityDirectory, "security-invariants.json"));

        var baselineFile = Path.Combine(securityDirectory, "security-surface-v1.json");
        var options = new SecurityReferenceGeneratorOptions
        {
            OutputDirectory = Path.Combine(directory.DirectoryPath, "content", "reference", "security"),
            BaselineFile = baselineFile,
            ManifestFile = Path.Combine(securityDirectory, "security-invariants.json"),
            RepositoryRoot = repositoryRoot,
            SourceRevision = _sourceRevision,
            UpdateBaseline = true,
        };

        await SecurityReferenceGenerator.GenerateAsync(options, TestContext.Current.CancellationToken);

        File.Exists(baselineFile).Should().BeTrue();
        var baseline = await File.ReadAllTextAsync(baselineFile, TestContext.Current.CancellationToken);
        baseline.Should().Contain("roslyn-workbench-security-surface/v1");
    }

    [Fact]
    public async Task GIVEN_MissingBaseline_WHEN_CheckingSecurityReference_THEN_ShouldRejectGeneration()
    {
        using var directory = TemporaryDirectory.Create("roslyn-workbench-security-baseline-tests");
        var repositoryRoot = GetRepositoryRoot();
        var baselineFile = Path.Combine(directory.DirectoryPath, "security-surface-v1.json");
        var options = new SecurityReferenceGeneratorOptions
        {
            OutputDirectory = Path.Combine(directory.DirectoryPath, "content", "reference", "security"),
            BaselineFile = baselineFile,
            ManifestFile = Path.Combine(repositoryRoot, "docs", "security", "security-invariants.json"),
            RepositoryRoot = repositoryRoot,
            SourceRevision = _sourceRevision,
        };

        var action = async () => await SecurityReferenceGenerator.GenerateAsync(
            options,
            TestContext.Current.CancellationToken);

        await action.Should().ThrowAsync<FileNotFoundException>()
            .WithMessage("*baseline was not found*");
    }

    [Fact]
    public async Task GIVEN_ChangedBaseline_WHEN_CheckingSecurityReference_THEN_ShouldRejectGeneration()
    {
        using var directory = TemporaryDirectory.Create("roslyn-workbench-security-baseline-tests");
        var repositoryRoot = GetRepositoryRoot();
        var baselineFile = Path.Combine(directory.DirectoryPath, "security-surface-v1.json");
        await File.WriteAllTextAsync(baselineFile, "{}", TestContext.Current.CancellationToken);

        var options = new SecurityReferenceGeneratorOptions
        {
            OutputDirectory = Path.Combine(directory.DirectoryPath, "content", "reference", "security"),
            BaselineFile = baselineFile,
            ManifestFile = Path.Combine(repositoryRoot, "docs", "security", "security-invariants.json"),
            RepositoryRoot = repositoryRoot,
            SourceRevision = _sourceRevision,
        };

        var action = async () => await SecurityReferenceGenerator.GenerateAsync(
            options,
            TestContext.Current.CancellationToken);

        await action.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*compiled security surface differs*");
    }

    [Fact]
    public void GIVEN_UnsafeOutputDirectory_WHEN_WritingSecurityReference_THEN_ShouldRejectTarget()
    {
        using var directory = TemporaryDirectory.Create("roslyn-workbench-security-writer-tests");
        var manifest = new SecurityInvariantManifest
        {
            Format = "roslyn-workbench-security-invariants/v1",
            Entries = [],
        };

        var action = () => SecurityReferenceWriter.Write(
            Path.Combine(directory.DirectoryPath, "unsafe"),
            manifest,
            [],
            _sourceRevision);

        action.Should().Throw<InvalidOperationException>()
            .WithMessage("*content/reference/security*");
    }

    [Fact]
    public void GIVEN_ExistingUnmanagedOutput_WHEN_WritingSecurityReference_THEN_ShouldPreserveIt()
    {
        using var directory = TemporaryDirectory.Create("roslyn-workbench-security-writer-tests");
        var outputDirectory = Path.Combine(directory.DirectoryPath, "content", "reference", "security");
        Directory.CreateDirectory(outputDirectory);
        var unmanagedFile = Path.Combine(outputDirectory, "unmanaged.txt");
        File.WriteAllText(unmanagedFile, "retain");

        var manifest = new SecurityInvariantManifest
        {
            Format = "roslyn-workbench-security-invariants/v1",
            Entries = [],
        };

        SecurityReferenceWriter.Write(outputDirectory, manifest, [], _sourceRevision);

        File.ReadAllText(unmanagedFile).Should().Be("retain");
    }

    private static string GetRepositoryRoot()
    {
        return typeof(SecurityReferenceGeneratorIntegrationTests).Assembly
            .GetCustomAttributes<AssemblyMetadataAttribute>()
            .Single(static attribute => attribute.Key == "RepositoryRoot")
            .Value
            ?? throw new InvalidOperationException("RepositoryRoot assembly metadata was not configured.");
    }
}
