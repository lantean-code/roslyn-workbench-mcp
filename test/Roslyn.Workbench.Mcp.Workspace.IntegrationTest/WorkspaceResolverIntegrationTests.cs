using Roslyn.Workbench.Mcp.Plugins;

namespace Roslyn.Workbench.Mcp.Workspace.Test;

public sealed class WorkspaceResolverIntegrationTests
{
    [Fact]
    public async Task GIVEN_WorkspaceRelativeProjectPath_WHEN_ResolvingProject_THEN_ShouldResolveAgainstWorkspaceRoot()
    {
        using var fixture = await TestWorkspaceFixture.CreateAsync();
        var target = fixture.CreateCoordinator();
        await target.OpenAsync(new WorkspaceOpenRequest
        {
            Path = fixture.ProjectPath,
        }, CancellationToken.None);

        await using var contextLease = target.CreateQueryContext(new WorkspaceStatusRequest(), CancellationToken.None);
        var resolution = contextLease.Context!.WorkspaceResolver.ResolveProject(new ProjectSelector
        {
            Path = "Sample.csproj",
        });

        resolution.Status.Should().Be(SelectorResolveStatus.Resolved);
        resolution.Value.Should().NotBeNull();
        contextLease.Context.WorkspaceResolver.NormalizeProjectPath(resolution.Value!.FilePath!).Should().Be("Sample.csproj");
    }

    [Fact]
    public async Task GIVEN_AmbiguousProjectSelector_WHEN_ResolvingProject_THEN_ShouldReturnAmbiguous()
    {
        using var fixture = await TestWorkspaceFixture.CreateAmbiguousAsync();
        var target = fixture.CreateCoordinator();
        await target.OpenAsync(new WorkspaceOpenRequest
        {
            Path = fixture.ProjectPath,
        }, CancellationToken.None);

        await using var contextLease = target.CreateQueryContext(new WorkspaceStatusRequest(), CancellationToken.None);
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
        using var fixture = await TestWorkspaceFixture.CreateAmbiguousAsync();
        var target = fixture.CreateCoordinator();
        await target.OpenAsync(new WorkspaceOpenRequest
        {
            Path = fixture.ProjectPath,
        }, CancellationToken.None);

        await using var contextLease = target.CreateQueryContext(new WorkspaceStatusRequest(), CancellationToken.None);
        var resolution = contextLease.Context!.WorkspaceResolver.ResolveDocument(new DocumentSelector
        {
            Path = fixture.SharedDocumentPath!,
        });

        resolution.Status.Should().Be(SelectorResolveStatus.Ambiguous);
        resolution.Value.Should().BeNull();
    }

    [Fact]
    public async Task GIVEN_MissingDocumentSelector_WHEN_ResolvingDocument_THEN_ShouldReturnNotFound()
    {
        using var fixture = await TestWorkspaceFixture.CreateAsync();
        var target = fixture.CreateCoordinator();
        await target.OpenAsync(new WorkspaceOpenRequest
        {
            Path = fixture.ProjectPath,
        }, CancellationToken.None);

        await using var contextLease = target.CreateQueryContext(new WorkspaceStatusRequest(), CancellationToken.None);
        var resolution = contextLease.Context!.WorkspaceResolver.ResolveDocument(new DocumentSelector
        {
            Path = "Missing.cs",
        });

        resolution.Status.Should().Be(SelectorResolveStatus.NotFound);
        resolution.Value.Should().BeNull();
    }

    [Fact]
    public async Task GIVEN_ValidWorkspaceEpochWithoutTransactionRevision_WHEN_ValidatingSnapshot_THEN_ShouldMatch()
    {
        using var fixture = await TestWorkspaceFixture.CreateAsync();
        var target = fixture.CreateCoordinator();
        await target.OpenAsync(new WorkspaceOpenRequest
        {
            Path = fixture.ProjectPath,
        }, CancellationToken.None);

        await using var contextLease = target.CreateQueryContext(new WorkspaceStatusRequest(), CancellationToken.None);
        var result = contextLease.Context!.WorkspaceResolver.ValidateSnapshot(new SnapshotPrecondition
        {
            WorkspaceEpoch = contextLease.Context.WorkspaceIdentity.WorkspaceEpoch,
        });

        result.Kind.Should().Be(SnapshotMatchKind.Matched);
    }

