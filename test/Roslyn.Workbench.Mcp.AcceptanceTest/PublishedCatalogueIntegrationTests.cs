namespace Roslyn.Workbench.Mcp.AcceptanceTest;

public sealed class PublishedCatalogueIntegrationTests
{
    private static readonly string[] _mutationToolNames =
    [
        "format-document",
        "rename-symbol",
        "stage-code-action",
        "transaction-commit",
        "transaction-history",
        "transaction-preview",
        "transaction-review",
        "transaction-rollback",
        "transaction-start",
        "transaction-validate",
    ];

    [Theory]
    [InlineData("Full", "tools-list-v1-full.json")]
    [InlineData("Omit", "tools-list-v1-omit.json")]
    public async Task GIVEN_BuiltInCatalogue_WHEN_ListingTools_THEN_ShouldMatchCanonicalCompatibilityBaseline(
        string outputSchemaMode,
        string baselineFileName)
    {
        var catalogues = await ListCompatibilityCataloguesAsync(outputSchemaMode);
        var catalogueMap = catalogues.ToDictionary(
            static catalogue => catalogue.Name,
            static catalogue => catalogue.Tools,
            StringComparer.Ordinal);

        var actual = ToolCatalogueCanonicalizer.Create(catalogueMap);
        AssertExactAvailability(catalogues);
        if (CaptureCompatibilityBaselineWhenRequested(baselineFileName, actual))
        {
            return;
        }

        var baselinePath = Path.Combine(AppContext.BaseDirectory, "CompatibilityBaselines", baselineFileName);
        var expectedBaseline = await File.ReadAllTextAsync(baselinePath, TestContext.Current.CancellationToken);
        var expected = expectedBaseline.ReplaceLineEndings("\r\n");

        actual.Should().Be(expected);
    }

    [Fact]
    public void GIVEN_UnclassifiedProtocolField_WHEN_CanonicalisingTool_THEN_ShouldRejectContract()
    {
        var protocolTool = new System.Text.Json.Nodes.JsonObject
        {
            ["name"] = "tool-name",
            ["futureField"] = true,
        };

        var action = () => ToolCatalogueCanonicalizer.CreateContract(protocolTool, "tool-name");

        action.Should().Throw<InvalidOperationException>()
            .WithMessage("*tool-name*futureField*");
    }

    [Fact]
    public async Task GIVEN_WorkspaceLifecycleChanges_WHEN_ListingTools_THEN_ShouldKeepCatalogueAndMetadataStable()
    {
        await using var target = await AcceptanceProcessFixture.StartPublishedHostAsync(
            TestContext.Current.CancellationToken,
            pluginAssets:
            [
                AcceptancePluginAsset.HostQuery,
                AcceptancePluginAsset.HostMutation,
            ]);

        try
        {
            var initialTools = await target.ListToolsAsync(TestContext.Current.CancellationToken);
            AssertRepresentativeMetadata(initialTools);

            var openResult = await OpenWorkspaceAsync(target, Path.Combine(target.WorkspaceRoot, "Sample.csproj"));
            var workspace = AcceptanceWorkspaceIdentity.FromOpenResult(openResult);
            var workspaceSelector = workspace.CreateSelector();

            await target.CallToolAsync(
                "transaction-start",
                new Dictionary<string, object?>
                {
                    ["workspace"] = workspaceSelector,
                },
                TestContext.Current.CancellationToken);

            var transactionTools = await target.ListToolsAsync(TestContext.Current.CancellationToken);

            await target.CallToolAsync(
                "transaction-rollback",
                new Dictionary<string, object?>
                {
                    ["workspace"] = workspaceSelector,
                },
                TestContext.Current.CancellationToken);
            await target.CallToolAsync(
                "workspace-close",
                new Dictionary<string, object?>
                {
                    ["workspace"] = workspaceSelector,
                },
                TestContext.Current.CancellationToken);

            var closedTools = await target.ListToolsAsync(TestContext.Current.CancellationToken);
            CreateCatalogueFingerprint(transactionTools).Should().Equal(CreateCatalogueFingerprint(initialTools));
            CreateCatalogueFingerprint(closedTools).Should().Equal(CreateCatalogueFingerprint(initialTools));
        }
        catch
        {
            target.RetainRootOnFailure();
            throw;
        }
    }

