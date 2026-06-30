using AwesomeAssertions;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;

using Roslyn.Workbench.Mcp.Contracts.CodeActions;
using Roslyn.Workbench.Mcp.TestSupport;

using Xunit;

namespace Roslyn.Workbench.Mcp.Workspace.Test;

public sealed class CodeActionDescriptorRegistryTests
{
    [Theory]
    [MemberData(nameof(GetVisibleReplayFamilies))]
    public void GIVEN_AuditedReplayProvider_WHEN_Classifying_THEN_ShouldReturnReplay(string providerId, string title)
    {
        var target = new CodeActionDescriptorRegistry();
        var action = new TestReplayCodeAction(title);

        var result = target.Classify(action, providerId, title);

        result.IsVisible.Should().BeTrue();
        result.ExecutionMode.Should().Be(CodeActionExecutionMode.Replay);
    }

    [Fact]
    public void GIVEN_TestRefactoringProviderParameterisedAction_WHEN_Classifying_THEN_ShouldReturnParameterised()
    {
        var target = new CodeActionDescriptorRegistry();
        var action = new TestOptionsCodeAction("Change signature test refactoring");

        var result = target.Classify(action, "Roslyn.Workbench.Mcp.TestSupport.TestRefactoringProvider", action.Title);

        result.IsVisible.Should().BeTrue();
        result.ExecutionMode.Should().Be(CodeActionExecutionMode.Parameterised);
        result.DescribeTool.Should().Be("describe-code-action");
        result.ContextKind.Should().Be(CodeActionDescriptorContextKind.SignaturePlan);
    }

    [Fact]
    public void GIVEN_TestRefactoringProviderUnsupportedOptionsAction_WHEN_Classifying_THEN_ShouldReturnUnsupported()
    {
        var target = new CodeActionDescriptorRegistry();
        var action = new TestOptionsCodeAction("Option gathering test refactoring");

        var result = target.Classify(action, "Roslyn.Workbench.Mcp.TestSupport.TestRefactoringProvider", action.Title);

        result.IsVisible.Should().BeTrue();
        result.ExecutionMode.Should().Be(CodeActionExecutionMode.Unsupported);
        result.UnsupportedReasonCode.Should().Be("UnsupportedCodeActionWithOptions");
    }

    [Fact]
    public void GIVEN_CurrentLedger_WHEN_QueryingHiddenReplayFamilies_THEN_ShouldHaveNoResidualDeferredReplayEntries()
    {
        BuiltInCodeActionAuditCases.HiddenReplayFamilies.Should().BeEmpty();
    }

    [Theory]
    [InlineData("Microsoft.CodeAnalysis.ExtractInterface.ExtractInterfaceCodeRefactoringProvider", "Extract interface")]
    [InlineData("Microsoft.CodeAnalysis.CSharp.CodeFixes.GenerateType.GenerateTypeCodeFixProvider", "Generate type 'MissingType'")]
    public void GIVEN_UnauditedProvider_WHEN_Classifying_THEN_ShouldHideByDefault(string providerId, string title)
    {
        var target = new CodeActionDescriptorRegistry();
        var action = new TestReplayCodeAction(title);

        var result = target.Classify(action, providerId, title);

        result.IsVisible.Should().BeFalse();
        result.ExecutionMode.Should().Be(CodeActionExecutionMode.Unsupported);
    }

    public static TheoryData<string, string> GetHiddenReplayFamilies()
    {
        return CreateTheoryData(BuiltInCodeActionAuditCases.HiddenReplayFamilies);
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

    private sealed class TestOptionsCodeAction : CodeActionWithOptions
    {
        private readonly string _title;

        public TestOptionsCodeAction(string title)
        {
            _title = title;
        }

        public override string Title => _title;

        public override object GetOptions(CancellationToken cancellationToken)
        {
            _ = cancellationToken;
            return new object();
        }

        protected override Task<IEnumerable<CodeActionOperation>> ComputeOperationsAsync(object options, IProgress<CodeAnalysisProgress> progressTracker, CancellationToken cancellationToken)
        {
            _ = options;
            _ = progressTracker;
            _ = cancellationToken;
            return Task.FromResult<IEnumerable<CodeActionOperation>>([]);
        }
    }

    private sealed class TestReplayCodeAction : CodeAction
    {
        private readonly string _title;

        public TestReplayCodeAction(string title)
        {
            _title = title;
        }

        public override string Title => _title;

        protected override Task<IEnumerable<CodeActionOperation>> ComputeOperationsAsync(CancellationToken cancellationToken)
        {
            _ = cancellationToken;
            return Task.FromResult<IEnumerable<CodeActionOperation>>([]);
        }
    }
}
