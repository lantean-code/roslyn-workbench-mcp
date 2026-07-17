using Roslyn.Workbench.Mcp.Plugins;

namespace Roslyn.Workbench.Mcp.Workspace.Test;

public sealed class WorkspaceResolverIntegrationTests
{
    [Fact]
    public async Task GIVEN_WorkspaceRelativeProjectPath_WHEN_ResolvingProject_THEN_ShouldResolveAgainstWorkspaceRoot()
    {
        await using var fixture = await TestWorkspaceFixture.CreateAsync();
        var originalDocumentBytes = await File.ReadAllBytesAsync(fixture.DocumentPath, TestContext.Current.CancellationToken);
        await using var target = fixture.CreateWorkspace();
        await target.OpenAsync(fixture.ProjectPath, TestContext.Current.CancellationToken);

        await using var contextLease = target.CreateQueryContext(new QueryRequest(), TestContext.Current.CancellationToken);
        var resolution = contextLease.Context!.WorkspaceResolver.ResolveProject(new ProjectSelector
        {
            Path = "Sample.csproj",
        });

        resolution.Status.Should().Be(SelectorResolveStatus.Resolved);
        resolution.Value.Should().NotBeNull();
        contextLease.Context.WorkspaceResolver.NormalizeProjectPath(resolution.Value!.FilePath!).Should().Be("Sample.csproj");
        fixture.WorkspaceRoot.Should().Be(Path.GetDirectoryName(fixture.ProjectPath));
        Directory.Exists(fixture.StateRoot).Should().BeTrue();
        (await File.ReadAllBytesAsync(fixture.DocumentPath, TestContext.Current.CancellationToken)).Should().Equal(originalDocumentBytes);
    }

    [Fact]
    public async Task GIVEN_AmbiguousProjectSelector_WHEN_ResolvingProject_THEN_ShouldReturnAmbiguous()
    {
        await using var fixture = await TestWorkspaceFixture.CreateAmbiguousAsync();
        await using var target = fixture.CreateWorkspace();
        await target.OpenAsync(fixture.ProjectPath, TestContext.Current.CancellationToken);

        await using var contextLease = target.CreateQueryContext(new QueryRequest(), TestContext.Current.CancellationToken);
        var resolution = contextLease.Context!.WorkspaceResolver.ResolveProject(new ProjectSelector
        {
            Name = "Sample",
        });

        resolution.Status.Should().Be(SelectorResolveStatus.Ambiguous);
        resolution.Value.Should().BeNull();
    }

    [Fact]
    public async Task GIVEN_AmbiguousDocumentPath_WHEN_ResolvingDocument_THEN_ShouldReturnAmbiguous()
    {
        await using var fixture = await TestWorkspaceFixture.CreateAmbiguousAsync();
        await using var target = fixture.CreateWorkspace();
        await target.OpenAsync(fixture.ProjectPath, TestContext.Current.CancellationToken);

        await using var contextLease = target.CreateQueryContext(new QueryRequest(), TestContext.Current.CancellationToken);
        var resolution = contextLease.Context!.WorkspaceResolver.ResolveDocument(new DocumentSelector
        {
            Path = fixture.SharedDocumentPath!,
        });

        resolution.Status.Should().Be(SelectorResolveStatus.Ambiguous);
        resolution.Value.Should().BeNull();
    }

    [Fact]
    public async Task GIVEN_TextSpanLocationSelector_WHEN_ResolvingLocation_THEN_ShouldReturnSourceLocation()
    {
        await using var fixture = await TestWorkspaceFixture.CreateAsync();
        await using var target = fixture.CreateWorkspace();
        await target.OpenAsync(fixture.ProjectPath, TestContext.Current.CancellationToken);
        var sourceText = await File.ReadAllTextAsync(fixture.DocumentPath, TestContext.Current.CancellationToken);
        var start = sourceText.IndexOf("Class1", StringComparison.Ordinal);

        await using var contextLease = target.CreateQueryContext(new QueryRequest(), TestContext.Current.CancellationToken);
        var resolution = await contextLease.Context!.WorkspaceResolver.ResolveLocationAsync(new LocationSelector
        {
            Span = new TextSpanSelector
            {
                Document = new DocumentSelector
                {
                    Path = fixture.DocumentPath,
                },
                Start = start,
                Length = "Class1".Length,
            },
        }, TestContext.Current.CancellationToken);

        resolution.Status.Should().Be(SelectorResolveStatus.Resolved);
        resolution.Value.Should().NotBeNull();
        resolution.Value!.IsInSource.Should().BeTrue();

        var projectedLocation = contextLease.Context.WorkspaceResolver.CreateResolvedLocation(resolution.Value);

        projectedLocation.Should().NotBeNull();
        projectedLocation!.Document!.Path.Should().Be("Class1.cs");
        projectedLocation.Span!.Start.Should().Be(start);
    }

