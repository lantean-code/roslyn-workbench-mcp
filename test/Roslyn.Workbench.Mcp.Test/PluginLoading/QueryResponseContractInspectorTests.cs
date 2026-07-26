using Roslyn.Workbench.Mcp.Plugins;

namespace Roslyn.Workbench.Mcp.Test.PluginLoading;

public sealed class QueryResponseContractInspectorTests
{
    [Theory]
    [InlineData(true, "mutation")]
    [InlineData(false, "list-code-actions")]
    public void GIVEN_ExcludedTool_WHEN_Inspecting_THEN_ShouldReturnNoDiagnostics(
        bool isMutation,
        string name)
    {
        var kind = isMutation ? ToolKind.Mutation : ToolKind.Query;
        var tool = CreateTool(kind, name, typeof(RawCollectionResponse));

        var result = QueryResponseContractInspector.Inspect(tool);

        result.Should().BeEmpty();
    }

    [Fact]
    public void GIVEN_BoundedAndScalarProperties_WHEN_Inspecting_THEN_ShouldReturnNoDiagnostics()
    {
        var tool = CreateTool(ToolKind.Query, "query", typeof(BoundedResponse));

        var result = QueryResponseContractInspector.Inspect(tool);

        result.Should().BeEmpty();
    }

    [Fact]
    public void GIVEN_BoundedCollectionResponse_WHEN_Inspecting_THEN_ShouldReturnNoDiagnostics()
    {
        var tool = CreateTool(
            ToolKind.Query,
            "query",
            typeof(BoundedCollection<string>));

        var result = QueryResponseContractInspector.Inspect(tool);

        result.Should().BeEmpty();
    }

    [Theory]
    [InlineData(typeof(string[]))]
    [InlineData(typeof(IReadOnlyList<string>))]
    [InlineData(typeof(ISet<string>))]
    [InlineData(typeof(IReadOnlyDictionary<string, string>))]
    [InlineData(typeof(IAsyncEnumerable<string>))]
    public void GIVEN_RawCollectionResponse_WHEN_Inspecting_THEN_ShouldReportDiagnostic(
        Type responseType)
    {
        var tool = CreateTool(ToolKind.Query, "query", responseType);

        var result = QueryResponseContractInspector.Inspect(tool);

        var diagnostic = result.Should().ContainSingle().Subject;
        diagnostic.Id.Should().Be("QueryResponseContract");
        diagnostic.Message.Should().Contain("unbounded collection response");
    }

    [Fact]
    public void GIVEN_RawTopLevelCollections_WHEN_Inspecting_THEN_ShouldReportEveryOffendingProperty()
    {
        var tool = CreateTool(ToolKind.Query, "query", typeof(RawCollectionResponse));

        var result = QueryResponseContractInspector.Inspect(tool);

        var diagnostic = result.Should().ContainSingle().Subject;
        diagnostic.Id.Should().Be("QueryResponseContract");
        diagnostic.Severity.Should().Be(DiagnosticSeverity.Warning);
        diagnostic.Message.Should().Contain("ArrayItems");
        diagnostic.Message.Should().Contain("ReadOnlyListItems");
        diagnostic.Message.Should().Contain("ReadOnlyCollectionItems");
        diagnostic.Message.Should().Contain("EnumerableItems");
        diagnostic.Message.Should().Contain("CollectionItems");
        diagnostic.Message.Should().Contain("ListInterfaceItems");
        diagnostic.Message.Should().Contain("ListItems");
        diagnostic.Message.Should().Contain("SetItems");
        diagnostic.Message.Should().Contain("DictionaryItems");
        diagnostic.Message.Should().Contain("AsyncItems");
    }

    private static RegisteredTool CreateTool(ToolKind kind, string name, Type responseType)
    {
        return new RegisteredTool
        {
            Metadata = new ToolRegistrationMetadata
            {
                Name = name,
            },
            Kind = kind,
            ResponseType = responseType,
        };
    }

#pragma warning disable CA1812 // Response fixtures are inspected through reflection metadata without construction.
    private sealed record BoundedResponse
    {
        public string Text { get; init; } = string.Empty;

        public int Count { get; init; }

        public BoundedCollection<string> BoundedItems { get; init; } = BoundedCollection.Empty<string>();
    }

    private sealed record RawCollectionResponse
    {
        public string[] ArrayItems { get; init; } = [];

        public IReadOnlyList<string> ReadOnlyListItems { get; init; } = [];

        public IReadOnlyCollection<string> ReadOnlyCollectionItems { get; init; } = [];

        public IEnumerable<string> EnumerableItems { get; init; } = [];

        public ICollection<string> CollectionItems { get; init; } = [];

        public IList<string> ListInterfaceItems { get; init; } = [];

        public List<string> ListItems { get; init; } = [];

        public ISet<string> SetItems { get; init; } = new HashSet<string>();

        public IReadOnlyDictionary<string, string> DictionaryItems { get; init; } =
            new Dictionary<string, string>();

        public IAsyncEnumerable<string>? AsyncItems { get; init; }
    }
#pragma warning restore CA1812
}
