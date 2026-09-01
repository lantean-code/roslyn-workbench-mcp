using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using Json.Schema;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using ModelContextProtocol;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using Roslyn.Workbench.Mcp.Hosting;
using Roslyn.Workbench.Mcp.PluginLoading;

namespace Roslyn.Workbench.Mcp.ToolReferenceGenerator;

/// <summary>
/// Generates deterministic human-readable and machine-readable reference files from the production Host composition.
/// </summary>
internal sealed class ToolReferenceGenerator
{
    private const string _commitMetadataKey = "RoslynWorkbenchCommitSha";
    private const string _formatVersion = "roslyn-workbench-tool-reference/v1";
    private const string _pluginDirectoryEnvironmentVariable = "ROSLYN_WORKBENCH_MCP_PLUGIN_DIRECTORY";
    private const string _sourceTagMetadataKey = "RoslynWorkbenchSourceTag";

    /// <summary>
    /// Generates the complete reference into the configured output directory.
    /// </summary>
    /// <param name="options">The output and authored-example inputs.</param>
    /// <param name="cancellationToken">The token used to cancel generation.</param>
    /// <returns>A task that completes after all reference files have been written.</returns>
    public async Task GenerateAsync(
        ToolReferenceGeneratorOptions options,
        CancellationToken cancellationToken)
    {
        var examples = LoadExamples(options.ExamplesFile);
        var outputDirectory = options.OutputDirectory;
        RecreateOutputDirectory(outputDirectory);

        var stateDirectory = Path.Combine(Path.GetTempPath(), $"roslyn-workbench-tool-reference-{Guid.NewGuid():N}");
        Directory.CreateDirectory(stateDirectory);

        var previousPluginDirectory = Environment.GetEnvironmentVariable(_pluginDirectoryEnvironmentVariable);
        Environment.SetEnvironmentVariable(_pluginDirectoryEnvironmentVariable, null);

        try
        {
            var entries = await ComposeEntriesAsync(stateDirectory, examples, cancellationToken);
            var identity = ReadBuildIdentity();
            ValidateExamples(entries, examples);

            ToolReferenceWriter.Write(outputDirectory, identity, _formatVersion, entries);
        }
        finally
        {
            Environment.SetEnvironmentVariable(_pluginDirectoryEnvironmentVariable, previousPluginDirectory);
            Directory.Delete(stateDirectory, recursive: true);
        }
    }