    [Fact]
    public async Task GIVEN_LocationBasedSymbolSelector_WHEN_ResolvingSymbol_THEN_ShouldReturnCanonicalSymbolReference()
    {
        await using var fixture = await TestWorkspaceFixture.CreateAsync();
        await using var target = fixture.CreateWorkspace();
        await target.OpenAsync(fixture.ProjectPath, TestContext.Current.CancellationToken);
        var sourceText = await File.ReadAllTextAsync(fixture.DocumentPath, TestContext.Current.CancellationToken);
        var start = sourceText.IndexOf("Class1", StringComparison.Ordinal);

        await using var contextLease = target.CreateQueryContext(new QueryRequest(), TestContext.Current.CancellationToken);
        var resolution = await contextLease.Context!.WorkspaceResolver.ResolveSymbolAsync(new SymbolSelector
        {
            Location = new LocationSelector
            {
                Span = new TextSpanSelector
                {
                    Document = new DocumentSelector
                    {
                        Path = "Class1.cs",
                    },
                    Start = start,
                    Length = "Class1".Length,
                },
            },
        }, TestContext.Current.CancellationToken);

        resolution.Status.Should().Be(SelectorResolveStatus.Resolved);
        resolution.Value.Should().NotBeNull();
        resolution.Value!.Name.Should().Be("Class1");

        var reference = contextLease.Context.WorkspaceResolver.CreateSymbolReference(resolution.Value);

        reference.DisplayName.Should().Contain("Class1");
        reference.DocumentationCommentId.Should().Be("T:Sample.Class1");
        reference.Location.Should().NotBeNull();
    }

    [Fact]
    public async Task GIVEN_MetadataSymbolSourceLocation_WHEN_ResolvingSymbol_THEN_ShouldReturnMetadataReference()
    {
        await using var fixture = await TestWorkspaceFixture.CreateAsync();
        const string source = "namespace Sample; public sealed class Class1 { public System.String Value { get; } = System.String.Empty; }";
        await File.WriteAllTextAsync(fixture.DocumentPath, source, TestContext.Current.CancellationToken);
        await using var target = fixture.CreateWorkspace();
        await target.OpenAsync(fixture.ProjectPath, TestContext.Current.CancellationToken);
        var start = source.IndexOf("String", StringComparison.Ordinal);

        await using var contextLease = target.CreateQueryContext(new QueryRequest(), TestContext.Current.CancellationToken);
        var resolution = await contextLease.Context!.WorkspaceResolver.ResolveSymbolAsync(new SymbolSelector
        {
            Location = new LocationSelector
            {
                Span = new TextSpanSelector
                {
                    Document = new DocumentSelector
                    {
                        Path = "Class1.cs",
                    },
                    Start = start,
                    Length = "String".Length,
                },
            },
        }, TestContext.Current.CancellationToken);

        resolution.Status.Should().Be(SelectorResolveStatus.Resolved);
        resolution.Value.Should().NotBeNull();
        resolution.Value!.Locations.Should().Contain(static location => location.IsInMetadata);

        var reference = contextLease.Context.WorkspaceResolver.CreateSymbolReference(resolution.Value);

        reference.DocumentationCommentId.Should().Be("T:System.String");
        reference.Location.Should().BeNull();
    }

    [Fact]
    public async Task GIVEN_ReferencedProjectDocumentationId_WHEN_ResolvingSymbol_THEN_ShouldResolveAcrossProjectBoundary()
    {
        await using var fixture = await TestWorkspaceFixture.CreateAmbiguousAsync();
        await using var target = fixture.CreateWorkspace();
        await target.OpenAsync(fixture.ProjectPath, TestContext.Current.CancellationToken);

        await using var contextLease = target.CreateQueryContext(new QueryRequest(), TestContext.Current.CancellationToken);
        var resolution = await contextLease.Context!.WorkspaceResolver.ResolveSymbolAsync(new SymbolSelector
        {
            DocumentationCommentId = "T:Sample.ProjectTwo.Class1",
        }, TestContext.Current.CancellationToken);

        resolution.Status.Should().Be(SelectorResolveStatus.Resolved);
        resolution.Value.Should().NotBeNull();

        var reference = contextLease.Context.WorkspaceResolver.CreateSymbolReference(resolution.Value!);

        reference.DocumentationCommentId.Should().Be("T:Sample.ProjectTwo.Class1");
        reference.Location.Should().NotBeNull();
        reference.Location!.Document!.Path.Should().Be("../ProjectTwo/Class1.cs");
    }

    private sealed record QueryRequest : WorkspaceBoundRequest;
}
