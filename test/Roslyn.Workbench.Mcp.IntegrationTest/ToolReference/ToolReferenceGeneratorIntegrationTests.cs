using System.Reflection;
using System.Text.Json;
using Json.Schema;
using Roslyn.Workbench.Mcp.IntegrationTestSupport;
using Roslyn.Workbench.Mcp.ToolReferenceGenerator;

namespace Roslyn.Workbench.Mcp.Test.ToolReference;

[Collection(ToolReferenceGenerationCollectionDefinition.Name)]
[Trait("Category", "Integration")]
public sealed class ToolReferenceGeneratorIntegrationTests
{
    [Fact]
    public async Task GIVEN_ValidCommandLine_WHEN_RunningGeneratorProgram_THEN_ShouldReturnSuccess()
    {
        using var directory = TemporaryDirectory.Create("roslyn-workbench-tool-reference-tests");
        var repositoryRoot = GetRepositoryRoot();
        var examplesFile = Path.Combine(repositoryRoot, "docs", "examples", "tool-reference-examples.json");
        var outputDirectory = Path.Combine(directory.DirectoryPath, "reference", "tools");

        var exitCode = await Roslyn.Workbench.Mcp.ToolReferenceGenerator.Program.Main(
        [
            "--output",
            outputDirectory,
            "--examples",
            examplesFile,
        ]);

        exitCode.Should().Be(0);
        File.Exists(Path.Combine(outputDirectory, "catalog.json")).Should().BeTrue();
    }

    [Fact]
    public async Task GIVEN_InvalidCommandLine_WHEN_RunningGeneratorProgram_THEN_ShouldReturnFailure()
    {
        var exitCode = await Roslyn.Workbench.Mcp.ToolReferenceGenerator.Program.Main(["--unknown", "value"]);

        exitCode.Should().Be(1);
    }

