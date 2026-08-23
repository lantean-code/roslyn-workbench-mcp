using System.Collections.Immutable;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CodeRefactorings;
using Microsoft.CodeAnalysis.Text;

namespace Roslyn.Workbench.Mcp.CodeActions.Test.Discovery;

#pragma warning disable CA1861 // Fresh mutable arrays keep each discovery scenario isolated from other tests.
public sealed class CodeActionDiscoveryServiceTests
{
    private readonly Mock<ICodeActionPolicy> _policy;
    private readonly CodeActionDiscoveryService _target;

    public CodeActionDiscoveryServiceTests()
    {
        _policy = new Mock<ICodeActionPolicy>();

        _policy
            .Setup(item => item.EvaluateProvider(It.IsAny<string>()))
            .Returns(CodeActionPolicyDecision.Allowed());

        _policy
            .Setup(item => item.EvaluateAction(It.IsAny<string>(), It.IsAny<CodeAction>()))
            .Returns(CodeActionPolicyDecision.Allowed());

        _target = new CodeActionDiscoveryService(_policy.Object);
    }

    [Fact]
    public void GIVEN_CodeFixProviderMetadataIsAvailable_WHEN_InspectingProvider_THEN_ShouldReturnCapturedMetadata()
    {
        var provider = new Mock<CodeFixProvider>();
        provider.SetupGet(item => item.FixableDiagnosticIds).Returns(["DiagnosticId"]);

        var result = _target.ReadCodeFixProviderMetadata(
            provider.Object,
            TestContext.Current.CancellationToken);

        result.IsSuccessful.Should().BeTrue();
        result.Value!.Provider.Should().BeSameAs(provider.Object);
        result.Value.FixableDiagnosticIds.Should().Equal("DiagnosticId");
    }

    [Fact]
    public void GIVEN_CodeFixProviderReturnsDefaultDiagnosticIds_WHEN_InspectingProvider_THEN_ShouldNormaliseToEmptyMetadata()
    {
        var provider = new Mock<CodeFixProvider>();
        provider.SetupGet(item => item.FixableDiagnosticIds).Returns(default(ImmutableArray<string>));

        var result = _target.ReadCodeFixProviderMetadata(
            provider.Object,
            TestContext.Current.CancellationToken);

        result.IsSuccessful.Should().BeTrue();
        result.Value!.FixableDiagnosticIds.Should().BeEmpty();
        result.Value.FixableDiagnosticIds.IsDefault.Should().BeFalse();
    }

    [Fact]
    public void GIVEN_CodeFixProviderMetadataThrows_WHEN_InspectingProvider_THEN_ShouldReturnProviderFailure()
    {
        var provider = new Mock<CodeFixProvider>();
        provider.SetupGet(item => item.FixableDiagnosticIds).Throws<InvalidOperationException>();
        var providerId = CodeActionProviderIdentity.GetId(provider.Object);

        var result = _target.ReadCodeFixProviderMetadata(
            provider.Object,
            TestContext.Current.CancellationToken);

        result.IsSuccessful.Should().BeFalse();
        result.Value.Should().BeNull();
        result.Failure.Should().BeEquivalentTo(new CodeActionProviderFailure
        {
            ProviderId = providerId,
            Operation = "reading fixable diagnostic IDs",
            ExceptionType = nameof(InvalidOperationException),
        });
    }

