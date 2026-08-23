using System.Collections.Frozen;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CodeRefactorings;

namespace Roslyn.Workbench.Mcp.CodeActions.Test.Discovery;

#pragma warning disable CA1861 // Fresh mutable arrays keep each catalogue scenario isolated from other tests.
public sealed class CodeActionProviderCatalogTests
{
    private readonly Mock<ICodeActionProviderSelection> _providerSelection;
    private readonly Mock<ICodeActionPolicy> _policy;
    private readonly CodeActionProviderCatalog _target;

    public CodeActionProviderCatalogTests()
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

        _target = new CodeActionProviderCatalog(_providerSelection.Object, _policy.Object);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void GIVEN_RefactoringProvider_WHEN_GettingMatchingProviders_THEN_ShouldApplyProviderFilter(bool matches)
    {
        var provider = new Mock<CodeRefactoringProvider>();
        var providerId = CodeActionProviderIdentity.GetId(provider.Object);
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
        var providerId = CodeActionProviderIdentity.GetId(provider.Object);
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
        var providerId = CodeActionProviderIdentity.GetId(provider.Object);
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
    public void GIVEN_RefactoringProviderIsExcludedByPolicy_WHEN_GettingAllMatchingProviders_THEN_ShouldOmitProvider()
    {
        var provider = new Mock<CodeRefactoringProvider>();
        var providerId = CodeActionProviderIdentity.GetId(provider.Object);
        _providerSelection
            .SetupGet(item => item.RefactoringProviders)
            .Returns(CreateProviderSelection((providerId, provider.Object)));

        _policy
            .Setup(item => item.EvaluateProvider(providerId))
            .Returns(CodeActionPolicyDecision.Excluded("ReasonCode"));

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
        var actualProviderId = CodeActionProviderIdentity.GetId(provider.Object);
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
        var providerId = CodeActionProviderIdentity.GetId(provider.Object);
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
        var providerId = CodeActionProviderIdentity.GetId(provider.Object);
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
        var providerId = CodeActionProviderIdentity.GetId(provider.Object);
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
