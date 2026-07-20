using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CodeRefactorings;
using Microsoft.CodeAnalysis.Text;

namespace Roslyn.Workbench.Mcp.CodeActions.Test.Discovery;

#pragma warning disable CA1861 // Fresh mutable arrays keep each discovery scenario isolated from other tests.
public sealed class CodeActionDiscoveryServiceTests
{
    private readonly Mock<ICodeActionProviderCatalog> _providerCatalog;
    private readonly Mock<ICodeActionDescriptorRegistry> _descriptorRegistry;
    private readonly CodeActionDescriptorEntry _descriptor;
    private readonly CodeActionDiscoveryService _target;

    public CodeActionDiscoveryServiceTests()
    {
        _providerCatalog = new Mock<ICodeActionProviderCatalog>();
        _descriptorRegistry = new Mock<ICodeActionDescriptorRegistry>();
        _descriptor = new CodeActionDescriptorEntry
        {
            ExecutionMode = CodeActionExecutionMode.Replay,
        };

        _providerCatalog.SetupGet(item => item.RefactoringProviders).Returns([]);
        _providerCatalog.SetupGet(item => item.CodeFixProviders).Returns([]);
        _descriptorRegistry
            .Setup(item => item.GetProviderCapability(It.IsAny<string>()))
            .Returns(new CodeActionProviderCapability
            {
                ShouldDiscover = true,
                Descriptor = _descriptor,
            });

        _target = new CodeActionDiscoveryService(_providerCatalog.Object, _descriptorRegistry.Object);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void GIVEN_RefactoringProvider_WHEN_GettingMatchingProviders_THEN_ShouldApplyProviderFilter(bool matches)
    {
        var provider = new Mock<CodeRefactoringProvider>();
        var providerId = _target.GetProviderId(provider.Object);
        _providerCatalog.SetupGet(item => item.RefactoringProviders).Returns([provider.Object]);

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

    [Fact]
    public void GIVEN_RefactoringProvider_WHEN_GettingAllMatchingProviders_THEN_ShouldReturnProvider()
    {
        var provider = new Mock<CodeRefactoringProvider>();
        _providerCatalog.SetupGet(item => item.RefactoringProviders).Returns([provider.Object]);

        var result = _target.GetMatchingRefactoringProviders(providerId: null);

        result.Should().ContainSingle().Which.Should().BeSameAs(provider.Object);
    }

    [Fact]
    public void GIVEN_MultipleRefactoringProviders_WHEN_GettingAllMatchingProviders_THEN_ShouldReturnAllProviders()
    {
        var firstProvider = new Mock<CodeRefactoringProvider>();
        var secondProvider = new Mock<CodeRefactoringProvider>();
        _providerCatalog.SetupGet(item => item.RefactoringProviders).Returns([firstProvider.Object, secondProvider.Object]);

        var result = _target.GetMatchingRefactoringProviders(providerId: null);

        result.Should().HaveCount(2);
    }

    [Fact]
    public void GIVEN_ProviderHasHiddenCapability_WHEN_GettingMatchingProviders_THEN_ShouldExcludeProvider()
    {
        var provider = new Mock<CodeRefactoringProvider>();
        var providerId = _target.GetProviderId(provider.Object);
        _providerCatalog.SetupGet(item => item.RefactoringProviders).Returns([provider.Object]);
        _descriptorRegistry
            .Setup(item => item.GetProviderCapability(providerId))
            .Returns(new CodeActionProviderCapability
            {
                ShouldDiscover = false,
                Descriptor = new CodeActionDescriptorEntry
                {
                    IsVisible = false,
                },
            });

        var result = _target.GetMatchingRefactoringProviders(providerId: null);

        result.Should().BeEmpty();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    public void GIVEN_CodeFixProvider_WHEN_GettingAllMatchingProviders_THEN_ShouldReturnProvider(string? providerId)
    {
        var provider = new Mock<CodeFixProvider>();
        _providerCatalog.SetupGet(item => item.CodeFixProviders).Returns([provider.Object]);

        var result = _target.GetMatchingCodeFixProviders(providerId);

        result.Should().ContainSingle().Which.Should().BeSameAs(provider.Object);
    }

    [Fact]
    public void GIVEN_MultipleCodeFixProviders_WHEN_GettingAllMatchingProviders_THEN_ShouldReturnAllProviders()
    {
        var firstProvider = new Mock<CodeFixProvider>();
        var secondProvider = new Mock<CodeFixProvider>();
        _providerCatalog.SetupGet(item => item.CodeFixProviders).Returns([firstProvider.Object, secondProvider.Object]);

        var result = _target.GetMatchingCodeFixProviders(providerId: null);

        result.Should().HaveCount(2);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void GIVEN_CodeFixProvider_WHEN_GettingMatchingProviders_THEN_ShouldApplyProviderFilter(bool matches)
    {
        var provider = new Mock<CodeFixProvider>();
        var providerId = _target.GetProviderId(provider.Object);
        _providerCatalog.SetupGet(item => item.CodeFixProviders).Returns([provider.Object]);

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
        _providerCatalog.SetupGet(item => item.CodeFixProviders).Returns([provider.Object]);

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
            && item.Descriptor == _descriptor
            && item.DiagnosticIds.Count == 0);
        _descriptorRegistry.Verify(item => item.ResolveActionDependentDescriptor(
            It.IsAny<CodeAction>(),
            It.IsAny<string>(),
            It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task GIVEN_RefactoringProviderHasHiddenCapability_WHEN_DiscoveringRefactorings_THEN_ShouldNotInvokeProvider()
    {
        using var roslyn = RoslynTestFactory.CreateDocument("class C { }");
        var provider = new Mock<CodeRefactoringProvider>();
        var providerId = _target.GetProviderId(provider.Object);
        _descriptorRegistry
            .Setup(item => item.GetProviderCapability(providerId))
            .Returns(new CodeActionProviderCapability
            {
                ShouldDiscover = false,
                Descriptor = _descriptor,
            });

        var result = await _target.DiscoverRefactoringsAsync(
            provider.Object,
            roslyn.Document,
            new TextSpan(0, 1),
            TestContext.Current.CancellationToken);

        result.Should().BeEmpty();
        provider.Verify(item => item.ComputeRefactoringsAsync(It.IsAny<CodeRefactoringContext>()), Times.Never);
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
    public async Task GIVEN_CodeFixProviderHasHiddenCapability_WHEN_DiscoveringCodeFixes_THEN_ShouldNotInspectDiagnosticsOrInvokeProvider()
    {
        using var roslyn = RoslynTestFactory.CreateDocument("class C { }");
        var provider = new Mock<CodeFixProvider>();
        var providerId = _target.GetProviderId(provider.Object);
        _descriptorRegistry
            .Setup(item => item.GetProviderCapability(providerId))
            .Returns(new CodeActionProviderCapability
            {
                ShouldDiscover = false,
                Descriptor = _descriptor,
            });

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
        provider.SetupGet(item => item.FixableDiagnosticIds).Returns(["FirstDiagnostic", "SecondDiagnostic"]);
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
        result[0].DiagnosticIds.Should().Equal("FirstDiagnostic", "SecondDiagnostic");
        result[1].DiagnosticIds.Should().Equal("FirstDiagnostic");
        result.Should().OnlyContain(item =>
            item.Kind == DiscoveredActionKind.CodeFix
            && item.ProviderId == providerId
            && item.Descriptor == _descriptor
            && item.ActionPath.SequenceEqual(new[] { 0 }));
        provider.Verify(item => item.RegisterCodeFixesAsync(
            It.Is<CodeFixContext>(context => context.Span == new TextSpan(0, 1) && context.Diagnostics.Length == 3)), Times.Once);
        provider.Verify(item => item.RegisterCodeFixesAsync(
            It.Is<CodeFixContext>(context => context.Span == new TextSpan(2, 1) && context.Diagnostics.Length == 1)), Times.Once);
    }

    [Fact]
    public async Task GIVEN_ActionDependentCapability_WHEN_DiscoveringRefactoring_THEN_ShouldResolveDescriptorOncePerLeaf()
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
        _descriptorRegistry
            .Setup(item => item.GetProviderCapability(providerId))
            .Returns(new CodeActionProviderCapability
            {
                ShouldDiscover = true,
                Descriptor = _descriptor,
                RequiresActionResolution = true,
            });

        _descriptorRegistry
            .Setup(item => item.ResolveActionDependentDescriptor(action, providerId, "Title"))
            .Returns(_descriptor);

        var result = await _target.DiscoverRefactoringsAsync(
            provider.Object,
            roslyn.Document,
            new TextSpan(0, 1),
            TestContext.Current.CancellationToken);

        result.Should().ContainSingle().Which.Descriptor.Should().BeSameAs(_descriptor);
        _descriptorRegistry.Verify(item => item.ResolveActionDependentDescriptor(action, providerId, "Title"), Times.Once);
    }
}
#pragma warning restore CA1861
