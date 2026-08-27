using System.Text.Json;

namespace Roslyn.Workbench.Mcp.Workspace.Test.Resolution;

public sealed class WorkspaceResolverIntegrationTests
{
    [Fact]
    public async Task GIVEN_WorkspaceRelativeProjectPath_WHEN_ResolvingProject_THEN_ShouldResolveAgainstWorkspaceRoot()
    {
        using var fixture = TestWorkspaceFixture.Create();
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
        var normalized = contextLease.Context.WorkspacePathService.TryNormalizePath(resolution.Value!.FilePath!, out var normalizedPath);

        normalized.Should().BeTrue();
        normalizedPath.Should().Be("Sample.csproj");
        fixture.WorkspaceRoot.Should().Be(Path.GetDirectoryName(fixture.ProjectPath));
        Directory.Exists(fixture.StateRoot).Should().BeTrue();
        (await File.ReadAllBytesAsync(fixture.DocumentPath, TestContext.Current.CancellationToken)).Should().Equal(originalDocumentBytes);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task GIVEN_SingleTargetProject_WHEN_ResolvingTargetFramework_THEN_ShouldUseEvaluatedProjectFramework()
    {
        using var fixture = TestWorkspaceFixture.Create();
        await using var target = fixture.CreateWorkspace();
        await target.OpenAsync(fixture.ProjectPath, TestContext.Current.CancellationToken);

        await using var contextLease = target.CreateQueryContext(new QueryRequest(), TestContext.Current.CancellationToken);
        var resolver = contextLease.Context!.WorkspaceResolver;

        var matching = resolver.ResolveProject(new ProjectSelector { TargetFramework = "NET10.0" });
        var unavailable = resolver.ResolveProject(new ProjectSelector { TargetFramework = "net8.0" });

        matching.Status.Should().Be(SelectorResolveStatus.Resolved);
        matching.Value!.FilePath.Should().Be(fixture.ProjectPath);
        unavailable.Status.Should().Be(SelectorResolveStatus.NotFound);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task GIVEN_MultiTargetProject_WHEN_ResolvingTargetFramework_THEN_ShouldSelectMatchingRoslynProject()
    {
        using var fixture = WorkspaceAssetMaterializer.Materialize("MultiTargetLinked");
        var solutionPath = Path.Combine(fixture.WorkspaceRoot, "Sample.slnx");
        var options = new ComponentWorkspaceOptions
        {
            StateDirectory = fixture.StateRoot,
        };
        await using var target = ComponentWorkspace.Create(options);
        await target.OpenAsync(solutionPath, TestContext.Current.CancellationToken);

        await using var contextLease = target.CreateQueryContext(new QueryRequest(), TestContext.Current.CancellationToken);
        var resolver = contextLease.Context!.WorkspaceResolver;

        var net10 = resolver.ResolveProject(new ProjectSelector
        {
            Path = "MultiTarget/MultiTarget.csproj",
            TargetFramework = "net10.0",
        });

        var netStandard = resolver.ResolveProject(new ProjectSelector
        {
            Path = "MultiTarget/MultiTarget.csproj",
            TargetFramework = "netstandard2.0",
        });

        net10.Status.Should().Be(SelectorResolveStatus.Resolved);
        netStandard.Status.Should().Be(SelectorResolveStatus.Resolved);
        net10.Value!.Id.Should().NotBe(netStandard.Value!.Id);
        net10.Value.Name.Should().EndWith("(net10.0)");
        netStandard.Value.Name.Should().EndWith("(netstandard2.0)");
    }

    [Fact]
    public async Task GIVEN_AmbiguousProjectSelector_WHEN_ResolvingProject_THEN_ShouldReturnAmbiguous()
    {
        using var fixture = TestWorkspaceFixture.CreateAmbiguous();
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
        using var fixture = TestWorkspaceFixture.CreateAmbiguous();
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
    [Trait("Category", "Integration")]
    public async Task GIVEN_TextSpanLocationSelector_WHEN_ResolvingLocation_THEN_ShouldReturnCanonicalWireCompatibleSelector()
    {
        using var fixture = TestWorkspaceFixture.Create();
        await using var target = fixture.CreateWorkspace();
        await target.OpenAsync(fixture.ProjectPath, TestContext.Current.CancellationToken);
        var sourceText = await File.ReadAllTextAsync(fixture.DocumentPath, TestContext.Current.CancellationToken);
        var start = sourceText.IndexOf("Class1", StringComparison.Ordinal);

        await using var contextLease = target.CreateQueryContext(new QueryRequest(), TestContext.Current.CancellationToken);
        var document = new DocumentSelector { Path = fixture.DocumentPath };
        var range = new TextSpanRange { Start = start, Length = "Class1".Length };
        var span = new TextSpanSelector
        {
            Document = document,
            Range = range,
        };

        var selector = new LocationSelector { Span = span };
        var resolution = await contextLease.Context!.WorkspaceResolver.ResolveLocationAsync(selector, TestContext.Current.CancellationToken);

        resolution.Status.Should().Be(SelectorResolveStatus.Resolved);
        resolution.Value.Should().NotBeNull();
        resolution.Value!.IsInSource.Should().BeTrue();

        var projectedLocation = contextLease.Context.WorkspaceResolver.CreateResolvedLocation(resolution.Value);

        projectedLocation.Should().NotBeNull();
        projectedLocation!.Document!.Path.Should().Be("Class1.cs");
        projectedLocation.Span!.Start.Should().Be(start);
        projectedLocation.Selector.Should().NotBeNull();

        var selectorJson = JsonSerializer.Serialize(projectedLocation.Selector, JsonSerializerOptions.Web);
        var inputSelector = JsonSerializer.Deserialize<LocationSelector>(selectorJson, JsonSerializerOptions.Web);

        inputSelector.Should().NotBeNull();
        var inputResolution = await contextLease.Context.WorkspaceResolver.ResolveLocationAsync(
            inputSelector!,
            TestContext.Current.CancellationToken);

        inputResolution.Status.Should().Be(SelectorResolveStatus.Resolved);
        inputResolution.Value!.SourceSpan.Should().Be(new TextSpan(start, "Class1".Length));
        inputResolution.Value.SourceTree!.FilePath.Should().Be(fixture.DocumentPath);
    }

    [Fact]
    public async Task GIVEN_LocationBasedSymbolSelector_WHEN_ResolvingSymbol_THEN_ShouldReturnCanonicalSymbolReference()
    {
        using var fixture = TestWorkspaceFixture.Create();
        await using var target = fixture.CreateWorkspace();
        await target.OpenAsync(fixture.ProjectPath, TestContext.Current.CancellationToken);
        var sourceText = await File.ReadAllTextAsync(fixture.DocumentPath, TestContext.Current.CancellationToken);
        var start = sourceText.IndexOf("Class1", StringComparison.Ordinal);

        await using var contextLease = target.CreateQueryContext(new QueryRequest(), TestContext.Current.CancellationToken);
        var document = new DocumentSelector { Path = "Class1.cs" };
        var range = new TextSpanRange { Start = start, Length = "Class1".Length };
        var span = new TextSpanSelector
        {
            Document = document,
            Range = range,
        };

        var location = new LocationSelector { Span = span };
        var selector = new SymbolSelector { Location = location };
        var resolution = await contextLease.Context!.WorkspaceResolver.ResolveSymbolAsync(selector, TestContext.Current.CancellationToken);

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
        using var fixture = TestWorkspaceFixture.Create();
        const string source = "namespace Sample; public sealed class Class1 { public System.String Value { get; } = System.String.Empty; }";
        await File.WriteAllTextAsync(fixture.DocumentPath, source, TestContext.Current.CancellationToken);
        await using var target = fixture.CreateWorkspace();
        await target.OpenAsync(fixture.ProjectPath, TestContext.Current.CancellationToken);
        var start = source.IndexOf("String", StringComparison.Ordinal);

        await using var contextLease = target.CreateQueryContext(new QueryRequest(), TestContext.Current.CancellationToken);
        var document = new DocumentSelector { Path = "Class1.cs" };
        var range = new TextSpanRange { Start = start, Length = "String".Length };
        var span = new TextSpanSelector
        {
            Document = document,
            Range = range,
        };

        var location = new LocationSelector { Span = span };
        var selector = new SymbolSelector { Location = location };
        var resolution = await contextLease.Context!.WorkspaceResolver.ResolveSymbolAsync(selector, TestContext.Current.CancellationToken);

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
        using var fixture = TestWorkspaceFixture.CreateAmbiguous();
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
        reference.Location!.Document!.Path.Should().Be("ProjectTwo/Class1.cs");
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task GIVEN_LinkedSymbolAndProjectPath_WHEN_ResolvingSymbol_THEN_ShouldResolveWithinSelectedProject()
    {
        using var fixture = TestWorkspaceFixture.CreateAmbiguous();
        await using var target = fixture.CreateWorkspace();
        await target.OpenAsync(fixture.ProjectPath, TestContext.Current.CancellationToken);

        await using var contextLease = target.CreateQueryContext(new QueryRequest(), TestContext.Current.CancellationToken);
        var resolver = contextLease.Context!.WorkspaceResolver;
        var projectSelector = new ProjectSelector { Path = "ProjectOne/Sample.csproj" };
        var projectResolution = resolver.ResolveProject(projectSelector);

        var resolution = await resolver.ResolveSymbolAsync(new SymbolSelector
        {
            DocumentationCommentId = "T:Sample.Shared.SharedClass",
            Project = projectSelector,
        }, TestContext.Current.CancellationToken);

        projectResolution.Status.Should().Be(SelectorResolveStatus.Resolved);
        resolution.Status.Should().Be(SelectorResolveStatus.Resolved);

        var reference = resolver.CreateSymbolReference(resolution.Value!);

        reference.Location!.Document!.ProjectId.Should().Be(projectResolution.Value!.Id.Id.ToString());
    }

    private sealed record QueryRequest : WorkspaceBoundRequest;
}
