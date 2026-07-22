using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Roslyn.Workbench.Mcp.Plugins.Core.Test.Inspection;

public sealed class AnalyzeNullabilityToolTests
{
    [Fact]
    public async Task GIVEN_LocationAndValidateSnapshotReturnsConflict_WHEN_CallingExecuteAsync_THEN_ShouldReturnConflictResult()
    {
        var target = new AnalyzeNullabilityTool();
        var queryContextMocks = QueryContextMockHelper.Create();
        var expected = PluginExecutionResult<NullabilityAnalysisData>.Conflict(new PluginExecutionError
        {
            Code = "SnapshotMismatch",
            Message = "SnapshotMismatch",
        });

        queryContextMocks.RequestResolver
            .Setup(item => item.ValidateSnapshot<NullabilityAnalysisData>(
                queryContextMocks.QueryContext.Object,
                It.IsAny<SnapshotPrecondition?>()))
            .Returns(expected);

        var result = await target.ExecuteAsync(new AnalyzeNullabilityRequest
        {
            Location = new LocationSelector(),
            ExpectedSnapshot = new SnapshotPrecondition
            {
                WorkspaceEpoch = 1,
            },
        }, queryContextMocks.QueryContext.Object, TestContext.Current.CancellationToken);

        result.Should().BeEquivalentTo(expected);
        queryContextMocks.WorkspaceResolver.Verify(item => item.ResolveLocationAsync(It.IsAny<LocationSelector>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task GIVEN_LocationAndResolveLocationReturnsNotFound_WHEN_CallingExecuteAsync_THEN_ShouldReturnLocationNotFoundResult()
    {
        var target = new AnalyzeNullabilityTool();
        var queryContextMocks = QueryContextMockHelper.Create();

        queryContextMocks.RequestResolver
            .Setup(item => item.ValidateSnapshot<NullabilityAnalysisData>(
                queryContextMocks.QueryContext.Object,
                It.IsAny<SnapshotPrecondition?>()))
            .Returns((PluginExecutionResult<NullabilityAnalysisData>?)null);
        queryContextMocks.WorkspaceResolver
            .Setup(item => item.ResolveLocationAsync(It.IsAny<LocationSelector>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(SelectorResolveResult<Location>.NotFound());

        var result = await target.ExecuteAsync(new AnalyzeNullabilityRequest
        {
            Location = new LocationSelector(),
        }, queryContextMocks.QueryContext.Object, TestContext.Current.CancellationToken);

        result.Outcome.Should().Be(PluginExecutionOutcome.Rejected);
        result.Error.Should().BeEquivalentTo(new PluginExecutionError
        {
            Code = "LocationNotFound",
            Message = "The location selector did not match any result.",
        });
        result.RequiredAction.Should().Be(RequiredAction.ResolveTargetAgain);
    }

    [Fact]
    public async Task GIVEN_LocationAndResolveLocationReturnsAmbiguous_WHEN_CallingExecuteAsync_THEN_ShouldReturnLocationAmbiguousResult()
    {
        var target = new AnalyzeNullabilityTool();
        var queryContextMocks = QueryContextMockHelper.Create();

        queryContextMocks.RequestResolver
            .Setup(item => item.ValidateSnapshot<NullabilityAnalysisData>(
                queryContextMocks.QueryContext.Object,
                It.IsAny<SnapshotPrecondition?>()))
            .Returns((PluginExecutionResult<NullabilityAnalysisData>?)null);
        queryContextMocks.WorkspaceResolver
            .Setup(item => item.ResolveLocationAsync(It.IsAny<LocationSelector>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(SelectorResolveResult<Location>.Ambiguous());

        var result = await target.ExecuteAsync(new AnalyzeNullabilityRequest
        {
            Location = new LocationSelector(),
        }, queryContextMocks.QueryContext.Object, TestContext.Current.CancellationToken);

        result.Outcome.Should().Be(PluginExecutionOutcome.Rejected);
        result.Error.Should().BeEquivalentTo(new PluginExecutionError
        {
            Code = "LocationAmbiguous",
            Message = "The location selector matched multiple results.",
        });
        result.RequiredAction.Should().Be(RequiredAction.ResolveTargetAgain);
    }

    [Fact]
    public async Task GIVEN_LocationAndCurrentSolutionDoesNotContainResolvedDocument_WHEN_CallingExecuteAsync_THEN_ShouldReturnLocationNotFoundResult()
    {
        using var document = RoslynTestFactory.CreateDocument("""
            #nullable enable

            class Formatter
            {
                string Format(string? value)
                {
                    return value.ToString();
                }
            }
            """);
        using var emptyWorkspace = new AdhocWorkspace();

        var target = new AnalyzeNullabilityTool();
        var queryContextMocks = QueryContextMockHelper.Create();
        var selectedLocation = document.GetSingleNodeLocation<IdentifierNameSyntax>(item => item.Identifier.ValueText == "value");

        queryContextMocks.QueryContext
            .SetupGet(item => item.CurrentSolution)
            .Returns(emptyWorkspace.CurrentSolution);
        queryContextMocks.RequestResolver
            .Setup(item => item.ValidateSnapshot<NullabilityAnalysisData>(
                queryContextMocks.QueryContext.Object,
                It.IsAny<SnapshotPrecondition?>()))
            .Returns((PluginExecutionResult<NullabilityAnalysisData>?)null);
        queryContextMocks.WorkspaceResolver
            .Setup(item => item.ResolveLocationAsync(It.IsAny<LocationSelector>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(SelectorResolveResult<Location>.Resolved(selectedLocation));

        var result = await target.ExecuteAsync(new AnalyzeNullabilityRequest
        {
            Location = new LocationSelector(),
        }, queryContextMocks.QueryContext.Object, TestContext.Current.CancellationToken);

        result.Outcome.Should().Be(PluginExecutionOutcome.Rejected);
        result.Error.Should().BeEquivalentTo(new PluginExecutionError
        {
            Code = "LocationNotFound",
            Message = "The location selector did not resolve to a source document.",
        });
        result.RequiredAction.Should().Be(RequiredAction.ResolveTargetAgain);
    }

    [Fact]
    public async Task GIVEN_LocationAndCompilerDiagnosticsContainMixedSpans_WHEN_CallingExecuteAsync_THEN_ShouldReturnOnlyIntersectingNullabilityFindings()
    {
        using var document = RoslynTestFactory.CreateDocument("""
            #nullable enable

            class Formatter
            {
                string Format(string? value)
                {
                    var text = value.ToString();
                    return value.ToString();
                }
            }
            """);

        var target = new AnalyzeNullabilityTool();
        var queryContextMocks = QueryContextMockHelper.Create();
        var compilerDiagnosticService = new Mock<ICompilerDiagnosticService>();
        var selectedLocation = document.GetSingleNodeLocation<VariableDeclaratorSyntax>(item => item.Identifier.ValueText == "text");
        var selectedDocument = document.Document;
        var syntaxTree = await selectedDocument.GetSyntaxTreeAsync(TestContext.Current.CancellationToken);
        var selectedStart = selectedLocation.SourceSpan.Start;
        var selectedLength = selectedLocation.SourceSpan.Length;
        var outsideLocation = document.GetSingleNodeLocation<ReturnStatementSyntax>(item => item.ToString().Contains("return value.ToString()", StringComparison.Ordinal));
        var projectedSelectedLocation = SelectorTestFactory.CreateResolvedLocation(selectedLocation, "Code.cs");

        queryContextMocks.QueryContext
            .SetupGet(item => item.CurrentSolution)
            .Returns(document.Solution);
        queryContextMocks.QueryContext
            .SetupGet(item => item.DefaultMaxResults)
            .Returns(10);
        queryContextMocks.ToolExecutionServices
            .SetupGet(item => item.CompilerDiagnosticService)
            .Returns(compilerDiagnosticService.Object);
        queryContextMocks.RequestResolver
            .Setup(item => item.ValidateSnapshot<NullabilityAnalysisData>(
                queryContextMocks.QueryContext.Object,
                It.IsAny<SnapshotPrecondition?>()))
            .Returns((PluginExecutionResult<NullabilityAnalysisData>?)null);
        queryContextMocks.WorkspaceResolver
            .Setup(item => item.ResolveLocationAsync(It.IsAny<LocationSelector>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(SelectorResolveResult<Location>.Resolved(selectedLocation));
        queryContextMocks.WorkspaceResolver
            .Setup(item => item.CreateResolvedLocation(It.Is<Location>(location =>
                location.SourceSpan == selectedLocation.SourceSpan
                && location.SourceTree == syntaxTree)))
            .Returns(projectedSelectedLocation);
        queryContextMocks.WorkspaceResolver
            .Setup(item => item.CreateResolvedLocation(It.Is<Location>(location =>
                location.SourceSpan == outsideLocation.SourceSpan
                && location.SourceTree == syntaxTree)))
            .Returns(SelectorTestFactory.CreateResolvedLocation(outsideLocation, "Code.cs"));

        compilerDiagnosticService
            .Setup(item => item.GetCompilerDiagnosticsAsync(
                It.Is<IReadOnlyList<Document>>(documents => documents.Count == 1 && documents[0] == selectedDocument),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(
            [
                RoslynTestFactory.CreateDiagnostic("CS8602", syntaxTree!, selectedStart, selectedLength),
                RoslynTestFactory.CreateDiagnostic("CS8602", syntaxTree!, outsideLocation.SourceSpan.Start, outsideLocation.SourceSpan.Length),
                RoslynTestFactory.CreateDiagnostic("CS0219", syntaxTree!, selectedStart, selectedLength),
            ]);

        var result = await target.ExecuteAsync(new AnalyzeNullabilityRequest
        {
            Location = new LocationSelector(),
        }, queryContextMocks.QueryContext.Object, TestContext.Current.CancellationToken);

        result.Outcome.Should().Be(PluginExecutionOutcome.Succeeded);
        result.Data!.Findings.Items.Should().ContainSingle();
        result.Data.Findings.Items[0].Diagnostic!.Id.Should().Be("CS8602");
        result.Data.Findings.Items[0].Diagnostic!.Location.Should().Be(projectedSelectedLocation);
    }

    [Fact]
    public async Task GIVEN_ScopeResolutionHasRejection_WHEN_CallingExecuteAsync_THEN_ShouldReturnRejectionResult()
    {
        var target = new AnalyzeNullabilityTool();
        var queryContextMocks = QueryContextMockHelper.Create();
        var expected = PluginExecutionResult<NullabilityAnalysisData>.Rejected(new PluginExecutionError
        {
            Code = "DocumentNotFound",
            Message = "DocumentNotFound",
        });

        queryContextMocks.RequestResolver
            .Setup(item => item.ResolveDocuments<NullabilityAnalysisData>(
                It.IsAny<ScopeSelector?>(),
                queryContextMocks.QueryContext.Object))
            .Returns(ToolResolutionResult<IReadOnlyList<Document>, NullabilityAnalysisData>.Rejected(expected));

        var result = await target.ExecuteAsync(new AnalyzeNullabilityRequest(), queryContextMocks.QueryContext.Object, TestContext.Current.CancellationToken);

        result.Should().BeEquivalentTo(expected);
    }

    [Fact]
    public async Task GIVEN_ScopeDiagnosticsContainMixedIdsAcrossDocuments_WHEN_CallingExecuteAsync_THEN_ShouldReturnOrderedBoundedNullabilityFindings()
    {
        using var solution = RoslynTestFactory.CreateSolution(
        [
            new InMemoryRoslynProjectDefinition
            {
                Name = "Project",
                Documents =
                [
                    new InMemoryRoslynDocumentDefinition
                    {
                        Name = "B.cs",
                        Source = """
                            #nullable enable

                            class BFormatter
                            {
                                string Format(string? value)
                                {
                                    return value.ToString();
                                }
                            }
                            """,
                    },
                    new InMemoryRoslynDocumentDefinition
                    {
                        Name = "A.cs",
                        Source = """
                            #nullable enable

                            class AFormatter
                            {
                                string Format(string? value)
                                {
                                    return value.ToString();
                                }
                            }
                            """,
                    },
                ],
            },
        ]);

        var target = new AnalyzeNullabilityTool();
        var queryContextMocks = QueryContextMockHelper.Create();
        var compilerDiagnosticService = new Mock<ICompilerDiagnosticService>();
        var documents = solution.Solution.Projects.Single().Documents.OrderByDescending(item => item.Name, StringComparer.Ordinal).ToArray();
        var firstDocument = solution.GetDocument("A.cs");
        var secondDocument = solution.GetDocument("B.cs");
        var firstTree = await firstDocument.GetSyntaxTreeAsync(TestContext.Current.CancellationToken);
        var secondTree = await secondDocument.GetSyntaxTreeAsync(TestContext.Current.CancellationToken);
        var firstValueLocation = solution.GetSingleNodeLocation<IdentifierNameSyntax>("A.cs", item => item.Identifier.ValueText == "value");
        var secondValueLocation = solution.GetSingleNodeLocation<IdentifierNameSyntax>("B.cs", item => item.Identifier.ValueText == "value");
        var firstProjectedLocation = SelectorTestFactory.CreateResolvedLocation(firstValueLocation, "A.cs");

        queryContextMocks.QueryContext
            .SetupGet(item => item.DefaultMaxResults)
            .Returns(10);
        queryContextMocks.ToolExecutionServices
            .SetupGet(item => item.CompilerDiagnosticService)
            .Returns(compilerDiagnosticService.Object);
        queryContextMocks.RequestResolver
            .Setup(item => item.ResolveDocuments<NullabilityAnalysisData>(
                It.IsAny<ScopeSelector?>(),
                queryContextMocks.QueryContext.Object))
            .Returns(ToolResolutionResult<IReadOnlyList<Document>, NullabilityAnalysisData>.Resolved(documents));
        queryContextMocks.WorkspaceResolver
            .Setup(item => item.CreateResolvedLocation(It.Is<Location>(location =>
                location.SourceSpan == firstValueLocation.SourceSpan
                && location.SourceTree == firstTree)))
            .Returns(firstProjectedLocation);
        queryContextMocks.WorkspaceResolver
            .Setup(item => item.CreateResolvedLocation(It.Is<Location>(location =>
                location.SourceSpan == secondValueLocation.SourceSpan
                && location.SourceTree == secondTree)))
            .Returns(SelectorTestFactory.CreateResolvedLocation(secondValueLocation, "B.cs"));

        compilerDiagnosticService
            .Setup(item => item.GetCompilerDiagnosticsAsync(
                It.Is<IReadOnlyList<Document>>(selected => selected.Count == 2),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(
            [
                RoslynTestFactory.CreateDiagnostic("CS8602", secondTree!, secondValueLocation.SourceSpan.Start, secondValueLocation.SourceSpan.Length),
                RoslynTestFactory.CreateDiagnostic("CS0219", secondTree!, secondValueLocation.SourceSpan.Start, secondValueLocation.SourceSpan.Length),
                RoslynTestFactory.CreateDiagnostic("CS8602", firstTree!, firstValueLocation.SourceSpan.Start, firstValueLocation.SourceSpan.Length),
            ]);

        var result = await target.ExecuteAsync(new AnalyzeNullabilityRequest
        {
            FindingsLimit = 1,
        }, queryContextMocks.QueryContext.Object, TestContext.Current.CancellationToken);

        result.Outcome.Should().Be(PluginExecutionOutcome.Succeeded);
        result.Data!.Findings.Items.Should().ContainSingle();
        result.Data.Findings.Items[0].Diagnostic!.Id.Should().Be("CS8602");
        result.Data.Findings.Items[0].Diagnostic!.Location.Should().Be(firstProjectedLocation);
        result.Data.Findings.HasMore.Should().BeTrue();
        queryContextMocks.WorkspaceResolver.Verify(item => item.CreateResolvedLocation(firstValueLocation), Times.Once);
        queryContextMocks.WorkspaceResolver.Verify(item => item.CreateResolvedLocation(secondValueLocation), Times.Never);
    }

    [Fact]
    public async Task GIVEN_ScopeDiagnosticsIncludeNonSourceFinding_WHEN_CallingExecuteAsync_THEN_ShouldReturnFindingWithoutLocation()
    {
        using var document = RoslynTestFactory.CreateDocument("""
            #nullable enable

            class Formatter
            {
                string Format(string? value)
                {
                    return value.ToString();
                }
            }
            """);

        var target = new AnalyzeNullabilityTool();
        var queryContextMocks = QueryContextMockHelper.Create();
        var compilerDiagnosticService = new Mock<ICompilerDiagnosticService>();
        var syntaxTree = await document.Document.GetSyntaxTreeAsync(TestContext.Current.CancellationToken);
        var valueLocation = document.GetSingleNodeLocation<IdentifierNameSyntax>(item => item.Identifier.ValueText == "value");

        queryContextMocks.QueryContext
            .SetupGet(item => item.DefaultMaxResults)
            .Returns(10);
        queryContextMocks.ToolExecutionServices
            .SetupGet(item => item.CompilerDiagnosticService)
            .Returns(compilerDiagnosticService.Object);
        queryContextMocks.RequestResolver
            .Setup(item => item.ResolveDocuments<NullabilityAnalysisData>(
                It.IsAny<ScopeSelector?>(),
                queryContextMocks.QueryContext.Object))
            .Returns(ToolResolutionResult<IReadOnlyList<Document>, NullabilityAnalysisData>.Resolved([document.Document]));
        queryContextMocks.WorkspaceResolver
            .Setup(item => item.CreateResolvedLocation(It.Is<Location>(location =>
                location.SourceSpan == valueLocation.SourceSpan
                && location.SourceTree == syntaxTree)))
            .Returns(SelectorTestFactory.CreateResolvedLocation(valueLocation, "Code.cs"));

        compilerDiagnosticService
            .Setup(item => item.GetCompilerDiagnosticsAsync(
                It.Is<IReadOnlyList<Document>>(documents => documents.Count == 1 && documents[0] == document.Document),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(
            [
                Diagnostic.Create(
                    new DiagnosticDescriptor("CS8602", "CS8602", "Message", "Category", Microsoft.CodeAnalysis.DiagnosticSeverity.Warning, isEnabledByDefault: true),
                    Location.None),
                RoslynTestFactory.CreateDiagnostic("CS8602", syntaxTree!, valueLocation.SourceSpan.Start, valueLocation.SourceSpan.Length),
            ]);

        var result = await target.ExecuteAsync(new AnalyzeNullabilityRequest(), queryContextMocks.QueryContext.Object, TestContext.Current.CancellationToken);

        result.Outcome.Should().Be(PluginExecutionOutcome.Succeeded);
        result.Data!.Findings.Items.Should().HaveCount(2);
        result.Data.Findings.Items.Should().Contain(item => item.Diagnostic!.Location == null);
        result.Data.Findings.Items.Should().Contain(item => item.Diagnostic!.Location != null);
    }

}
