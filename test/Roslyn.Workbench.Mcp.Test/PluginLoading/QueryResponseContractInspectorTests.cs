using Roslyn.Workbench.Mcp.Plugins.Core.Contracts.Collections;

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

    public sealed record BoundedResponse
    {
        public string Text { get; init; } = string.Empty;

        public int Count { get; init; }

        public BoundedCollection<string> BoundedItems { get; init; } = new();
    }

    public sealed record RawCollectionResponse
    {
        public string[] ArrayItems { get; init; } = [];

        public IReadOnlyList<string> ReadOnlyListItems { get; init; } = [];

        public IReadOnlyCollection<string> ReadOnlyCollectionItems { get; init; } = [];

        public IEnumerable<string> EnumerableItems { get; init; } = [];

        public ICollection<string> CollectionItems { get; init; } = [];

        public IList<string> ListInterfaceItems { get; init; } = [];

        public List<string> ListItems { get; init; } = [];
    }
}
