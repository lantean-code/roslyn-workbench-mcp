using Microsoft.CodeAnalysis.CodeActions;

namespace Roslyn.Workbench.Mcp.CodeActions.Test.Policy;

public sealed class CodeActionPolicyTests
{
    private readonly CodeActionPolicy _target;

    public CodeActionPolicyTests()
    {
        _target = new CodeActionPolicy();
    }

    [Theory]
    [InlineData("Microsoft.CodeAnalysis.CSharp.CodeRefactorings.AddMissingImports.CSharpAddMissingImportsRefactoringProvider", "EditorStateRequired")]
    [InlineData("Microsoft.CodeAnalysis.ChangeSignature.ChangeSignatureCodeRefactoringProvider", "OptionsRequired")]
    [InlineData("Microsoft.CodeAnalysis.GenerateOverrides.GenerateOverridesCodeRefactoringProvider", "OptionsRequired")]
    [InlineData("Microsoft.CodeAnalysis.ExtractInterface.ExtractInterfaceCodeRefactoringProvider", "OptionsRequired")]
    [InlineData("Microsoft.CodeAnalysis.CSharp.CodeRefactorings.ExtractClass.CSharpExtractClassCodeRefactoringProvider", "OptionsRequired")]
    [InlineData("Microsoft.CodeAnalysis.CSharp.CodeRefactorings.MoveStaticMembers.CSharpMoveStaticMembersRefactoringProvider", "OptionsRequired")]
    [InlineData("Microsoft.CodeAnalysis.MoveToNamespace.MoveToNamespaceCodeActionProvider", "OptionsRequired")]
    [InlineData("Microsoft.CodeAnalysis.CSharp.CodeRefactorings.EnableNullable.EnableNullableCodeRefactoringProvider", "ProjectMutationRequired")]
    [InlineData("Microsoft.CodeAnalysis.CSharp.AddMissingReference.CSharpAddMissingReferenceCodeFixProvider", "ProjectMutationRequired")]
    [InlineData("Microsoft.CodeAnalysis.CSharp.AddPackage.CSharpAddSpecificPackageCodeFixProvider", "PackageMutationRequired")]
    [InlineData("Microsoft.CodeAnalysis.CSharp.UpdateProjectToAllowUnsafe.CSharpUpdateProjectToAllowUnsafeCodeFixProvider", "ProjectMutationRequired")]
    [InlineData("Microsoft.CodeAnalysis.CSharp.UpgradeProject.CSharpUpgradeProjectCodeFixProvider", "ProjectMutationRequired")]
    [InlineData("Microsoft.CodeAnalysis.CSharp.Copilot.CSharpCopilotCodeFixProvider", "ExternalIntelligenceRequired")]
    [InlineData("Microsoft.CodeAnalysis.CSharp.Copilot.CSharpImplementNotImplementedExceptionFixProvider", "ExternalIntelligenceRequired")]
    public void GIVEN_ExcludedProvider_WHEN_EvaluatingProvider_THEN_ShouldReturnReason(
        string providerId,
        string reasonCode)
    {
        var result = _target.EvaluateProvider(providerId);

        result.IsAllowed.Should().BeFalse();
        result.ReasonCode.Should().Be(reasonCode);
    }

    [Fact]
    public void GIVEN_UnknownProvider_WHEN_EvaluatingProvider_THEN_ShouldAllowProvider()
    {
        var result = _target.EvaluateProvider("ProviderId");

        result.IsAllowed.Should().BeTrue();
        result.ReasonCode.Should().BeNull();
    }

    [Fact]
    public void GIVEN_OrdinaryAction_WHEN_EvaluatingAction_THEN_ShouldAllowAction()
    {
        var action = new Mock<CodeAction>();

        var result = _target.EvaluateAction("ProviderId", action.Object);

        result.IsAllowed.Should().BeTrue();
        result.ReasonCode.Should().BeNull();
    }

    [Fact]
    public void GIVEN_OptionBackedAction_WHEN_EvaluatingAction_THEN_ShouldExcludeAction()
    {
        var action = new Mock<CodeActionWithOptions>();

        var result = _target.EvaluateAction("ProviderId", action.Object);

        result.IsAllowed.Should().BeFalse();
        result.ReasonCode.Should().Be("OptionsRequired");
    }

    [Fact]
    public void GIVEN_ExcludedProviderWithOptionBackedAction_WHEN_EvaluatingAction_THEN_ShouldReturnProviderReason()
    {
        var action = new Mock<CodeActionWithOptions>();

        var result = _target.EvaluateAction(
            "Microsoft.CodeAnalysis.CSharp.AddPackage.CSharpAddSpecificPackageCodeFixProvider",
            action.Object);

        result.IsAllowed.Should().BeFalse();
        result.ReasonCode.Should().Be("PackageMutationRequired");
    }
}
