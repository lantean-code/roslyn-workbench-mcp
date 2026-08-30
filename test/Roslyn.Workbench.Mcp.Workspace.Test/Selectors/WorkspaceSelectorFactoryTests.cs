namespace Roslyn.Workbench.Mcp.Workspace.Test.Selectors;

public sealed class WorkspaceSelectorFactoryTests
{
    private readonly WorkspaceSelectorFactory _target;

    public WorkspaceSelectorFactoryTests()
    {
        _target = new WorkspaceSelectorFactory();
    }

    [Fact]
    public void GIVEN_ResolvedLocationIsNull_WHEN_CreatingSelectors_THEN_ShouldReturnNull()
    {
        var canonicalSelector = _target.CreateCanonicalLocationSelector(null);
        var locationSelector = _target.CreateLocationSelector(null);
        var symbolSelector = _target.CreateSymbolSelector(null);

        canonicalSelector.Should().BeNull();
        locationSelector.Should().BeNull();
        symbolSelector.Should().BeNull();
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void GIVEN_ResolvedLocationLacksRequiredData_WHEN_CreatingSelectors_THEN_ShouldReturnNull(bool omitDocument)
    {
        DocumentReference? document = new()
        {
            DocumentId = "DocumentId",
            Path = "Path",
            ProjectId = "ProjectId",
        };

        TextSpanRange? span = null;
        if (omitDocument)
        {
            document = null;
            span = new TextSpanRange
            {
                Start = 1,
                Length = 2,
            };
        }

        var resolvedLocation = new ResolvedLocation
        {
            Snapshot = WorkspaceSnapshotTestFactory.CreatePrecondition(
                Guid.Parse("11111111-1111-1111-1111-111111111111")),
            Document = document,
            Span = span,
        };

        var canonicalSelector = _target.CreateCanonicalLocationSelector(resolvedLocation);
        var locationSelector = _target.CreateLocationSelector(resolvedLocation);
        var symbolSelector = _target.CreateSymbolSelector(resolvedLocation);

        canonicalSelector.Should().BeNull();
        locationSelector.Should().BeNull();
        symbolSelector.Should().BeNull();
    }

    [Theory]
    [InlineData("DocumentId", "DocumentPath", "ProjectId", "DocumentId", null)]
    [InlineData("", "DocumentPath", "ProjectId", null, "DocumentPath")]
    [InlineData("DocumentId", "DocumentPath", "", "DocumentId", null)]
    public void GIVEN_ResolvedLocation_WHEN_CreatingSelectors_THEN_ShouldProjectDocumentIdentityAndSpan(
        string documentId,
        string documentPath,
        string projectId,
        string? expectedDocumentId,
        string? expectedPath)
    {
        var resolvedLocation = new ResolvedLocation
        {
            Snapshot = WorkspaceSnapshotTestFactory.CreatePrecondition(
                Guid.Parse("11111111-1111-1111-1111-111111111111")),
            Document = new DocumentReference
            {
                DocumentId = documentId,
                Path = documentPath,
                ProjectId = projectId,
            },
            Span = new TextSpanRange
            {
                Start = 1,
                Length = 2,
            },
        };

        var canonicalSelector = _target.CreateCanonicalLocationSelector(resolvedLocation);
        var locationSelector = _target.CreateLocationSelector(resolvedLocation);
        var symbolSelector = _target.CreateSymbolSelector(resolvedLocation);

        ProjectSelector? expectedProject = null;
        if (!string.IsNullOrWhiteSpace(projectId))
        {
            expectedProject = new ProjectSelector
            {
                ProjectId = projectId,
            };
        }

        var expectedDocument = new DocumentSelector
        {
            DocumentId = expectedDocumentId,
            Path = expectedPath,
            Project = expectedProject,
        };

        var expectedRange = new TextSpanRange
        {
            Start = 1,
            Length = 2,
        };

        var expectedSpan = new TextSpanSelector
        {
            Document = expectedDocument,
            Range = expectedRange,
        };

        var expectedLocation = new LocationSelector
        {
            Span = expectedSpan,
        };

        var expectedCanonical = new CanonicalLocationSelector
        {
            Span = expectedSpan,
        };

        var expectedSymbol = new SymbolSelector
        {
            Location = expectedLocation,
        };

        canonicalSelector.Should().BeEquivalentTo(expectedCanonical);
        locationSelector.Should().BeEquivalentTo(expectedLocation);
        symbolSelector.Should().BeEquivalentTo(expectedSymbol);
    }
}
