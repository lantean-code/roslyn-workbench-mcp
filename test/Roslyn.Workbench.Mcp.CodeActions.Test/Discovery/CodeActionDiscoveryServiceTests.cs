using System.Collections.Frozen;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CodeRefactorings;
using Microsoft.CodeAnalysis.Text;

namespace Roslyn.Workbench.Mcp.CodeActions.Test.Discovery;

#pragma warning disable CA1861 // Fresh mutable arrays keep each discovery scenario isolated from other tests.
public sealed class CodeActionDiscoveryServiceTests
{
    private readonly Mock<ICodeActionProviderSelection> _providerSelection;
    private readonly Mock<ICodeActionPolicy> _policy;
    private readonly CodeActionDiscoveryService _target;

    public CodeActionDiscoveryServiceTests()
    {
        _providerSelection = new Mock<ICodeActionProviderSelection>();
        _policy = new Mock<ICodeActionPolicy>();

        _providerSelection
            .SetupGet(item => item.RefactoringProviders)
            .Returns(FrozenDictionary<string, CodeRefactoringProvider>.Empty);

        _providerSelection
            .SetupGet(item => item.CodeFixProviders)
            .Returns(FrozenDictionary<string, CodeFixProvider>.Empty);

        _policy
            .Setup(item => item.EvaluateProvider(It.IsAny<string>()))
            .Returns(CodeActionPolicyDecision.Allowed());

        _policy
            .Setup(item => item.EvaluateAction(It.IsAny<string>(), It.IsAny<CodeAction>()))
            .Returns(CodeActionPolicyDecision.Allowed());

        _target = new CodeActionDiscoveryService(_providerSelection.Object, _policy.Object);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void GIVEN_RefactoringProvider_WHEN_GettingMatchingProviders_THEN_ShouldApplyProviderFilter(bool matches)
    {
        var provider = new Mock<CodeRefactoringProvider>();
        var providerId = _target.GetProviderId(provider.Object);
        _providerSelection
            .SetupGet(item => item.RefactoringProviders)
            .Returns(CreateProviderSelection((providerId, provider.Object)));

        var result = _target.GetMatchingRefactoringProviders(matches ? providerId : "ProviderId");

        if (matches)
        {
            result.Should().ContainSingle().Which.Should().BeSameAs(provider.Object);
        }
        else
        {
            result.Should().BeEmpty();
        }
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void GIVEN_RefactoringProvider_WHEN_FindingProvider_THEN_ShouldReturnMatchingProvider(bool matches)
    {
        var provider = new Mock<CodeRefactoringProvider>();
        var providerId = _target.GetProviderId(provider.Object);
        _providerSelection
            .SetupGet(item => item.RefactoringProviders)
            .Returns(CreateProviderSelection((providerId, provider.Object)));

        var result = _target.FindRefactoringProvider(matches ? providerId : "ProviderId");

        if (matches)
        {
            result.Should().BeSameAs(provider.Object);
        }
        else
        {
            result.Should().BeNull();
        }
    }

    [Fact]
    public void GIVEN_RefactoringProvider_WHEN_GettingAllMatchingProviders_THEN_ShouldReturnProvider()
    {
        var provider = new Mock<CodeRefactoringProvider>();
        var providerId = _target.GetProviderId(provider.Object);
        _providerSelection
            .SetupGet(item => item.RefactoringProviders)
            .Returns(CreateProviderSelection((providerId, provider.Object)));

        var result = _target.GetMatchingRefactoringProviders(providerId: null);

        result.Should().ContainSingle().Which.Should().BeSameAs(provider.Object);
    }

    [Fact]
    public void GIVEN_MultipleRefactoringProviders_WHEN_GettingAllMatchingProviders_THEN_ShouldReturnAllProviders()
    {
        var firstProvider = new Mock<CodeRefactoringProvider>();
        var secondProvider = new Mock<CodeRefactoringProvider>();
        _providerSelection
            .SetupGet(item => item.RefactoringProviders)
            .Returns(CreateProviderSelection(
                ("FirstProviderId", firstProvider.Object),
                ("SecondProviderId", secondProvider.Object)));

        var result = _target.GetMatchingRefactoringProviders(providerId: null);

        result.Should().HaveCount(2);
    }

    [Fact]
    public void GIVEN_RequestedRefactoringProviderIsExcludedByPolicy_WHEN_GettingMatchingProviders_THEN_ShouldReturnNoProviders()
    {
        var provider = new Mock<CodeRefactoringProvider>();
        var providerId = _target.GetProviderId(provider.Object);
        _providerSelection
            .SetupGet(item => item.RefactoringProviders)
            .Returns(CreateProviderSelection((providerId, provider.Object)));

        _policy
            .Setup(item => item.EvaluateProvider(providerId))
            .Returns(CodeActionPolicyDecision.Excluded("ReasonCode"));

        var result = _target.GetMatchingRefactoringProviders(providerId);

        result.Should().BeEmpty();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    public void GIVEN_CodeFixProvider_WHEN_GettingAllMatchingProviders_THEN_ShouldReturnProvider(string? providerId)
    {
        var provider = new Mock<CodeFixProvider>();
        var actualProviderId = _target.GetProviderId(provider.Object);
        _providerSelection
            .SetupGet(item => item.CodeFixProviders)
            .Returns(CreateProviderSelection((actualProviderId, provider.Object)));

        var result = _target.GetMatchingCodeFixProviders(providerId);

        result.Should().ContainSingle().Which.Should().BeSameAs(provider.Object);
    }

    [Fact]
    public void GIVEN_MultipleCodeFixProviders_WHEN_GettingAllMatchingProviders_THEN_ShouldReturnAllProviders()
    {
        var firstProvider = new Mock<CodeFixProvider>();
        var secondProvider = new Mock<CodeFixProvider>();
        _providerSelection
            .SetupGet(item => item.CodeFixProviders)
            .Returns(CreateProviderSelection(
                ("FirstProviderId", firstProvider.Object),
                ("SecondProviderId", secondProvider.Object)));

        var result = _target.GetMatchingCodeFixProviders(providerId: null);

        result.Should().HaveCount(2);
    }

    [Fact]
    public void GIVEN_CodeFixProviderIsExcludedByPolicy_WHEN_GettingAllMatchingProviders_THEN_ShouldOmitProvider()
    {
        var provider = new Mock<CodeFixProvider>();
        var providerId = _target.GetProviderId(provider.Object);
        _providerSelection
            .SetupGet(item => item.CodeFixProviders)
            .Returns(CreateProviderSelection((providerId, provider.Object)));

        _policy
            .Setup(item => item.EvaluateProvider(providerId))
            .Returns(CodeActionPolicyDecision.Excluded("ReasonCode"));

        var result = _target.GetMatchingCodeFixProviders(providerId: null);

        result.Should().BeEmpty();
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void GIVEN_CodeFixProvider_WHEN_GettingMatchingProviders_THEN_ShouldApplyProviderFilter(bool matches)
    {
        var provider = new Mock<CodeFixProvider>();
        var providerId = _target.GetProviderId(provider.Object);
        _providerSelection
            .SetupGet(item => item.CodeFixProviders)
            .Returns(CreateProviderSelection((providerId, provider.Object)));

        var result = _target.GetMatchingCodeFixProviders(matches ? providerId : "ProviderId");

        if (matches)
        {
            result.Should().ContainSingle().Which.Should().BeSameAs(provider.Object);
        }
        else
        {
            result.Should().BeEmpty();
        }
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void GIVEN_CodeFixProvider_WHEN_FindingProvider_THEN_ShouldReturnMatchingProvider(bool matches)
    {
        var provider = new Mock<CodeFixProvider>();
        var providerId = _target.GetProviderId(provider.Object);
        _providerSelection
            .SetupGet(item => item.CodeFixProviders)
            .Returns(CreateProviderSelection((providerId, provider.Object)));

        var result = _target.FindCodeFixProvider(matches ? providerId : "ProviderId");

        if (matches)
        {
            result.Should().BeSameAs(provider.Object);
        }
        else
        {
            result.Should().BeNull();
        }
    }

    [Fact]
    public async Task GIVEN_RefactoringProviderWithNestedActions_WHEN_DiscoveringRefactorings_THEN_ShouldFlattenLeafActions()
    {
        using var roslyn = RoslynTestFactory.CreateDocument("class C { }");
        var firstAction = CodeAction.Create(
            "FirstTitle",
            _ => Task.FromResult(roslyn.Document),
            "FirstEquivalenceKey");

        var secondAction = CodeAction.Create(
            "SecondTitle",
            _ => Task.FromResult(roslyn.Document),
            "SecondEquivalenceKey");

        var groupAction = CodeAction.Create("GroupTitle", [firstAction, secondAction], isInlinable: true);
        var provider = new Mock<CodeRefactoringProvider>();
        provider.Setup(item => item.ComputeRefactoringsAsync(It.IsAny<CodeRefactoringContext>()))
            .Returns((CodeRefactoringContext context) =>
            {
                context.RegisterRefactoring(groupAction);
                return Task.CompletedTask;
            });

        var providerId = _target.GetProviderId(provider.Object);
        var result = await _target.DiscoverRefactoringsAsync(
            provider.Object,
            roslyn.Document,
            new TextSpan(0, 1),
            TestContext.Current.CancellationToken);

        result.Select(item => item.Title).Should().Equal("FirstTitle", "SecondTitle");
        result.Select(item => item.ActionPath).Should().BeEquivalentTo(new[] { new[] { 0, 0 }, new[] { 0, 1 } });
        result.Should().OnlyContain(item =>
            item.Kind == DiscoveredActionKind.Refactoring
            && item.ProviderId == providerId
            && item.TargetSpan == new TextSpan(0, 1)
            && item.DiagnosticIds.Count == 0);
    }

    [Fact]
    public async Task GIVEN_NoFixableDiagnostics_WHEN_DiscoveringCodeFixes_THEN_ShouldNotInvokeProvider()
    {
        using var roslyn = RoslynTestFactory.CreateDocument("class C { }");
        var syntaxTree = await roslyn.Document.GetSyntaxTreeAsync(TestContext.Current.CancellationToken);
        var requiredSyntaxTree = syntaxTree ?? throw new InvalidOperationException("The test document has no syntax tree.");
        var diagnostic = RoslynTestFactory.CreateDiagnostic("OtherDiagnostic", requiredSyntaxTree, 0, 1);
        var provider = new Mock<CodeFixProvider>();
        provider.SetupGet(item => item.FixableDiagnosticIds).Returns(["FixableDiagnostic"]);

        var result = await _target.DiscoverCodeFixesAsync(
            provider.Object,
            roslyn.Document,
            [diagnostic],
            TestContext.Current.CancellationToken);

        result.Should().BeEmpty();
        provider.Verify(item => item.RegisterCodeFixesAsync(It.IsAny<CodeFixContext>()), Times.Never);
    }

    [Fact]
    public async Task GIVEN_FixableDiagnosticsAtDifferentSpans_WHEN_DiscoveringCodeFixes_THEN_ShouldRegisterOneContextPerSpan()
    {
        using var roslyn = RoslynTestFactory.CreateDocument("class C { int Value; }");
        var syntaxTree = await roslyn.Document.GetSyntaxTreeAsync(TestContext.Current.CancellationToken);
        var requiredSyntaxTree = syntaxTree ?? throw new InvalidOperationException("The test document has no syntax tree.");
        var firstDiagnostic = RoslynTestFactory.CreateDiagnostic("FirstDiagnostic", requiredSyntaxTree, 0, 1);
        var duplicateFirstDiagnostic = RoslynTestFactory.CreateDiagnostic("FirstDiagnostic", requiredSyntaxTree, 0, 1);
        var secondDiagnostic = RoslynTestFactory.CreateDiagnostic("SecondDiagnostic", requiredSyntaxTree, 0, 1);
        var thirdDiagnostic = RoslynTestFactory.CreateDiagnostic("FirstDiagnostic", requiredSyntaxTree, 2, 1);
        var ignoredDiagnostic = RoslynTestFactory.CreateDiagnostic("IgnoredDiagnostic", requiredSyntaxTree, 4, 1);
        var provider = new Mock<CodeFixProvider>();
        var fixAllProvider = new Mock<FixAllProvider>();
        provider.SetupGet(item => item.FixableDiagnosticIds).Returns(["FirstDiagnostic", "SecondDiagnostic"]);
        provider.Setup(item => item.GetFixAllProvider()).Returns(fixAllProvider.Object);
        fixAllProvider
            .Setup(item => item.GetSupportedFixAllScopes())
            .Returns([FixAllScope.Document, FixAllScope.Document, FixAllScope.Project, FixAllScope.Solution, FixAllScope.ContainingType]);

        provider.Setup(item => item.RegisterCodeFixesAsync(It.IsAny<CodeFixContext>()))
            .Returns((CodeFixContext context) =>
            {
                var action = CodeAction.Create(
                    $"Title{context.Span.Start}",
                    _ => Task.FromResult(roslyn.Document),
                    $"EquivalenceKey{context.Span.Start}");

                context.RegisterCodeFix(action, context.Diagnostics);
                return Task.CompletedTask;
            });

        var providerId = _target.GetProviderId(provider.Object);
        var result = await _target.DiscoverCodeFixesAsync(
            provider.Object,
            roslyn.Document,
            [firstDiagnostic, duplicateFirstDiagnostic, secondDiagnostic, thirdDiagnostic, ignoredDiagnostic],
            TestContext.Current.CancellationToken);

        result.Select(item => item.Title).Should().Equal("Title0", "Title2");
        result.Select(item => item.TargetSpan).Should().Equal(new TextSpan(0, 1), new TextSpan(2, 1));
        result[0].DiagnosticIds.Should().Equal("FirstDiagnostic", "SecondDiagnostic");
        result[1].DiagnosticIds.Should().Equal("FirstDiagnostic");
        result[0].Diagnostics.Should().BeEquivalentTo(
        [
            new CodeActionDiagnosticIdentity
            {
                Id = "FirstDiagnostic",
                Message = "Message",
                Start = 0,
                Length = 1,
            },
            new CodeActionDiagnosticIdentity
            {
                Id = "SecondDiagnostic",
                Message = "Message",
                Start = 0,
                Length = 1,
            },
        ]);

        result.Should().OnlyContain(item =>
            item.FixAllScopes.SequenceEqual(new[]
            {
                CodeActionFixAllScope.Document,
                CodeActionFixAllScope.Project,
                CodeActionFixAllScope.Solution,
            }));

        result.Should().OnlyContain(item =>
            item.Kind == DiscoveredActionKind.CodeFix
            && item.ProviderId == providerId
            && item.ActionPath.SequenceEqual(new[] { 0 }));

        provider.Verify(item => item.RegisterCodeFixesAsync(
            It.Is<CodeFixContext>(context => context.Span == new TextSpan(0, 1) && context.Diagnostics.Length == 3)), Times.Once);

        provider.Verify(item => item.RegisterCodeFixesAsync(
            It.Is<CodeFixContext>(context => context.Span == new TextSpan(2, 1) && context.Diagnostics.Length == 1)), Times.Once);
    }

    [Fact]
    public async Task GIVEN_CodeFixHasNoEquivalenceKey_WHEN_DiscoveringCodeFixes_THEN_ShouldNotAdvertiseFixAll()
    {
        using var roslyn = RoslynTestFactory.CreateDocument("class C { }");
        var syntaxTree = await roslyn.Document.GetSyntaxTreeAsync(TestContext.Current.CancellationToken);
        var requiredSyntaxTree = syntaxTree ?? throw new InvalidOperationException("The test document has no syntax tree.");
        var diagnostic = RoslynTestFactory.CreateDiagnostic("DiagnosticId", requiredSyntaxTree, 0, 1);
        var provider = new Mock<CodeFixProvider>();
        var fixAllProvider = new Mock<FixAllProvider>();
        provider.SetupGet(item => item.FixableDiagnosticIds).Returns(["DiagnosticId"]);
        provider.Setup(item => item.GetFixAllProvider()).Returns(fixAllProvider.Object);
        fixAllProvider
            .Setup(item => item.GetSupportedFixAllScopes())
            .Returns([FixAllScope.Document]);

        provider.Setup(item => item.RegisterCodeFixesAsync(It.IsAny<CodeFixContext>()))
            .Returns((CodeFixContext context) =>
            {
                var action = CodeAction.Create("Title", _ => Task.FromResult(roslyn.Document));
                context.RegisterCodeFix(action, context.Diagnostics);
                return Task.CompletedTask;
            });

        var result = await _target.DiscoverCodeFixesAsync(
            provider.Object,
            roslyn.Document,
            [diagnostic],
            TestContext.Current.CancellationToken);

        result.Should().ContainSingle().Which.FixAllScopes.Should().BeEmpty();
    }

    [Fact]
    public async Task GIVEN_ProviderIsExcludedByPolicy_WHEN_DiscoveringRefactorings_THEN_ShouldNotInvokeProvider()
    {
        using var roslyn = RoslynTestFactory.CreateDocument("class C { }");
        var provider = new Mock<CodeRefactoringProvider>();
        var providerId = _target.GetProviderId(provider.Object);
        _policy
            .Setup(item => item.EvaluateProvider(providerId))
            .Returns(CodeActionPolicyDecision.Excluded("ReasonCode"));

        var result = await _target.DiscoverRefactoringsAsync(
            provider.Object,
            roslyn.Document,
            new TextSpan(0, 1),
            TestContext.Current.CancellationToken);

        result.Should().BeEmpty();
        provider.Verify(item => item.ComputeRefactoringsAsync(It.IsAny<CodeRefactoringContext>()), Times.Never);
    }

    [Fact]
    public async Task GIVEN_ProviderIsExcludedByPolicy_WHEN_DiscoveringCodeFixes_THEN_ShouldNotInspectDiagnosticsOrInvokeProvider()
    {
        using var roslyn = RoslynTestFactory.CreateDocument("class C { }");
        var provider = new Mock<CodeFixProvider>();
        var providerId = _target.GetProviderId(provider.Object);
        _policy
            .Setup(item => item.EvaluateProvider(providerId))
            .Returns(CodeActionPolicyDecision.Excluded("ReasonCode"));

        var result = await _target.DiscoverCodeFixesAsync(
            provider.Object,
            roslyn.Document,
            [],
            TestContext.Current.CancellationToken);

        result.Should().BeEmpty();
        provider.VerifyGet(item => item.FixableDiagnosticIds, Times.Never);
        provider.Verify(item => item.RegisterCodeFixesAsync(It.IsAny<CodeFixContext>()), Times.Never);
    }

    [Fact]
    public async Task GIVEN_ActionIsExcludedByPolicy_WHEN_DiscoveringRefactorings_THEN_ShouldOmitLeaf()
    {
        using var roslyn = RoslynTestFactory.CreateDocument("class C { }");
        var action = CodeAction.Create("Title", _ => Task.FromResult(roslyn.Document));
        var provider = new Mock<CodeRefactoringProvider>();
        provider.Setup(item => item.ComputeRefactoringsAsync(It.IsAny<CodeRefactoringContext>()))
            .Returns((CodeRefactoringContext context) =>
            {
                context.RegisterRefactoring(action);
                return Task.CompletedTask;
            });

        var providerId = _target.GetProviderId(provider.Object);
        _policy
            .Setup(item => item.EvaluateAction(providerId, action))
            .Returns(CodeActionPolicyDecision.Excluded("ReasonCode"));

        var result = await _target.DiscoverRefactoringsAsync(
            provider.Object,
            roslyn.Document,
            new TextSpan(0, 1),
            TestContext.Current.CancellationToken);

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GIVEN_CachedRefactoringRecipe_WHEN_Rediscovering_THEN_ShouldBypassPolicy()
    {
        using var roslyn = RoslynTestFactory.CreateDocument("class C { }");
        var action = CodeAction.Create("Title", _ => Task.FromResult(roslyn.Document));
        var provider = new Mock<CodeRefactoringProvider>();
        provider.Setup(item => item.ComputeRefactoringsAsync(It.IsAny<CodeRefactoringContext>()))
            .Returns((CodeRefactoringContext context) =>
            {
                context.RegisterRefactoring(action);
                return Task.CompletedTask;
            });

        var result = await _target.RediscoverRefactoringsAsync(
            provider.Object,
            roslyn.Document,
            new TextSpan(0, 1),
            TestContext.Current.CancellationToken);

        result.Should().ContainSingle().Which.Action.Should().BeSameAs(action);
        _policy.Verify(item => item.EvaluateProvider(It.IsAny<string>()), Times.Never);
        _policy.Verify(item => item.EvaluateAction(It.IsAny<string>(), It.IsAny<CodeAction>()), Times.Never);
    }

    [Fact]
    public async Task GIVEN_CachedCodeFixRecipe_WHEN_Rediscovering_THEN_ShouldBypassPolicy()
    {
        using var roslyn = RoslynTestFactory.CreateDocument("class C { }");
        var syntaxTree = await roslyn.Document.GetSyntaxTreeAsync(TestContext.Current.CancellationToken);
        var requiredSyntaxTree = syntaxTree ?? throw new InvalidOperationException("The test document has no syntax tree.");
        var diagnostic = RoslynTestFactory.CreateDiagnostic("DiagnosticId", requiredSyntaxTree, 0, 1);
        var action = CodeAction.Create("Title", _ => Task.FromResult(roslyn.Document));
        var provider = new Mock<CodeFixProvider>();
        provider.SetupGet(item => item.FixableDiagnosticIds).Returns(["DiagnosticId"]);
        provider.Setup(item => item.RegisterCodeFixesAsync(It.IsAny<CodeFixContext>()))
            .Returns((CodeFixContext context) =>
            {
                context.RegisterCodeFix(action, context.Diagnostics);
                return Task.CompletedTask;
            });

        var result = await _target.RediscoverCodeFixesAsync(
            provider.Object,
            roslyn.Document,
            [diagnostic],
            TestContext.Current.CancellationToken);

        result.Should().ContainSingle().Which.Action.Should().BeSameAs(action);
        _policy.Verify(item => item.EvaluateProvider(It.IsAny<string>()), Times.Never);
        _policy.Verify(item => item.EvaluateAction(It.IsAny<string>(), It.IsAny<CodeAction>()), Times.Never);
    }

    private static FrozenDictionary<string, TProvider> CreateProviderSelection<TProvider>(
        params (string ProviderId, TProvider Provider)[] providers)
        where TProvider : class
    {
        var providerDictionary = new Dictionary<string, TProvider>(providers.Length, StringComparer.Ordinal);
        foreach (var (providerId, provider) in providers)
        {
            providerDictionary.Add(providerId, provider);
        }

        return providerDictionary.ToFrozenDictionary(StringComparer.Ordinal);
    }
}
#pragma warning restore CA1861
