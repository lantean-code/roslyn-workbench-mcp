using Microsoft.CodeAnalysis.CodeActions;

namespace Roslyn.Workbench.Mcp.CodeActions.Test.Resolution.Descriptors;

public sealed class CodeActionDescriptorRegistryTests
{
    [Theory]
    [MemberData(nameof(GetVisibleReplayFamilies))]
    public void GIVEN_AuditedReplayProvider_WHEN_GettingCapability_THEN_ShouldReturnStableReplayDescriptor(string providerId)
    {
        var target = new CodeActionDescriptorRegistry();

        var firstResult = target.GetProviderCapability(providerId);
        var secondResult = target.GetProviderCapability(providerId);

        firstResult.ShouldDiscover.Should().BeTrue();
        firstResult.Descriptor.Should().BeSameAs(secondResult.Descriptor);
        AssertReplayDescriptor(firstResult.Descriptor);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("Roslyn.Workbench.Mcp.IntegrationTestSupport.TestRefactoringProvider")]
    [InlineData("Unknown.Provider")]
    public void GIVEN_UnauditedProvider_WHEN_GettingCapability_THEN_ShouldExcludeProvider(string providerId)
    {
        var target = new CodeActionDescriptorRegistry();

        var result = target.GetProviderCapability(providerId);

        result.ShouldDiscover.Should().BeFalse();
        result.Descriptor.IsVisible.Should().BeFalse();
        result.Descriptor.ExecutionMode.Should().Be(CodeActionExecutionMode.Unsupported);
        result.Descriptor.ContextKind.Should().Be(CodeActionDescriptorContextKind.Unsupported);
    }

    [Fact]
    public void GIVEN_OverrideReturnsDescriptor_WHEN_ResolvingActionDependentDescriptor_THEN_ShouldUseOverrideWithNormalisedTitle()
    {
        var action = new Mock<CodeAction>();
        var expected = new CodeActionDescriptorEntry
        {
            ExecutionMode = CodeActionExecutionMode.Parameterised,
            ExecutorTool = "ExecutorTool",
        };

        CodeActionDescriptorOverride descriptorOverride = (candidate, providerId, title) =>
        {
            candidate.Should().BeSameAs(action.Object);
            providerId.Should().Be("ProviderId");
            title.Should().Be("Title");
            return expected;
        };

        var target = new CodeActionDescriptorRegistry([descriptorOverride]);

        var capability = target.GetProviderCapability("ProviderId");
        var result = target.ResolveActionDependentDescriptor(action.Object, "ProviderId", "  Title  ");

        capability.ShouldDiscover.Should().BeTrue();
        capability.RequiresActionResolution.Should().BeTrue();
        result.Should().BeSameAs(expected);
    }

    [Fact]
    public void GIVEN_OverrideDoesNotMatch_WHEN_ResolvingKnownProvider_THEN_ShouldContinueNormalClassification()
    {
        CodeActionDescriptorOverride descriptorOverride = (_, _, _) => null;
        var target = new CodeActionDescriptorRegistry([descriptorOverride]);
        var action = new Mock<CodeAction>();

        var result = target.ResolveActionDependentDescriptor(
            action.Object,
            "Microsoft.CodeAnalysis.CodeRefactorings.ExtractMethod.ExtractMethodCodeRefactoringProvider",
            "Title");

        AssertReplayDescriptor(result);
    }

    [Theory]
    [InlineData("")]
    [InlineData("ProviderId")]
    public void GIVEN_OverrideDoesNotMatchUnknownProvider_WHEN_ResolvingDescriptor_THEN_ShouldReturnHiddenDescriptor(string providerId)
    {
        CodeActionDescriptorOverride descriptorOverride = (_, _, _) => null;
        var target = new CodeActionDescriptorRegistry([descriptorOverride]);
        var action = new Mock<CodeAction>();

        var result = target.ResolveActionDependentDescriptor(action.Object, providerId, "Title");

        result.IsVisible.Should().BeFalse();
        result.ExecutionMode.Should().Be(CodeActionExecutionMode.Unsupported);
    }

    [Fact]
    public void GIVEN_ParameterisedLedgerFamily_WHEN_GettingCapability_THEN_ShouldReturnDedicatedDescriptor()
    {
        var target = new CodeActionDescriptorRegistry();

        var result = target.GetProviderCapability("Microsoft.CodeAnalysis.CSharp.UseAutoProperty.CSharpUseAutoPropertyCodeFixProvider");

        result.ShouldDiscover.Should().BeTrue();
        result.Descriptor.IsVisible.Should().BeTrue();
        result.Descriptor.ExecutionMode.Should().Be(CodeActionExecutionMode.Parameterised);
        result.Descriptor.ExecutorTool.Should().Be("convert-property");
        result.Descriptor.DescribeTool.Should().Be("describe-code-action");
        result.Descriptor.Requirements.Should().Equal("requires-dedicated-tool", "requires-preflight-description");
        result.Descriptor.ContextKind.Should().Be(CodeActionDescriptorContextKind.None);
    }

    [Fact]
    public void GIVEN_HiddenLedgerFamily_WHEN_GettingCapability_THEN_ShouldExcludeProvider()
    {
        var target = new CodeActionDescriptorRegistry();

        var result = target.GetProviderCapability("Microsoft.CodeAnalysis.CSharp.CodeRefactorings.AddMissingImports.CSharpAddMissingImportsRefactoringProvider");

        result.ShouldDiscover.Should().BeFalse();
        result.Descriptor.IsVisible.Should().BeFalse();
        result.Descriptor.ExecutionMode.Should().Be(CodeActionExecutionMode.Unsupported);
        result.Descriptor.ContextKind.Should().Be(CodeActionDescriptorContextKind.Unsupported);
    }

    public static TheoryData<string> GetVisibleReplayFamilies()
    {
        var data = new TheoryData<string>();
        foreach (var family in BuiltInCodeActionLedger.Families)
        {
            if (family.State != BuiltInCodeActionSupportState.SupportedReplay
                || string.IsNullOrWhiteSpace(family.ProviderId))
            {
                continue;
            }

            data.Add(family.ProviderId);
        }

        return data;
    }

    private static void AssertReplayDescriptor(CodeActionDescriptorEntry result)
    {
        result.IsVisible.Should().BeTrue();
        result.ExecutionMode.Should().Be(CodeActionExecutionMode.Replay);
        result.Requirements.Should().Equal("deterministic-replay");
        result.ContextKind.Should().Be(CodeActionDescriptorContextKind.None);
    }
}