    [Fact]
    public async Task GIVEN_RequestIsCancelledWhileReadingCodeFixMetadata_WHEN_InspectingProvider_THEN_ShouldPropagateCancellation()
    {
        using var cancellationSource = new CancellationTokenSource();
        await cancellationSource.CancelAsync();
        var provider = new Mock<CodeFixProvider>();
        provider.SetupGet(item => item.FixableDiagnosticIds)
            .Throws(new OperationCanceledException(cancellationSource.Token));

        var action = () => _target.ReadCodeFixProviderMetadata(
            provider.Object,
            cancellationSource.Token);

        action.Should().Throw<OperationCanceledException>();
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

        var providerId = CodeActionProviderIdentity.GetId(provider.Object);
        var result = await _target.DiscoverRefactoringsAsync(
            provider.Object,
            roslyn.Document,
            new TextSpan(0, 1),
            TestContext.Current.CancellationToken);

        result.IsSuccessful.Should().BeTrue();
        var actions = result.Value ?? throw new InvalidOperationException("Successful discovery must contain actions.");
        actions.Select(item => item.Title).Should().Equal("FirstTitle", "SecondTitle");
        actions.Select(item => item.ActionPath).Should().BeEquivalentTo(new[] { new[] { 0, 0 }, new[] { 0, 1 } });
        actions.Should().OnlyContain(item =>
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
        var providerMetadata = CreateProviderMetadata(provider.Object, "FixableDiagnostic");

        var result = await _target.DiscoverCodeFixesAsync(
            providerMetadata,
            roslyn.Document,
            [diagnostic],
            TestContext.Current.CancellationToken);

        result.IsSuccessful.Should().BeTrue();
        result.Value.Should().BeEmpty();
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
        var providerMetadata = CreateProviderMetadata(provider.Object, "FirstDiagnostic", "SecondDiagnostic");
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

        var providerId = CodeActionProviderIdentity.GetId(provider.Object);
        var result = await _target.DiscoverCodeFixesAsync(
            providerMetadata,
            roslyn.Document,
            [firstDiagnostic, duplicateFirstDiagnostic, secondDiagnostic, thirdDiagnostic, ignoredDiagnostic],
            TestContext.Current.CancellationToken);

        result.IsSuccessful.Should().BeTrue();
        var actions = result.Value ?? throw new InvalidOperationException("Successful discovery must contain actions.");
        actions.Select(item => item.Title).Should().Equal("Title0", "Title2");
        actions.Select(item => item.TargetSpan).Should().Equal(new TextSpan(0, 1), new TextSpan(2, 1));
        actions[0].DiagnosticIds.Should().Equal("FirstDiagnostic", "SecondDiagnostic");
        actions[1].DiagnosticIds.Should().Equal("FirstDiagnostic");
        actions[0].Diagnostics.Should().BeEquivalentTo(
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

        actions.Should().OnlyContain(item =>
            item.FixAllScopes.SequenceEqual(new[]
            {
                CodeActionFixAllScope.Document,
                CodeActionFixAllScope.Project,
                CodeActionFixAllScope.Solution,
            }));

        actions.Should().OnlyContain(item =>
            item.Kind == DiscoveredActionKind.CodeFix
            && item.ProviderId == providerId
            && item.ActionPath.SequenceEqual(new[] { 0 }));

        provider.Verify(item => item.RegisterCodeFixesAsync(
            It.Is<CodeFixContext>(context => context.Span == new TextSpan(0, 1) && context.Diagnostics.Length == 3)), Times.Once);

        provider.Verify(item => item.RegisterCodeFixesAsync(
            It.Is<CodeFixContext>(context => context.Span == new TextSpan(2, 1) && context.Diagnostics.Length == 1)), Times.Once);
    }

    [Fact]
    public async Task GIVEN_SiblingCodeFixRootsAtDifferentSpans_WHEN_Discovering_THEN_ShouldAssignContextLocalRootPaths()
    {
        using var roslyn = RoslynTestFactory.CreateDocument("class C { int Value; }");
        var syntaxTree = await roslyn.Document.GetSyntaxTreeAsync(TestContext.Current.CancellationToken);
        var requiredSyntaxTree = syntaxTree ?? throw new InvalidOperationException("The test document has no syntax tree.");
        var firstDiagnostic = RoslynTestFactory.CreateDiagnostic("DiagnosticId", requiredSyntaxTree, 0, 1);
        var secondDiagnostic = RoslynTestFactory.CreateDiagnostic("DiagnosticId", requiredSyntaxTree, 2, 1);
        var provider = new Mock<CodeFixProvider>();
        var providerMetadata = CreateProviderMetadata(provider.Object, "DiagnosticId");
        provider.Setup(item => item.RegisterCodeFixesAsync(It.IsAny<CodeFixContext>()))
            .Returns((CodeFixContext context) =>
            {
                var firstAction = CodeAction.Create("Title", _ => Task.FromResult(roslyn.Document), "EquivalenceKey");
                var secondAction = CodeAction.Create("Title", _ => Task.FromResult(roslyn.Document), "EquivalenceKey");
                context.RegisterCodeFix(firstAction, context.Diagnostics);
                context.RegisterCodeFix(secondAction, context.Diagnostics);
                return Task.CompletedTask;
            });

        var result = await _target.DiscoverCodeFixesAsync(
            providerMetadata,
            roslyn.Document,
            [firstDiagnostic, secondDiagnostic],
            TestContext.Current.CancellationToken);

        result.IsSuccessful.Should().BeTrue();
        result.Value!.Select(item => item.ActionPath.Single()).Should().Equal(0, 1, 0, 1);
    }

    [Fact]
    public async Task GIVEN_RefactoringProviderRegistersThenThrows_WHEN_Discovering_THEN_ShouldDiscardPartialActions()
    {
        using var roslyn = RoslynTestFactory.CreateDocument("class C { }");
        var provider = new Mock<CodeRefactoringProvider>();
        var action = CodeAction.Create("Title", _ => Task.FromResult(roslyn.Document));
        provider.Setup(item => item.ComputeRefactoringsAsync(It.IsAny<CodeRefactoringContext>()))
            .Returns((CodeRefactoringContext context) =>
            {
                context.RegisterRefactoring(action);
                throw new InvalidOperationException("Failure");
            });

        var result = await _target.DiscoverRefactoringsAsync(
            provider.Object,
            roslyn.Document,
            new TextSpan(0, 1),
            TestContext.Current.CancellationToken);

        result.IsSuccessful.Should().BeFalse();
        result.Value.Should().BeNull();
        result.Failure!.Operation.Should().Be("computing refactorings");
    }

    [Fact]
    public async Task GIVEN_CodeFixProviderRegistersThenThrows_WHEN_Discovering_THEN_ShouldDiscardPartialActions()
    {
        using var roslyn = RoslynTestFactory.CreateDocument("class C { }");
        var syntaxTree = await roslyn.Document.GetSyntaxTreeAsync(TestContext.Current.CancellationToken);
        var requiredSyntaxTree = syntaxTree ?? throw new InvalidOperationException("The test document has no syntax tree.");
        var diagnostic = RoslynTestFactory.CreateDiagnostic("DiagnosticId", requiredSyntaxTree, 0, 1);
        var provider = new Mock<CodeFixProvider>();
        var providerMetadata = CreateProviderMetadata(provider.Object, "DiagnosticId");
        var action = CodeAction.Create("Title", _ => Task.FromResult(roslyn.Document));
        provider.Setup(item => item.RegisterCodeFixesAsync(It.IsAny<CodeFixContext>()))
            .Returns((CodeFixContext context) =>
            {
                context.RegisterCodeFix(action, context.Diagnostics);
                throw new InvalidOperationException("Failure");
            });

        var result = await _target.DiscoverCodeFixesAsync(
            providerMetadata,
            roslyn.Document,
            [diagnostic],
            TestContext.Current.CancellationToken);

        result.IsSuccessful.Should().BeFalse();
        result.Value.Should().BeNull();
        result.Failure!.Operation.Should().Be("registering code fixes");
    }

    [Fact]
    public async Task GIVEN_RefactoringActionMetadataThrows_WHEN_Discovering_THEN_ShouldReturnProviderFailure()
    {
        using var roslyn = RoslynTestFactory.CreateDocument("class C { }");
        var provider = new Mock<CodeRefactoringProvider>();
        provider.Setup(item => item.ComputeRefactoringsAsync(It.IsAny<CodeRefactoringContext>()))
            .Returns((CodeRefactoringContext context) =>
            {
                context.RegisterRefactoring(new ThrowingTitleCodeAction());
                return Task.CompletedTask;
            });

        var result = await _target.DiscoverRefactoringsAsync(
            provider.Object,
            roslyn.Document,
            new TextSpan(0, 1),
            TestContext.Current.CancellationToken);

        result.IsSuccessful.Should().BeFalse();
        result.Value.Should().BeNull();
        result.Failure!.Operation.Should().Be("projecting refactorings");
    }

    [Fact]
    public async Task GIVEN_CodeFixActionMetadataThrows_WHEN_Discovering_THEN_ShouldReturnProviderFailure()
    {
        using var roslyn = RoslynTestFactory.CreateDocument("class C { }");
        var syntaxTree = await roslyn.Document.GetSyntaxTreeAsync(TestContext.Current.CancellationToken);
        var requiredSyntaxTree = syntaxTree ?? throw new InvalidOperationException("The test document has no syntax tree.");
        var diagnostic = RoslynTestFactory.CreateDiagnostic("DiagnosticId", requiredSyntaxTree, 0, 1);
        var provider = new Mock<CodeFixProvider>();
        var providerMetadata = CreateProviderMetadata(provider.Object, "DiagnosticId");
        provider.Setup(item => item.RegisterCodeFixesAsync(It.IsAny<CodeFixContext>()))
            .Returns((CodeFixContext context) =>
            {
                context.RegisterCodeFix(new ThrowingTitleCodeAction(), context.Diagnostics);
                return Task.CompletedTask;
            });

        var result = await _target.DiscoverCodeFixesAsync(
            providerMetadata,
            roslyn.Document,
            [diagnostic],
            TestContext.Current.CancellationToken);

        result.IsSuccessful.Should().BeFalse();
        result.Value.Should().BeNull();
        result.Failure!.Operation.Should().Be("projecting code fixes");
    }

    [Fact]
    public async Task GIVEN_RequestIsCancelledDuringRefactoringProjection_WHEN_Discovering_THEN_ShouldPropagateCancellation()
    {
        using var roslyn = RoslynTestFactory.CreateDocument("class C { }");
        using var cancellationSource = new CancellationTokenSource();
        await cancellationSource.CancelAsync();
        var provider = new Mock<CodeRefactoringProvider>();
        var action = new ThrowingTitleCodeAction(
            () => new OperationCanceledException(cancellationSource.Token));
        provider.Setup(item => item.ComputeRefactoringsAsync(It.IsAny<CodeRefactoringContext>()))
            .Returns((CodeRefactoringContext context) =>
            {
                context.RegisterRefactoring(action);
                return Task.CompletedTask;
            });

        var invocation = async () => await _target.DiscoverRefactoringsAsync(
            provider.Object,
            roslyn.Document,
            new TextSpan(0, 1),
            cancellationSource.Token);

        await invocation.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task GIVEN_RequestIsCancelledDuringCodeFixProjection_WHEN_Discovering_THEN_ShouldPropagateCancellation()
    {
        using var roslyn = RoslynTestFactory.CreateDocument("class C { }");
        using var cancellationSource = new CancellationTokenSource();
        await cancellationSource.CancelAsync();
        var syntaxTree = await roslyn.Document.GetSyntaxTreeAsync(TestContext.Current.CancellationToken);
        var requiredSyntaxTree = syntaxTree ?? throw new InvalidOperationException("The test document has no syntax tree.");
        var diagnostic = RoslynTestFactory.CreateDiagnostic("DiagnosticId", requiredSyntaxTree, 0, 1);
        var provider = new Mock<CodeFixProvider>();
        var providerMetadata = CreateProviderMetadata(provider.Object, "DiagnosticId");
        var action = new ThrowingTitleCodeAction(
            () => new OperationCanceledException(cancellationSource.Token));
        provider.Setup(item => item.RegisterCodeFixesAsync(It.IsAny<CodeFixContext>()))
            .Returns((CodeFixContext context) =>
            {
                context.RegisterCodeFix(action, context.Diagnostics);
                return Task.CompletedTask;
            });

        var invocation = async () => await _target.DiscoverCodeFixesAsync(
            providerMetadata,
            roslyn.Document,
            [diagnostic],
            cancellationSource.Token);

        await invocation.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task GIVEN_FixAllCapabilityInspectionThrows_WHEN_Discovering_THEN_ShouldDiscardProviderActions()
    {
        using var roslyn = RoslynTestFactory.CreateDocument("class C { }");
        var syntaxTree = await roslyn.Document.GetSyntaxTreeAsync(TestContext.Current.CancellationToken);
        var requiredSyntaxTree = syntaxTree ?? throw new InvalidOperationException("The test document has no syntax tree.");
        var diagnostic = RoslynTestFactory.CreateDiagnostic("DiagnosticId", requiredSyntaxTree, 0, 1);
        var provider = new Mock<CodeFixProvider>();
        var providerMetadata = CreateProviderMetadata(provider.Object, "DiagnosticId");
        provider.Setup(item => item.RegisterCodeFixesAsync(It.IsAny<CodeFixContext>()))
            .Returns((CodeFixContext context) =>
            {
                var action = CodeAction.Create("Title", _ => Task.FromResult(roslyn.Document));
                context.RegisterCodeFix(action, context.Diagnostics);
                return Task.CompletedTask;
            });

        provider.Setup(item => item.GetFixAllProvider()).Throws<InvalidOperationException>();

        var result = await _target.DiscoverCodeFixesAsync(
            providerMetadata,
            roslyn.Document,
            [diagnostic],
            TestContext.Current.CancellationToken);

        result.IsSuccessful.Should().BeFalse();
        result.Value.Should().BeNull();
        result.Failure!.Operation.Should().Be("reading Fix-All capabilities");
    }

    [Fact]
    public async Task GIVEN_RequestIsCancelledByRefactoringProvider_WHEN_Discovering_THEN_ShouldPropagateCancellation()
    {
        using var roslyn = RoslynTestFactory.CreateDocument("class C { }");
        using var cancellationSource = new CancellationTokenSource();
        var provider = new Mock<CodeRefactoringProvider>();
        provider.Setup(item => item.ComputeRefactoringsAsync(It.IsAny<CodeRefactoringContext>()))
            .Returns(async () =>
            {
                await cancellationSource.CancelAsync();
                throw new OperationCanceledException(cancellationSource.Token);
            });

        var action = async () => await _target.DiscoverRefactoringsAsync(
            provider.Object,
            roslyn.Document,
            new TextSpan(0, 1),
            cancellationSource.Token);

        await action.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task GIVEN_RequestIsCancelledByCodeFixProvider_WHEN_Discovering_THEN_ShouldPropagateCancellation()
    {
        using var roslyn = RoslynTestFactory.CreateDocument("class C { }");
        using var cancellationSource = new CancellationTokenSource();
        var syntaxTree = await roslyn.Document.GetSyntaxTreeAsync(TestContext.Current.CancellationToken);
        var requiredSyntaxTree = syntaxTree ?? throw new InvalidOperationException("The test document has no syntax tree.");
        var diagnostic = RoslynTestFactory.CreateDiagnostic("DiagnosticId", requiredSyntaxTree, 0, 1);
        var provider = new Mock<CodeFixProvider>();
        var providerMetadata = CreateProviderMetadata(provider.Object, "DiagnosticId");
        provider.Setup(item => item.RegisterCodeFixesAsync(It.IsAny<CodeFixContext>()))
            .Returns(async () =>
            {
                await cancellationSource.CancelAsync();
                throw new OperationCanceledException(cancellationSource.Token);
            });

        var action = async () => await _target.DiscoverCodeFixesAsync(
            providerMetadata,
            roslyn.Document,
            [diagnostic],
            cancellationSource.Token);

        await action.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task GIVEN_RequestIsCancelledDuringFixAllInspection_WHEN_Discovering_THEN_ShouldPropagateCancellation()
    {
        using var roslyn = RoslynTestFactory.CreateDocument("class C { }");
        using var cancellationSource = new CancellationTokenSource();
        var syntaxTree = await roslyn.Document.GetSyntaxTreeAsync(TestContext.Current.CancellationToken);
        var requiredSyntaxTree = syntaxTree ?? throw new InvalidOperationException("The test document has no syntax tree.");
        var diagnostic = RoslynTestFactory.CreateDiagnostic("DiagnosticId", requiredSyntaxTree, 0, 1);
        var provider = new Mock<CodeFixProvider>();
        var providerMetadata = CreateProviderMetadata(provider.Object, "DiagnosticId");
        provider.Setup(item => item.RegisterCodeFixesAsync(It.IsAny<CodeFixContext>()))
            .Returns((CodeFixContext context) =>
            {
                var codeAction = CodeAction.Create("Title", _ => Task.FromResult(roslyn.Document));
                context.RegisterCodeFix(codeAction, context.Diagnostics);
                return Task.CompletedTask;
            });

        provider.Setup(item => item.GetFixAllProvider())
            .Returns(() =>
            {
                cancellationSource.Cancel();
                throw new OperationCanceledException(cancellationSource.Token);
            });

        var action = async () => await _target.DiscoverCodeFixesAsync(
            providerMetadata,
            roslyn.Document,
            [diagnostic],
            cancellationSource.Token);

        await action.Should().ThrowAsync<OperationCanceledException>();
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
        var providerMetadata = CreateProviderMetadata(provider.Object, "DiagnosticId");
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
            providerMetadata,
            roslyn.Document,
            [diagnostic],
            TestContext.Current.CancellationToken);

        result.IsSuccessful.Should().BeTrue();
        result.Value.Should().ContainSingle().Which.FixAllScopes.Should().BeEmpty();
    }

    [Fact]
    public async Task GIVEN_ProviderIsExcludedByPolicy_WHEN_DiscoveringRefactorings_THEN_ShouldNotInvokeProvider()
    {
        using var roslyn = RoslynTestFactory.CreateDocument("class C { }");
        var provider = new Mock<CodeRefactoringProvider>();
        var providerId = CodeActionProviderIdentity.GetId(provider.Object);
        _policy
            .Setup(item => item.EvaluateProvider(providerId))
            .Returns(CodeActionPolicyDecision.Excluded("ReasonCode"));

        var result = await _target.DiscoverRefactoringsAsync(
            provider.Object,
            roslyn.Document,
            new TextSpan(0, 1),
            TestContext.Current.CancellationToken);

        result.IsSuccessful.Should().BeTrue();
        result.Value.Should().BeEmpty();
        provider.Verify(item => item.ComputeRefactoringsAsync(It.IsAny<CodeRefactoringContext>()), Times.Never);
    }

    [Fact]
    public async Task GIVEN_ProviderIsExcludedByPolicy_WHEN_DiscoveringCodeFixes_THEN_ShouldNotInspectDiagnosticsOrInvokeProvider()
    {
        using var roslyn = RoslynTestFactory.CreateDocument("class C { }");
        var provider = new Mock<CodeFixProvider>();
        var providerId = CodeActionProviderIdentity.GetId(provider.Object);
        _policy
            .Setup(item => item.EvaluateProvider(providerId))
            .Returns(CodeActionPolicyDecision.Excluded("ReasonCode"));
        var providerMetadata = CreateProviderMetadata(provider.Object);

        var result = await _target.DiscoverCodeFixesAsync(
            providerMetadata,
            roslyn.Document,
            [],
            TestContext.Current.CancellationToken);

        result.IsSuccessful.Should().BeTrue();
        result.Value.Should().BeEmpty();
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

        var providerId = CodeActionProviderIdentity.GetId(provider.Object);
        _policy
            .Setup(item => item.EvaluateAction(providerId, action))
            .Returns(CodeActionPolicyDecision.Excluded("ReasonCode"));

        var result = await _target.DiscoverRefactoringsAsync(
            provider.Object,
            roslyn.Document,
            new TextSpan(0, 1),
            TestContext.Current.CancellationToken);

        result.IsSuccessful.Should().BeTrue();
        result.Value.Should().BeEmpty();
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

        result.IsSuccessful.Should().BeTrue();
        result.Value.Should().ContainSingle().Which.Action.Should().BeSameAs(action);
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
        var providerMetadata = CreateProviderMetadata(provider.Object, "DiagnosticId");
        provider.Setup(item => item.RegisterCodeFixesAsync(It.IsAny<CodeFixContext>()))
            .Returns((CodeFixContext context) =>
            {
                context.RegisterCodeFix(action, context.Diagnostics);
                return Task.CompletedTask;
            });

        var result = await _target.RediscoverCodeFixesAsync(
            providerMetadata,
            roslyn.Document,
            [diagnostic],
            TestContext.Current.CancellationToken);

        result.IsSuccessful.Should().BeTrue();
        result.Value.Should().ContainSingle().Which.Action.Should().BeSameAs(action);
        _policy.Verify(item => item.EvaluateProvider(It.IsAny<string>()), Times.Never);
        _policy.Verify(item => item.EvaluateAction(It.IsAny<string>(), It.IsAny<CodeAction>()), Times.Never);
    }

    private static CodeFixProviderMetadata CreateProviderMetadata(
        CodeFixProvider provider,
        params string[] fixableDiagnosticIds)
    {
        return new CodeFixProviderMetadata
        {
            Provider = provider,
            FixableDiagnosticIds = [.. fixableDiagnosticIds],
        };
    }

    private sealed class ThrowingTitleCodeAction : CodeAction
    {
        private readonly Func<Exception> _createException;

        public ThrowingTitleCodeAction()
            : this(static () => new InvalidOperationException("Controlled action metadata failure."))
        {
        }

        public ThrowingTitleCodeAction(Func<Exception> createException)
        {
            _createException = createException;
        }

        public override string Title
        {
            get
            {
                throw _createException();
            }
        }

        protected override Task<IEnumerable<CodeActionOperation>> ComputeOperationsAsync(
            CancellationToken cancellationToken)
        {
            return Task.FromResult<IEnumerable<CodeActionOperation>>([]);
        }
    }
}
#pragma warning restore CA1861
