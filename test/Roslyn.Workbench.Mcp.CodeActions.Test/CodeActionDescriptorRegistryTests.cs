using Microsoft.CodeAnalysis.CodeActions;
using Roslyn.Workbench.Mcp.CodeActions.Catalog;
using Roslyn.Workbench.Mcp.CodeActions.Contracts;
using Roslyn.Workbench.Mcp.CodeActions.Resolution;

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
