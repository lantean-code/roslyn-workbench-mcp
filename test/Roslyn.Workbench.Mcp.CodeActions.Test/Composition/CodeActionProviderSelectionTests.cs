using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CodeRefactorings;

namespace Roslyn.Workbench.Mcp.CodeActions.Test.Composition;

public sealed class CodeActionProviderSelectionTests
{
    [Fact]
    public void GIVEN_ComposedProviders_WHEN_ConstructingSelection_THEN_ShouldPartitionProvidersOnce()
    {
        var includedRefactoring = new Mock<CodeRefactoringProvider>();
        var excludedRefactoring = new Mock<CodeRefactoringProvider>();
        var includedCodeFix = new Mock<CodeFixProvider>();
        var excludedCodeFix = new Mock<CodeFixProvider>();
        var composition = new Mock<ICodeActionComposition>();
        composition
            .SetupGet(item => item.RefactoringProviders)
            .Returns([includedRefactoring.Object, excludedRefactoring.Object]);
        composition
            .SetupGet(item => item.CodeFixProviders)
            .Returns([includedCodeFix.Object, excludedCodeFix.Object]);

        var policy = new Mock<ICodeActionPolicy>();
        policy
            .SetupSequence(item => item.EvaluateProvider(It.IsAny<string>()))
            .Returns(CodeActionPolicyDecision.Allowed())
            .Returns(CodeActionPolicyDecision.Excluded("ReasonCode"))
            .Returns(CodeActionPolicyDecision.Allowed())
            .Returns(CodeActionPolicyDecision.Excluded("ReasonCode"));

        var target = new CodeActionProviderSelection(composition.Object, policy.Object);

        target.RefactoringProviders.Should().ContainSingle().Which.Should().BeSameAs(includedRefactoring.Object);
        target.CodeFixProviders.Should().ContainSingle().Which.Should().BeSameAs(includedCodeFix.Object);
        composition.VerifyGet(item => item.RefactoringProviders, Times.Once);
        composition.VerifyGet(item => item.CodeFixProviders, Times.Once);
        policy.Verify(item => item.EvaluateProvider(It.IsAny<string>()), Times.Exactly(4));
    }
}