    [Fact]
    public async Task GIVEN_MismatchedWorkspaceEpoch_WHEN_ValidatingSnapshot_THEN_ShouldReportWorkspaceEpochMismatch()
    {
        using var fixture = await TestWorkspaceFixture.CreateAsync();
        var target = fixture.CreateCoordinator();
        await target.OpenAsync(new WorkspaceOpenRequest
        {
            Path = fixture.ProjectPath,
        }, CancellationToken.None);

        await using var contextLease = target.CreateQueryContext(new WorkspaceStatusRequest(), CancellationToken.None);
        var result = contextLease.Context!.WorkspaceResolver.ValidateSnapshot(new SnapshotPrecondition
        {
            WorkspaceEpoch = contextLease.Context.WorkspaceIdentity.WorkspaceEpoch + 1,
        });

        result.Kind.Should().Be(SnapshotMatchKind.WorkspaceEpochMismatch);
    }

    [Fact]
    public async Task GIVEN_SuppliedTransactionRevision_WHEN_ValidatingSnapshot_THEN_ShouldReportTransactionRevisionMismatch()
    {
        using var fixture = await TestWorkspaceFixture.CreateAsync();
        var target = fixture.CreateCoordinator();
        await target.OpenAsync(new WorkspaceOpenRequest
        {
            Path = fixture.ProjectPath,
        }, CancellationToken.None);

        await using var contextLease = target.CreateQueryContext(new WorkspaceStatusRequest(), CancellationToken.None);
        var result = contextLease.Context!.WorkspaceResolver.ValidateSnapshot(new SnapshotPrecondition
        {
            WorkspaceEpoch = contextLease.Context.WorkspaceIdentity.WorkspaceEpoch,
            TransactionRevision = 1,
        });

        result.Kind.Should().Be(SnapshotMatchKind.TransactionRevisionMismatch);
    }

    [Fact]
    public async Task GIVEN_TextSpanLocationSelector_WHEN_ResolvingLocation_THEN_ShouldReturnSourceLocation()
    {
        using var fixture = await TestWorkspaceFixture.CreateAsync();
        var target = fixture.CreateCoordinator();
        await target.OpenAsync(new WorkspaceOpenRequest
        {
            Path = fixture.ProjectPath,
        }, CancellationToken.None);
        var sourceText = await File.ReadAllTextAsync(fixture.DocumentPath, TestContext.Current.CancellationToken);
        var start = sourceText.IndexOf("Class1", StringComparison.Ordinal);

        await using var contextLease = target.CreateQueryContext(new WorkspaceStatusRequest(), CancellationToken.None);
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
        }, CancellationToken.None);

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
        using var fixture = await TestWorkspaceFixture.CreateAsync();
        var target = fixture.CreateCoordinator();
        await target.OpenAsync(new WorkspaceOpenRequest
        {
            Path = fixture.ProjectPath,
        }, CancellationToken.None);
        var sourceText = await File.ReadAllTextAsync(fixture.DocumentPath, TestContext.Current.CancellationToken);
        var start = sourceText.IndexOf("Class1", StringComparison.Ordinal);

        await using var contextLease = target.CreateQueryContext(new WorkspaceStatusRequest(), CancellationToken.None);
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
        }, CancellationToken.None);

        resolution.Status.Should().Be(SelectorResolveStatus.Resolved);
        resolution.Value.Should().NotBeNull();
        resolution.Value!.Name.Should().Be("Class1");

        var reference = contextLease.Context.WorkspaceResolver.CreateSymbolReference(resolution.Value);

        reference.DisplayName.Should().Contain("Class1");
        reference.DocumentationCommentId.Should().Be("T:Sample.Class1");
        reference.Location.Should().NotBeNull();
    }
}