    [Fact]
    public async Task GIVEN_BoundedQuery_WHEN_UsingDefaultZeroAndLowLimits_THEN_ShouldApplyStablePrefixes()
    {
        await using var target = await AcceptanceProcessFixture.StartPublishedHostAsync(
            TestContext.Current.CancellationToken,
            workspaceAsset: AcceptanceWorkspaceAsset.SolutionHierarchy);

        try
        {
            var openResult = await OpenWorkspaceAsync(target, Path.Combine(target.WorkspaceRoot, "Sample.slnx"));
            var workspaceSelector = AcceptanceWorkspaceIdentity.FromOpenResult(openResult).CreateSelector();

            var defaultResult = await SearchSymbolsAsync(target, workspaceSelector, limit: null);
            var zeroResult = await SearchSymbolsAsync(target, workspaceSelector, limit: 0);
            var firstLowResult = await SearchSymbolsAsync(target, workspaceSelector, limit: 1);
            var secondLowResult = await SearchSymbolsAsync(target, workspaceSelector, limit: 1);

            var defaultSymbols = GetSymbols(defaultResult);
            var zeroSymbols = GetSymbols(zeroResult);
            var firstLowSymbols = GetSymbols(firstLowResult);
            var secondLowSymbols = GetSymbols(secondLowResult);

            defaultSymbols.GetProperty("items").GetArrayLength().Should().BeGreaterThan(1);
            defaultSymbols.GetProperty("hasMore").GetBoolean().Should().BeFalse();
            zeroSymbols.GetProperty("items").GetArrayLength().Should().Be(0);
            zeroSymbols.GetProperty("hasMore").GetBoolean().Should().BeTrue();
            firstLowSymbols.GetProperty("items").GetArrayLength().Should().Be(1);
            firstLowSymbols.GetProperty("hasMore").GetBoolean().Should().BeTrue();
            firstLowSymbols.GetProperty("items")[0].GetRawText().Should().Be(defaultSymbols.GetProperty("items")[0].GetRawText());
            secondLowSymbols.GetRawText().Should().Be(firstLowSymbols.GetRawText());
        }
        catch
        {
            target.RetainRootOnFailure();
            throw;
        }
    }

    private static async Task<ModelContextProtocol.Protocol.CallToolResult> OpenWorkspaceAsync(
        AcceptanceProcessFixture target,
        string path)
    {
        return await target.CallToolAsync(
            "workspace-open",
            new Dictionary<string, object?>
            {
                ["path"] = path,
                ["workspaceRoot"] = target.WorkspaceRoot,
            },
            TestContext.Current.CancellationToken);
    }

    private static async Task<ModelContextProtocol.Protocol.CallToolResult> SearchSymbolsAsync(
        AcceptanceProcessFixture target,
        IReadOnlyDictionary<string, object?> workspaceSelector,
        int? limit)
    {
        var arguments = new Dictionary<string, object?>
        {
            ["workspace"] = workspaceSelector,
            ["query"] = "App",
        };
        if (limit is not null)
        {
            arguments["symbolsLimit"] = limit.Value;
        }

        return await target.CallToolAsync(
            "search-symbols",
            arguments,
            TestContext.Current.CancellationToken);
    }

    private static System.Text.Json.JsonElement GetSymbols(ModelContextProtocol.Protocol.CallToolResult result)
    {
        result.IsError.Should().NotBeTrue();
        return AcceptanceProtocol.GetSuccessData(result).GetProperty("symbols");
    }

    private static string[] CreateCatalogueFingerprint(IList<ModelContextProtocol.Client.McpClientTool> tools)
    {
        return tools
            .OrderBy(static tool => tool.Name, StringComparer.Ordinal)
            .Select(static tool => $"{tool.Name}|{tool.ProtocolTool.Description}|{tool.ProtocolTool.InputSchema.GetRawText()}")
            .ToArray();
    }

