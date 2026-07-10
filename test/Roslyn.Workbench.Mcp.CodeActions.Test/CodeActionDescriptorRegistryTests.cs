using Microsoft.CodeAnalysis.CodeActions;
using Roslyn.Workbench.Mcp.CodeActions.Resolution;
using Roslyn.Workbench.Mcp.Contracts.CodeActions;

namespace Roslyn.Workbench.Mcp.CodeActions.Test;

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

    [Fact]
    public void GIVEN_TestRefactoringProviderParameterisedAction_WHEN_Classifying_THEN_ShouldReturnParameterised()
    {
        var target = new CodeActionDescriptorRegistry();
        var action = new Mock<CodeActionWithOptions>();
        action.SetupGet(item => item.Title).Returns("Change signature test refactoring");

        var result = target.Classify(action.Object, "Roslyn.Workbench.Mcp.TestSupport.TestRefactoringProvider", action.Object.Title);

        result.IsVisible.Should().BeTrue();
        result.ExecutionMode.Should().Be(CodeActionExecutionMode.Parameterised);
        result.DescribeTool.Should().Be("describe-code-action");
        result.ContextKind.Should().Be(CodeActionDescriptorContextKind.SignaturePlan);
    }

    [Fact]
    public void GIVEN_TestRefactoringProviderUnsupportedOptionsAction_WHEN_Classifying_THEN_ShouldReturnUnsupported()
    {
        var target = new CodeActionDescriptorRegistry();
        var action = new Mock<CodeActionWithOptions>();
        action.SetupGet(item => item.Title).Returns("Option gathering test refactoring");

        var result = target.Classify(action.Object, "Roslyn.Workbench.Mcp.TestSupport.TestRefactoringProvider", action.Object.Title);

        result.IsVisible.Should().BeTrue();
        result.ExecutionMode.Should().Be(CodeActionExecutionMode.Unsupported);
        result.UnsupportedReasonCode.Should().Be("UnsupportedCodeActionWithOptions");
    }

    [Theory]
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
    }

    public static TheoryData<string, string> GetVisibleReplayFamilies()
    {
        return CreateTheoryData(BuiltInCodeActionAuditCases.VisibleReplayFamilies);
    }

    private static TheoryData<string, string> CreateTheoryData(IReadOnlyList<BuiltInCodeActionAuditCase> families)
    {
        var data = new TheoryData<string, string>();

        foreach (var family in families)
        {
            data.Add(family.ProviderId, family.Title ?? family.TitlePrefix ?? family.ProviderId);
        }

        return data;
    }
}
