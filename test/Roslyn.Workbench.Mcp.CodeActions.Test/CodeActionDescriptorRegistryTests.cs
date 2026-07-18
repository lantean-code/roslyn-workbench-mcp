using Microsoft.CodeAnalysis.CodeActions;

namespace Roslyn.Workbench.Mcp.CodeActions.Test.Resolution;

public sealed class CodeActionDescriptorRegistryTests
{
    [Theory]
    [MemberData(nameof(GetVisibleReplayFamilies))]
    public void GIVEN_AuditedReplayProvider_WHEN_Classifying_THEN_ShouldReturnReplay(string providerId, string title)
    {
        var target = new CodeActionDescriptorRegistry();
        var action = new Mock<CodeAction>();
        action.SetupGet(item => item.Title).Returns(title);

        var result = target.Classify(action.Object, providerId, title);

        result.IsVisible.Should().BeTrue();
        result.ExecutionMode.Should().Be(CodeActionExecutionMode.Replay);
    }

    [Theory]
    [InlineData("Roslyn.Workbench.Mcp.IntegrationTestSupport.TestRefactoringProvider", "Apply test refactoring")]
    [InlineData("Microsoft.CodeAnalysis.ExtractInterface.ExtractInterfaceCodeRefactoringProvider", "Extract interface")]
    [InlineData("Microsoft.CodeAnalysis.CSharp.CodeFixes.GenerateType.GenerateTypeCodeFixProvider", "Generate type 'MissingType'")]
    public void GIVEN_UnauditedProvider_WHEN_Classifying_THEN_ShouldHideByDefault(string providerId, string title)
    {
        var target = new CodeActionDescriptorRegistry();
        var action = new Mock<CodeAction>();
        action.SetupGet(item => item.Title).Returns(title);

        var result = target.Classify(action.Object, providerId, title);

        result.IsVisible.Should().BeFalse();
        result.ExecutionMode.Should().Be(CodeActionExecutionMode.Unsupported);
        result.ContextKind.Should().Be(CodeActionDescriptorContextKind.Unsupported);
    }

    [Fact]
    public void GIVEN_OverrideReturnsDescriptor_WHEN_Classifying_THEN_ShouldUseOverrideWithNormalisedTitle()
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

        var result = target.Classify(action.Object, "ProviderId", "  Title  ");

        result.Should().BeSameAs(expected);
    }

    [Fact]
    public void GIVEN_OverrideDoesNotMatch_WHEN_ClassifyingKnownProvider_THEN_ShouldContinueNormalClassification()
    {
        CodeActionDescriptorOverride descriptorOverride = (_, _, _) => null;
        var target = new CodeActionDescriptorRegistry([descriptorOverride]);
        var action = new Mock<CodeAction>();

        var result = target.Classify(
            action.Object,
            "Microsoft.CodeAnalysis.CodeRefactorings.ExtractMethod.ExtractMethodCodeRefactoringProvider",
            "Title");

        AssertReplayDescriptor(result);
    }

    [Fact]
    public void GIVEN_AddImportFixWithMissingUsingTitle_WHEN_Classifying_THEN_ShouldReturnReplayDescriptor()
    {
        var target = new CodeActionDescriptorRegistry();
        var action = new Mock<CodeAction>();

        var result = target.Classify(
            action.Object,
            "Microsoft.CodeAnalysis.CSharp.AddImport.CSharpAddImportCodeFixProvider",
            "  ADD MISSING USING System.Text  ");

        AssertReplayDescriptor(result);
    }

    [Theory]
    [InlineData("Remove unnecessary usings")]
    [InlineData("REMOVE UNUSED USINGS")]
    public void GIVEN_RemoveUsingTitle_WHEN_Classifying_THEN_ShouldReturnReplayDescriptor(string title)
    {
        var target = new CodeActionDescriptorRegistry();
        var action = new Mock<CodeAction>();

        var result = target.Classify(action.Object, "ProviderId", title);

        AssertReplayDescriptor(result);
    }

    [Fact]
    public void GIVEN_ParameterisedLedgerFamily_WHEN_Classifying_THEN_ShouldReturnDedicatedDescriptor()
    {
        var target = new CodeActionDescriptorRegistry();
        var action = new Mock<CodeAction>();

        var result = target.Classify(
            action.Object,
            "Microsoft.CodeAnalysis.CSharp.UseAutoProperty.CSharpUseAutoPropertyCodeFixProvider",
            "Title");

        result.IsVisible.Should().BeTrue();
        result.ExecutionMode.Should().Be(CodeActionExecutionMode.Parameterised);
        result.ExecutorTool.Should().Be("convert-property");
        result.DescribeTool.Should().Be("describe-code-action");
        result.Requirements.Should().Equal("requires-dedicated-tool", "requires-preflight-description");
        result.ContextKind.Should().Be(CodeActionDescriptorContextKind.None);
    }

    [Fact]
    public void GIVEN_HiddenLedgerFamily_WHEN_Classifying_THEN_ShouldReturnHiddenDescriptor()
    {
        var target = new CodeActionDescriptorRegistry();
        var action = new Mock<CodeAction>();

        var result = target.Classify(
            action.Object,
            "Microsoft.CodeAnalysis.CSharp.CodeRefactorings.AddMissingImports.CSharpAddMissingImportsRefactoringProvider",
            "Title");

        result.IsVisible.Should().BeFalse();
        result.ExecutionMode.Should().Be(CodeActionExecutionMode.Unsupported);
        result.ContextKind.Should().Be(CodeActionDescriptorContextKind.Unsupported);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("Unknown.Provider")]
    public void GIVEN_ProviderIsBlankOrUnknown_WHEN_Classifying_THEN_ShouldReturnHiddenDescriptor(string providerId)
    {
        var target = new CodeActionDescriptorRegistry();
        var action = new Mock<CodeAction>();

        var result = target.Classify(action.Object, providerId, "Title");

        result.IsVisible.Should().BeFalse();
        result.ExecutionMode.Should().Be(CodeActionExecutionMode.Unsupported);
    }

    private static void AssertReplayDescriptor(CodeActionDescriptorEntry result)
    {
        result.IsVisible.Should().BeTrue();
        result.ExecutionMode.Should().Be(CodeActionExecutionMode.Replay);
        result.Requirements.Should().Equal("deterministic-replay");
        result.ContextKind.Should().Be(CodeActionDescriptorContextKind.None);
    }

    public static TheoryData<string, string> GetVisibleReplayFamilies()
    {
        var data = new TheoryData<string, string>();

        foreach (var family in BuiltInCodeActionLedger.Families
            .Where(static family => family.State == BuiltInCodeActionSupportState.SupportedReplay)
            .Where(static family => !string.IsNullOrWhiteSpace(family.ProviderId)))
        {
            data.Add(family.ProviderId, "Title");
        }

        return data;
    }
}