    private static async Task<IReadOnlyList<CompatibilityCatalogue>> ListCompatibilityCataloguesAsync(
        string outputSchemaMode)
    {
        var configurations = new[]
        {
            new CompatibilityConfiguration("inspection-only", "inspection-only", false),
            new CompatibilityConfiguration("transactional", "transactional", false),
            new CompatibilityConfiguration("approval-required", "approval-required", false),
            new CompatibilityConfiguration("autonomous-trusted", "autonomous-trusted", false),
            new CompatibilityConfiguration("compiler-validation", "autonomous-trusted", true),
        };

        var catalogues = new List<CompatibilityCatalogue>();
        foreach (var configuration in configurations)
        {
            var additionalArguments = CreateCatalogueArguments(outputSchemaMode, configuration.CompilerValidation);
            await using var target = await AcceptanceProcessFixture.StartPublishedHostAsync(
                TestContext.Current.CancellationToken,
                additionalArguments: additionalArguments,
                operationalMode: configuration.OperationalMode);

            try
            {
                var tools = await target.ListToolsAsync(TestContext.Current.CancellationToken);
                catalogues.Add(new CompatibilityCatalogue(configuration.Name, tools));
            }
            catch
            {
                target.RetainRootOnFailure();
                throw;
            }
        }

        return catalogues;
    }

    private static List<string> CreateCatalogueArguments(string outputSchemaMode, bool compilerValidation)
    {
        var arguments = new List<string>
        {
            "--tool-output-schema-mode",
            outputSchemaMode,
        };

        if (compilerValidation)
        {
            arguments.Add("--commit-validation");
            arguments.Add("no-new-compiler-errors");
        }

        return arguments;
    }

    private static void AssertExactAvailability(
        IReadOnlyList<CompatibilityCatalogue> catalogues)
    {
        var unionNames = catalogues
            .SelectMany(static catalogue => catalogue.Tools)
            .Select(static tool => tool.Name)
            .ToHashSet(StringComparer.Ordinal);

        AssertCatalogueEquals(
            catalogues,
            "inspection-only",
            unionNames.Except(_mutationToolNames, StringComparer.Ordinal));

        AssertCatalogueEquals(
            catalogues,
            "transactional",
            unionNames.Except(["transaction-review", "transaction-validate"], StringComparer.Ordinal));

        AssertCatalogueEquals(
            catalogues,
            "approval-required",
            unionNames.Except(["transaction-preview", "transaction-validate"], StringComparer.Ordinal));

        AssertCatalogueEquals(
            catalogues,
            "autonomous-trusted",
            unionNames.Except(["transaction-review", "transaction-validate"], StringComparer.Ordinal));

        AssertCatalogueEquals(
            catalogues,
            "compiler-validation",
            unionNames.Except(["transaction-review"], StringComparer.Ordinal));
    }

    private static void AssertCatalogueEquals(
        IReadOnlyList<CompatibilityCatalogue> catalogues,
        string catalogueName,
        IEnumerable<string> expectedNames)
    {
        var catalogue = catalogues.Single(item => item.Name == catalogueName);
        var actualNames = catalogue.Tools.Select(static tool => tool.Name).Order(StringComparer.Ordinal);
        var orderedExpectedNames = expectedNames.Order(StringComparer.Ordinal);

        actualNames.Should().Equal(orderedExpectedNames);
    }

    private static void AssertRepresentativeMetadata(IList<ModelContextProtocol.Client.McpClientTool> tools)
    {
        foreach (var toolName in new[] { "workspace-list", "search-symbols", "rename-symbol", "list-code-actions", "host-valid-query" })
        {
            var tool = tools.Single(item => item.Name == toolName);
            tool.ProtocolTool.Description.Should().NotBeNullOrWhiteSpace();
            tool.ProtocolTool.InputSchema.GetProperty("type").GetString().Should().Be("object");
            tool.ProtocolTool.Annotations.Should().NotBeNull();
            tool.ProtocolTool.Annotations!.Title.Should().NotBeNullOrWhiteSpace();
        }
    }

    private static bool CaptureCompatibilityBaselineWhenRequested(string baselineFileName, string contents)
    {
        var captureDirectory = Environment.GetEnvironmentVariable("ROSLYN_WORKBENCH_CAPTURE_TOOL_BASELINES");
        if (string.IsNullOrWhiteSpace(captureDirectory))
        {
            return false;
        }

        Directory.CreateDirectory(captureDirectory);
        File.WriteAllText(Path.Combine(captureDirectory, baselineFileName), contents);
        return true;
    }

    private sealed record CompatibilityConfiguration(string Name, string OperationalMode, bool CompilerValidation);

    private sealed record CompatibilityCatalogue(
        string Name,
        IList<ModelContextProtocol.Client.McpClientTool> Tools);
}
