using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Text;

namespace Roslyn.Workbench.Mcp.CodeActions.Test.Discovery;

public sealed class CodeActionProjectDiagnosticCollectionTests
{
    [Fact]
    public void GIVEN_SourceDiagnosticIsNotForProjectDocument_WHEN_CreatingCollection_THEN_ShouldExcludeIt()
    {
        using var roslyn = RoslynTestFactory.CreateDocument("class Sample { }");
        var generatedSyntaxTree = CSharpSyntaxTree.ParseText(
            "internal sealed class GeneratedType { }",
            path: "GeneratedType.g.cs",
            cancellationToken: TestContext.Current.CancellationToken);

        var diagnostic = RoslynTestFactory.CreateDiagnostic("GeneratedDiagnostic", generatedSyntaxTree, 0, 1);
        var unpartitionedCollection = new CodeActionDiagnosticCollection([diagnostic], []);

        var result = CodeActionProjectDiagnosticCollection.Create(
            roslyn.Document.Project,
            unpartitionedCollection);

        result.Diagnostics.Should().BeEmpty();
        result.ProjectDiagnostics.Should().BeEmpty();
        result.GetDocumentDiagnostics(generatedSyntaxTree, span: null).Should().BeEmpty();
    }

    [Fact]
    public void GIVEN_SyntaxTreeIsNotPresent_WHEN_GettingDocumentDiagnostics_THEN_ShouldReturnEmpty()
    {
        var includedSyntaxTree = CSharpSyntaxTree.ParseText(
            "class Included { }",
            cancellationToken: TestContext.Current.CancellationToken);

        var missingSyntaxTree = CSharpSyntaxTree.ParseText(
            "class Missing { }",
            cancellationToken: TestContext.Current.CancellationToken);
        var diagnostic = RoslynTestFactory.CreateDiagnostic("DiagnosticId", includedSyntaxTree, 0, 1);
        var diagnosticsBySyntaxTree = new Dictionary<SyntaxTree, IReadOnlyList<Diagnostic>>
        {
            [includedSyntaxTree] = [diagnostic],
        };

        var target = new CodeActionProjectDiagnosticCollection(
            [diagnostic],
            [],
            diagnosticsBySyntaxTree,
            []);

        var result = target.GetDocumentDiagnostics(missingSyntaxTree, span: null);

        result.Should().BeEmpty();
    }

    [Fact]
    public void GIVEN_SpanIsNotSpecified_WHEN_GettingDocumentDiagnostics_THEN_ShouldReturnAllForTree()
    {
        var syntaxTree = CSharpSyntaxTree.ParseText(
            "class Sample { }",
            cancellationToken: TestContext.Current.CancellationToken);
        var diagnostic = RoslynTestFactory.CreateDiagnostic("DiagnosticId", syntaxTree, 0, 1);
        var diagnosticsBySyntaxTree = new Dictionary<SyntaxTree, IReadOnlyList<Diagnostic>>
        {
            [syntaxTree] = [diagnostic],
        };

        var target = new CodeActionProjectDiagnosticCollection(
            [diagnostic],
            [],
            diagnosticsBySyntaxTree,
            ["Warning"]);

        var result = target.GetDocumentDiagnostics(syntaxTree, span: null);

        result.Should().Equal(diagnostic);
        target.Diagnostics.Should().Equal(diagnostic);
        target.ProjectDiagnostics.Should().BeEmpty();
        target.Warnings.Should().Equal("Warning");
    }

    [Fact]
    public void GIVEN_SpanIsSpecified_WHEN_GettingDocumentDiagnostics_THEN_ShouldReturnOnlyIntersectingDiagnostics()
    {
        var syntaxTree = CSharpSyntaxTree.ParseText(
            "class Sample { }",
            cancellationToken: TestContext.Current.CancellationToken);
        var includedDiagnostic = RoslynTestFactory.CreateDiagnostic("Included", syntaxTree, 0, 1);
        var excludedDiagnostic = RoslynTestFactory.CreateDiagnostic("Excluded", syntaxTree, 7, 1);
        var diagnosticsBySyntaxTree = new Dictionary<SyntaxTree, IReadOnlyList<Diagnostic>>
        {
            [syntaxTree] = [includedDiagnostic, excludedDiagnostic],
        };

        var target = new CodeActionProjectDiagnosticCollection(
            [includedDiagnostic, excludedDiagnostic],
            [],
            diagnosticsBySyntaxTree,
            []);

        var result = target.GetDocumentDiagnostics(syntaxTree, new TextSpan(0, 2));

        result.Should().Equal(includedDiagnostic);
    }
}