    [Fact]
    public async Task GIVEN_ProductionHostAndCanonicalExamples_WHEN_GeneratingTwice_THEN_ShouldProduceCompleteDeterministicReference()
    {
        var expectedVersion = typeof(HostAssemblyMarker).Assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;

        expectedVersion.Should().NotBeNullOrWhiteSpace();
        var documentationVersion = expectedVersion == "0.0.0-dev" ? "dev" : expectedVersion;
        using var directory = TemporaryDirectory.Create("roslyn-workbench-tool-reference-tests");
        var repositoryRoot = GetRepositoryRoot();
        var examplesFile = Path.Combine(repositoryRoot, "docs", "examples", "tool-reference-examples.json");
        var firstOutput = Path.Combine(directory.DirectoryPath, "first", "reference", "tools");
        var secondOutput = Path.Combine(directory.DirectoryPath, "second", "reference", "tools");
        var generator = new ToolReferenceGenerator.ToolReferenceGenerator();
        Directory.CreateDirectory(firstOutput);
        await File.WriteAllTextAsync(
            Path.Combine(firstOutput, "stale-file.txt"),
            "stale",
            TestContext.Current.CancellationToken);

        await generator.GenerateAsync(CreateOptions(firstOutput, examplesFile), TestContext.Current.CancellationToken);
        await generator.GenerateAsync(CreateOptions(secondOutput, examplesFile), TestContext.Current.CancellationToken);

        var firstFiles = ReadGeneratedFiles(firstOutput);
        var secondFiles = ReadGeneratedFiles(secondOutput);
        firstFiles.Keys.Should().Equal(secondFiles.Keys);
        foreach (var file in firstFiles)
        {
            secondFiles[file.Key].Should().Equal(file.Value);
        }

        using var catalog = JsonDocument.Parse(firstFiles["catalog.json"]);
        catalog.RootElement.GetProperty("productVersion").GetString().Should().Be(expectedVersion);
        catalog.RootElement.GetProperty("sourceTag").GetString().Should().Be(expectedVersion);
        using var catalogSchemaDocument = JsonDocument.Parse(firstFiles["schemas/tool-catalog.schema.json"]);
        catalogSchemaDocument.RootElement.GetProperty("$id").GetString().Should().Be("tool-catalog.schema.json");
        var catalogSchema = JsonSchema.Build(catalogSchemaDocument.RootElement);
        catalogSchema.Evaluate(catalog.RootElement).IsValid.Should().BeTrue();
        var tools = catalog.RootElement.GetProperty("tools").EnumerateArray().ToArray();
        tools.Should().HaveCount(56);
        tools.Select(static tool => tool.GetProperty("name").GetString()).Should().BeInAscendingOrder(StringComparer.Ordinal);
        tools.Select(static tool => tool.GetProperty("name").GetString()).Should().OnlyHaveUniqueItems();
        tools.Select(static tool => tool.GetProperty("area").GetString()).Should().Contain(["Server", "CorePlugin", "CodeAction"]);

        using var detailSchemaDocument = JsonDocument.Parse(firstFiles["schemas/tool-detail.schema.json"]);
        detailSchemaDocument.RootElement.GetProperty("$id").GetString().Should().Be("tool-detail.schema.json");
        var detailSchema = JsonSchema.Build(detailSchemaDocument.RootElement);
        foreach (var tool in tools)
        {
            var name = tool.GetProperty("name").GetString();
            name.Should().NotBeNullOrWhiteSpace();
            firstFiles.Should().ContainKey($"{name}.md");
            firstFiles.Should().ContainKey($"data/{name}.json");
            tool.GetProperty("documentationUrl").GetString().Should().Be($"https://lantean-code.github.io/roslyn-workbench-mcp/{documentationVersion}/reference/tools/{name}.html");

            using var detail = JsonDocument.Parse(firstFiles[$"data/{name}.json"]);
            detailSchema.Evaluate(detail.RootElement).IsValid.Should().BeTrue();
            detail.RootElement.GetProperty("name").GetString().Should().Be(name);
            detail.RootElement.GetProperty("documentationUrl").GetString().Should().Be(tool.GetProperty("documentationUrl").GetString());
            var protocolTool = detail.RootElement.GetProperty("tool");
            protocolTool.GetProperty("name").GetString().Should().Be(name);
            protocolTool.GetProperty("inputSchema").ValueKind.Should().Be(JsonValueKind.Object);
            protocolTool.GetProperty("outputSchema").ValueKind.Should().Be(JsonValueKind.Object);
        }
    }