    private static async Task<IReadOnlyList<ToolReferenceEntry>> ComposeEntriesAsync(
        string stateDirectory,
        IReadOnlyList<ToolReferenceExample> examples,
        CancellationToken cancellationToken)
    {
        var builder = Host.CreateApplicationBuilder();
        builder.AddRoslynWorkbench(
        [
            "--state-directory",
            stateDirectory,
            "--tool-output-schema-mode",
            "Full",
        ]);

        await using var serviceProvider = builder.Services.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateOnBuild = true,
            ValidateScopes = true,
        });

        var pluginStartup = serviceProvider.GetServices<IHostedService>()
            .OfType<PluginCatalogStartupLifecycleService>()
            .Single();
        await pluginStartup.StartingAsync(cancellationToken);

        var tools = new List<Tool>();
        foreach (var serverTool in serviceProvider.GetServices<McpServerTool>())
        {
            tools.Add(serverTool.ProtocolTool);
        }

        var pluginCatalog = serviceProvider.GetRequiredService<IPluginCatalogState>().Current;
        foreach (var pluginTool in pluginCatalog.Tools.Values)
        {
            tools.Add(pluginTool.ProtocolTool);
        }

        tools.Sort(static (left, right) => StringComparer.Ordinal.Compare(left.Name, right.Name));
        EnsureUniqueNames(tools);

        var entries = new List<ToolReferenceEntry>(tools.Count);
        foreach (var tool in tools)
        {
            var matchingExamples = examples
                .Where(example => StringComparer.Ordinal.Equals(example.Tool, tool.Name))
                .ToArray();
            entries.Add(CreateEntry(tool, matchingExamples));
        }

        return entries;
    }

    private static IReadOnlyList<ToolReferenceExample> LoadExamples(string file)
    {
        if (!File.Exists(file))
        {
            throw new FileNotFoundException("The canonical tool-reference examples file was not found.", file);
        }

        var root = JsonNode.Parse(File.ReadAllText(file)) as JsonObject
            ?? throw new InvalidOperationException("The canonical examples document must be a JSON object.");
        var exampleNodes = root["examples"] as JsonArray
            ?? throw new InvalidOperationException("The canonical examples document must contain an examples array.");

        var examples = new List<ToolReferenceExample>(exampleNodes.Count);
        foreach (var exampleNode in exampleNodes)
        {
            var example = exampleNode as JsonObject
                ?? throw new InvalidOperationException("Every canonical example must be a JSON object.");
            examples.Add(new ToolReferenceExample
            {
                WorkflowId = ReadRequiredString(example, "workflowId"),
                WorkflowTitle = ReadRequiredString(example, "workflowTitle"),
                Step = example["step"]?.GetValue<int>()
                    ?? throw new InvalidOperationException("Every canonical example must contain a numeric step."),
                Id = ReadRequiredString(example, "id"),
                Title = ReadRequiredString(example, "title"),
                Purpose = ReadRequiredString(example, "purpose"),
                Tool = ReadRequiredString(example, "tool"),
                ExpectedOutcome = ReadRequiredString(example, "expectedOutcome"),
                RepresentativeResponse = ReadOptionalObject(example, "representativeResponse"),
                Request = example["request"]?.DeepClone() as JsonObject
                    ?? throw new InvalidOperationException("Every canonical example must contain an object request."),
            });
        }

        var duplicate = examples.GroupBy(static example => example.Id, StringComparer.Ordinal)
            .FirstOrDefault(static group => group.Count() > 1);
        if (duplicate is not null)
        {
            throw new InvalidOperationException($"Canonical example id '{duplicate.Key}' is duplicated.");
        }

        foreach (var workflow in examples.GroupBy(static example => example.WorkflowId, StringComparer.Ordinal))
        {
            var orderedSteps = workflow.Select(static example => example.Step).Order().ToArray();
            var expectedSteps = Enumerable.Range(1, orderedSteps.Length).ToArray();
            if (!orderedSteps.SequenceEqual(expectedSteps))
            {
                throw new InvalidOperationException($"Canonical workflow '{workflow.Key}' must use consecutive one-based step numbers.");
            }

            if (workflow.Select(static example => example.WorkflowTitle).Distinct(StringComparer.Ordinal).Count() != 1)
            {
                throw new InvalidOperationException($"Canonical workflow '{workflow.Key}' must use one title.");
            }
        }

        return examples;
    }

    private static void ValidateExamples(
        IReadOnlyList<ToolReferenceEntry> entries,
        IReadOnlyList<ToolReferenceExample> examples)
    {
        var entriesByName = entries.ToDictionary(static entry => entry.Name, StringComparer.Ordinal);
        if (examples.Count == 0)
        {
            throw new InvalidOperationException("At least one canonical tool example is required.");
        }

        foreach (var example in examples)
        {
            if (!entriesByName.TryGetValue(example.Tool, out var entry))
            {
                throw new InvalidOperationException($"Canonical example '{example.Id}' refers to unknown tool '{example.Tool}'.");
            }

            var schemaNode = entry.ProtocolTool["inputSchema"]
                ?? throw new InvalidOperationException($"Tool '{entry.Name}' does not contain an input schema.");
            using var schemaDocument = JsonDocument.Parse(schemaNode.ToJsonString());
            using var requestDocument = JsonDocument.Parse(example.Request.ToJsonString());
            var schema = JsonSchema.Build(schemaDocument.RootElement);
            var evaluation = schema.Evaluate(requestDocument.RootElement);
            if (!evaluation.IsValid)
            {
                throw new InvalidOperationException($"Canonical example '{example.Id}' does not satisfy the input schema for tool '{example.Tool}'.");
            }
        }

        ValidateWorkflowSemantics(examples);
    }

    private static void ValidateWorkflowSemantics(IReadOnlyList<ToolReferenceExample> examples)
    {
        foreach (var workflow in examples.GroupBy(static example => example.WorkflowId, StringComparer.Ordinal))
        {
            var transactionStarted = false;
            var changeStaged = false;
            var codeActionsListed = false;

            foreach (var example in workflow.OrderBy(static example => example.Step))
            {
                switch (example.Tool)
                {
                    case "transaction-start":
                        transactionStarted = true;
                        changeStaged = false;
                        break;
                    case "format-document":
                    case "rename-symbol":
                        EnsureTransactionStarted(workflow.Key, example, transactionStarted);
                        changeStaged = true;
                        break;
                    case "list-code-actions":
                        codeActionsListed = true;
                        break;
                    case "stage-code-action":
                        EnsureTransactionStarted(workflow.Key, example, transactionStarted);
                        if (!codeActionsListed)
                        {
                            throw new InvalidOperationException($"Canonical workflow '{workflow.Key}' must list Code Actions before staging one.");
                        }

                        changeStaged = true;
                        break;
                    case "transaction-preview":
                    case "transaction-commit":
                        EnsureTransactionStarted(workflow.Key, example, transactionStarted);
                        if (!changeStaged)
                        {
                            throw new InvalidOperationException($"Canonical workflow '{workflow.Key}' must stage a change before calling '{example.Tool}'.");
                        }

                        break;
                }
            }
        }
    }

    private static void EnsureTransactionStarted(
        string workflowId,
        ToolReferenceExample example,
        bool transactionStarted)
    {
        if (!transactionStarted)
        {
            throw new InvalidOperationException($"Canonical workflow '{workflowId}' must start a transaction before calling '{example.Tool}'.");
        }
    }

    private static ToolReferenceBuildIdentity ReadBuildIdentity()
    {
        var assembly = typeof(HostStartupComposer).Assembly;
        var productVersion = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
        var metadata = assembly.GetCustomAttributes<AssemblyMetadataAttribute>()
            .ToDictionary(static attribute => attribute.Key, static attribute => attribute.Value, StringComparer.Ordinal);

        if (string.IsNullOrWhiteSpace(productVersion)
            || !metadata.TryGetValue(_sourceTagMetadataKey, out var sourceTag)
            || string.IsNullOrWhiteSpace(sourceTag)
            || !metadata.TryGetValue(_commitMetadataKey, out var commit)
            || string.IsNullOrWhiteSpace(commit))
        {
            throw new InvalidOperationException("The compiled Host does not contain complete tool-reference build identity metadata.");
        }

        return new ToolReferenceBuildIdentity
        {
            ProductVersion = productVersion,
            SourceTag = sourceTag,
            Commit = commit,
        };
    }

    private static ToolReferenceEntry CreateEntry(
        Tool tool,
        IReadOnlyList<ToolReferenceExample> examples)
    {
        if (tool.OutputSchema is null)
        {
            throw new InvalidOperationException($"Tool '{tool.Name}' does not publish its full output schema.");
        }

        var protocolToolElement = JsonSerializer.SerializeToElement(tool, McpJsonUtilities.DefaultOptions);
        var protocolTool = JsonObject.Create(protocolToolElement)
            ?? throw new InvalidOperationException($"Tool '{tool.Name}' could not be serialized as an object.");
        return new ToolReferenceEntry
        {
            Name = tool.Name,
            Title = tool.Title ?? tool.Annotations?.Title ?? throw new InvalidOperationException($"Tool '{tool.Name}' does not publish a title."),
            Area = ToolReferenceMetadata.GetArea(tool.Name),
            Category = ToolReferenceMetadata.GetCategory(tool.Name),
            OperationKind = tool.Annotations?.ReadOnlyHint == true ? "Query" : "Mutation",
            Summary = ExtractSummary(tool),
            Availability = ToolReferenceMetadata.GetAvailability(tool.Name),
            ProtocolTool = protocolTool,
            Examples = examples,
        };
    }

    private static string ExtractSummary(Tool tool)
    {
        var description = tool.Description;
        if (string.IsNullOrWhiteSpace(description))
        {
            throw new InvalidOperationException($"Tool '{tool.Name}' does not publish a description.");
        }

        var inputIndex = description.IndexOf(" Input:", StringComparison.Ordinal);
        var resultIndex = description.IndexOf(" Result:", StringComparison.Ordinal);
        var endIndex = new[] { inputIndex, resultIndex }
            .Where(static index => index >= 0)
            .DefaultIfEmpty(description.Length)
            .Min();

        return description[..endIndex];
    }

    private static string ReadRequiredString(JsonObject value, string propertyName)
    {
        var result = value[propertyName]?.GetValue<string>();
        return string.IsNullOrWhiteSpace(result)
            ? throw new InvalidOperationException($"Canonical example property '{propertyName}' is required.")
            : result;
    }

    private static JsonObject? ReadOptionalObject(JsonObject value, string propertyName)
    {
        var node = value[propertyName];
        return node switch
        {
            null => null,
            JsonObject objectValue => objectValue.DeepClone().AsObject(),
            _ => throw new InvalidOperationException($"Canonical example property '{propertyName}' must be a JSON object when supplied."),
        };
    }

    private static void EnsureUniqueNames(IReadOnlyList<Tool> tools)
    {
        var duplicate = tools.GroupBy(static tool => tool.Name, StringComparer.Ordinal)
            .FirstOrDefault(static group => group.Count() > 1);
        if (duplicate is not null)
        {
            throw new InvalidOperationException($"Production composition publishes duplicate tool name '{duplicate.Key}'.");
        }
    }

    private static void RecreateOutputDirectory(string outputDirectory)
    {
        var directory = new DirectoryInfo(outputDirectory);
        if (!StringComparer.Ordinal.Equals(directory.Name, "tools")
            || directory.Parent is null
            || !StringComparer.Ordinal.Equals(directory.Parent.Name, "reference"))
        {
            throw new InvalidOperationException("The generated output directory must end with 'reference/tools'.");
        }

        if (Directory.Exists(outputDirectory))
        {
            Directory.Delete(outputDirectory, recursive: true);
        }

        Directory.CreateDirectory(outputDirectory);
    }
}
