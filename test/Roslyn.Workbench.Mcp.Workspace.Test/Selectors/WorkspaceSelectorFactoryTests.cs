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
        var locationSelector = _target.CreateLocationSelector(null);
        var symbolSelector = _target.CreateSymbolSelector(null);

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
            Document = document,
            Span = span,
        };

        var locationSelector = _target.CreateLocationSelector(resolvedLocation);
        var symbolSelector = _target.CreateSymbolSelector(resolvedLocation);

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
        var expectedSpan = new TextSpanSelector
        {
            Document = expectedDocument,
            Start = 1,
            Length = 2,
        };
        var expectedLocation = new LocationSelector
        {
            Span = expectedSpan,
        };
        var expectedSymbol = new SymbolSelector
        {
            Location = expectedLocation,
        };

        locationSelector.Should().BeEquivalentTo(expectedLocation);
        symbolSelector.Should().BeEquivalentTo(expectedSymbol);
    }
}