    [Theory]
    [InlineData()]
    [InlineData("--output")]
    [InlineData("--unknown", "value")]
    public void GIVEN_InvalidArguments_WHEN_ParsingGeneratorOptions_THEN_ShouldRejectArguments(params string[] arguments)
    {
        var action = () => ToolReferenceGeneratorOptions.Parse(arguments);

        action.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void GIVEN_CompleteArguments_WHEN_ParsingGeneratorOptions_THEN_ShouldReturnAbsolutePaths()
    {
        var options = ToolReferenceGeneratorOptions.Parse(["--output", "output", "--examples", "examples.json"]);

        Path.IsPathFullyQualified(options.OutputDirectory).Should().BeTrue();
        Path.IsPathFullyQualified(options.ExamplesFile).Should().BeTrue();
    }

    [Fact]
    public void GIVEN_UnknownToolName_WHEN_ClassifyingTool_THEN_ShouldRejectName()
    {
        var action = () => ToolReferenceMetadata.GetCategory("unknown-tool");

        action.Should().Throw<InvalidOperationException>()
            .WithMessage("*unknown-tool*");
    }

    [Fact]
    public async Task GIVEN_MissingExamplesFile_WHEN_Generating_THEN_ShouldRejectGeneration()
    {
        using var directory = TemporaryDirectory.Create("roslyn-workbench-tool-reference-tests");
        var generator = new ToolReferenceGenerator.ToolReferenceGenerator();
        var options = CreateOptions(
            Path.Combine(directory.DirectoryPath, "output"),
            Path.Combine(directory.DirectoryPath, "missing.json"));

        var action = async () => await generator.GenerateAsync(options, TestContext.Current.CancellationToken);

        await action.Should().ThrowAsync<FileNotFoundException>();
    }

    [Fact]
    public async Task GIVEN_OutputOutsideReferenceToolsDirectory_WHEN_Generating_THEN_ShouldRejectDestructiveTarget()
    {
        using var directory = TemporaryDirectory.Create("roslyn-workbench-tool-reference-tests");
        var repositoryRoot = GetRepositoryRoot();
        var examplesFile = Path.Combine(repositoryRoot, "docs", "examples", "tool-reference-examples.json");
        var generator = new ToolReferenceGenerator.ToolReferenceGenerator();
        var options = CreateOptions(Path.Combine(directory.DirectoryPath, "unsafe"), examplesFile);

        var action = async () => await generator.GenerateAsync(options, TestContext.Current.CancellationToken);

        await action.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*reference/tools*");
    }

    [Fact]
    public async Task GIVEN_ExampleReferencesUnknownTool_WHEN_Generating_THEN_ShouldRejectExample()
    {
        using var directory = TemporaryDirectory.Create("roslyn-workbench-tool-reference-tests");
        var repositoryRoot = GetRepositoryRoot();
        var canonicalExamplesFile = Path.Combine(repositoryRoot, "docs", "examples", "tool-reference-examples.json");
        var examples = await File.ReadAllTextAsync(canonicalExamplesFile, TestContext.Current.CancellationToken);
        var unknownToolExamples = examples.Replace(
            "\"tool\": \"workspace-list\"",
            "\"tool\": \"unknown-tool\"",
            StringComparison.Ordinal);
        var examplesFile = Path.Combine(directory.DirectoryPath, "examples.json");
        await File.WriteAllTextAsync(examplesFile, unknownToolExamples, TestContext.Current.CancellationToken);
        var outputDirectory = Path.Combine(directory.DirectoryPath, "reference", "tools");
        var generator = new ToolReferenceGenerator.ToolReferenceGenerator();

        var action = async () => await generator.GenerateAsync(
            CreateOptions(outputDirectory, examplesFile),
            TestContext.Current.CancellationToken);

        await action.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*unknown-tool*");
    }

    [Fact]
    public async Task GIVEN_NoCanonicalExamples_WHEN_Generating_THEN_ShouldRejectGeneration()
    {
        using var directory = TemporaryDirectory.Create("roslyn-workbench-tool-reference-tests");
        var examplesFile = Path.Combine(directory.DirectoryPath, "examples.json");
        await File.WriteAllTextAsync(examplesFile, "{ \"examples\": [] }", TestContext.Current.CancellationToken);
        var outputDirectory = Path.Combine(directory.DirectoryPath, "reference", "tools");
        var generator = new ToolReferenceGenerator.ToolReferenceGenerator();

        var action = async () => await generator.GenerateAsync(
            CreateOptions(outputDirectory, examplesFile),
            TestContext.Current.CancellationToken);

        await action.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*At least one canonical tool example*");
    }

    [Fact]
    public async Task GIVEN_CanonicalExampleViolatesToolSchema_WHEN_Generating_THEN_ShouldRejectExample()
    {
        using var directory = TemporaryDirectory.Create("roslyn-workbench-tool-reference-tests");
        var examplesFile = await WriteModifiedExamplesAsync(
            directory.DirectoryPath,
            "\"request\": {}",
            "\"request\": { \"unexpected\": true }");
        var outputDirectory = Path.Combine(directory.DirectoryPath, "reference", "tools");
        var generator = new ToolReferenceGenerator.ToolReferenceGenerator();

        var action = async () => await generator.GenerateAsync(
            CreateOptions(outputDirectory, examplesFile),
            TestContext.Current.CancellationToken);

        await action.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*does not satisfy the input schema*");
    }

    [Fact]
    public async Task GIVEN_DuplicateCanonicalExampleId_WHEN_Generating_THEN_ShouldRejectExamples()
    {
        using var directory = TemporaryDirectory.Create("roslyn-workbench-tool-reference-tests");
        var examplesFile = await WriteModifiedExamplesAsync(
            directory.DirectoryPath,
            "\"id\": \"open-solution\"",
            "\"id\": \"list-open-workspaces\"");
        var generator = new ToolReferenceGenerator.ToolReferenceGenerator();

        var action = async () => await generator.GenerateAsync(
            CreateOptions(Path.Combine(directory.DirectoryPath, "reference", "tools"), examplesFile),
            TestContext.Current.CancellationToken);

        await action.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*duplicated*");
    }

    [Fact]
    public async Task GIVEN_NonConsecutiveCanonicalWorkflowSteps_WHEN_Generating_THEN_ShouldRejectExamples()
    {
        using var directory = TemporaryDirectory.Create("roslyn-workbench-tool-reference-tests");
        var examplesFile = await WriteModifiedExamplesAsync(
            directory.DirectoryPath,
            "\"step\": 1",
            "\"step\": 3");
        var generator = new ToolReferenceGenerator.ToolReferenceGenerator();

        var action = async () => await generator.GenerateAsync(
            CreateOptions(Path.Combine(directory.DirectoryPath, "reference", "tools"), examplesFile),
            TestContext.Current.CancellationToken);

        await action.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*consecutive one-based step numbers*");
    }

    [Fact]
    public async Task GIVEN_CanonicalWorkflowUsesDifferentTitles_WHEN_Generating_THEN_ShouldRejectExamples()
    {
        using var directory = TemporaryDirectory.Create("roslyn-workbench-tool-reference-tests");
        var examplesFile = await WriteModifiedExamplesAsync(
            directory.DirectoryPath,
            "\"workflowTitle\": \"Open and select a workspace\"",
            "\"workflowTitle\": \"Different title\"");
        var generator = new ToolReferenceGenerator.ToolReferenceGenerator();

        var action = async () => await generator.GenerateAsync(
            CreateOptions(Path.Combine(directory.DirectoryPath, "reference", "tools"), examplesFile),
            TestContext.Current.CancellationToken);

        await action.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*must use one title*");
    }

    private static ToolReferenceGeneratorOptions CreateOptions(string outputDirectory, string examplesFile)
    {
        return new ToolReferenceGeneratorOptions
        {
            OutputDirectory = outputDirectory,
            ExamplesFile = examplesFile,
        };
    }

    private static SortedDictionary<string, byte[]> ReadGeneratedFiles(string outputDirectory)
    {
        var files = new SortedDictionary<string, byte[]>(StringComparer.Ordinal);
        foreach (var file in Directory.EnumerateFiles(outputDirectory, "*", SearchOption.AllDirectories))
        {
            var relativePath = Path.GetRelativePath(outputDirectory, file).Replace(Path.DirectorySeparatorChar, '/');
            files.Add(relativePath, File.ReadAllBytes(file));
        }

        return files;
    }

    private static async Task<string> WriteModifiedExamplesAsync(
        string directory,
        string original,
        string replacement)
    {
        var repositoryRoot = GetRepositoryRoot();
        var canonicalExamplesFile = Path.Combine(repositoryRoot, "docs", "examples", "tool-reference-examples.json");
        var examples = await File.ReadAllTextAsync(canonicalExamplesFile, TestContext.Current.CancellationToken);
        var originalIndex = examples.IndexOf(original, StringComparison.Ordinal);
        originalIndex.Should().BeGreaterThanOrEqualTo(0);
        var modifiedExamples = string.Concat(
            examples.AsSpan(0, originalIndex),
            replacement,
            examples.AsSpan(originalIndex + original.Length));

        var examplesFile = Path.Combine(directory, "examples.json");
        await File.WriteAllTextAsync(examplesFile, modifiedExamples, TestContext.Current.CancellationToken);
        return examplesFile;
    }

    private static string GetRepositoryRoot()
    {
        var repositoryRoot = typeof(ToolReferenceGeneratorIntegrationTests).Assembly
            .GetCustomAttributes<AssemblyMetadataAttribute>()
            .Single(static attribute => attribute.Key == "RepositoryRoot")
            .Value;

        return repositoryRoot ?? throw new InvalidOperationException("RepositoryRoot assembly metadata was not configured.");
    }
}
