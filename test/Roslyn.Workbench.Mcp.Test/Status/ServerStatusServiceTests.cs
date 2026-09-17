using Microsoft.Extensions.Options;

namespace Roslyn.Workbench.Mcp.Test.Status;

public sealed class ServerStatusServiceTests
{
    private readonly Mock<ICodeActionComposition> _codeActionComposition = new Mock<ICodeActionComposition>();
    private readonly Mock<IMsBuildRegistrationService> _msBuildRegistrationService = new Mock<IMsBuildRegistrationService>();
    private readonly Mock<ICommitRecoveryStore> _recoveryStore = new Mock<ICommitRecoveryStore>();
    private readonly Mock<IErrorReportingConsentService> _errorReportingConsentService = new Mock<IErrorReportingConsentService>();
    private readonly Mock<IErrorReportDispatcher> _errorReportDispatcher = new Mock<IErrorReportDispatcher>();
    private readonly Mock<IWorkspaceAuthority> _workspaceAuthority = new Mock<IWorkspaceAuthority>();
    private readonly Mock<ICommitConfirmationState> _commitConfirmationState = new Mock<ICommitConfirmationState>();

    public ServerStatusServiceTests()
    {
        _msBuildRegistrationService
            .SetupGet(item => item.CurrentStatus)
            .Returns(new ComponentStatus
            {
                IsAvailable = true,
                Version = "1.0.0",
                Message = "MSBuildPath",
            });

        _codeActionComposition
            .SetupGet(item => item.Status)
            .Returns(CodeActionCompositionStatus.Available());

        _errorReportDispatcher
            .SetupGet(item => item.Name)
            .Returns("Dispatcher");

        _workspaceAuthority.SetupGet(item => item.ExternalDocumentPolicy).Returns(ExternalDocumentPolicy.AllowReadOnly);
    }

    [Fact]
    public async Task GIVEN_StandardDetail_WHEN_GettingStatus_THEN_ShouldReturnSummaryWithoutExpandedBranches()
    {
        var pluginSnapshot = CreatePluginSnapshot();
        var target = CreateTarget(new StartupOptions(), pluginSnapshot);

        var result = await target.GetStatusAsync(StatusDetailLevel.Standard, clientSupportsElicitation: null, CancellationToken.None);

        var data = result.Data ?? throw new InvalidOperationException("The status response did not contain data.");
        data.ToolCount.Should().Be(
            pluginSnapshot.Tools.Count
            + ServerOwnedToolRegistration.GetPublishedToolCount(
                new ErrorReportingOptions(),
                OperationalPolicyResolver.Resolve(OperationalMode.InspectionOnly)));

        var msBuild = data.MsBuild ?? throw new InvalidOperationException("The status response did not contain MSBuild status.");
        var codeActions = data.CodeActions ?? throw new InvalidOperationException("The status response did not contain code-action status.");
        msBuild.IsAvailable.Should().BeTrue();
        codeActions.IsAvailable.Should().BeTrue();
        data.Plugins.Should().BeNull();
        data.Configuration.Should().BeNull();
        data.StartupWarnings.Should().BeNull();
        data.Recovery.Should().BeNull();
        _recoveryStore.Verify(item => item.GetStatusesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task GIVEN_FullDetail_WHEN_GettingStatus_THEN_ShouldReturnConfigurationPluginsAndRecovery()
    {
        var options = new StartupOptions
        {
            DefaultMaxResults = 100,
            MaxConcurrentQueries = 2,
            MaxTransactionRevisions = 20,
            CodeActionReferenceLifetime = TimeSpan.FromMinutes(5),
            StateDirectory = "/state",
        };

        var recovery = new RecoveryStatus
        {
            CommitId = "CommitId",
        };

        _recoveryStore.Setup(item => item.GetStatusesAsync(TestContext.Current.CancellationToken)).ReturnsAsync([recovery]);
        var pluginSnapshot = CreatePluginSnapshot();
        var startupWarning = new WarningInfo
        {
            Code = "Code",
            Message = "Message",
        };

        var target = CreateTarget(options, pluginSnapshot, startupWarnings: [startupWarning]);

        var result = await target.GetStatusAsync(StatusDetailLevel.Full, clientSupportsElicitation: true, TestContext.Current.CancellationToken);

        var data = result.Data ?? throw new InvalidOperationException("The status response did not contain data.");
        data.Configuration.Should().NotBeNull();
        data.Configuration!.DefaultMaxResults.Should().Be(100);
        data.Configuration.OperationalMode.Should().Be("inspection-only");
        data.Configuration.SourceMutationEnabled.Should().BeFalse();
        data.Configuration.ExternalPluginsEnabled.Should().BeFalse();
        data.Configuration.CommitConfirmationRequired.Should().BeFalse();
        data.Configuration.ReceiptApprovalRequired.Should().BeFalse();
        data.Configuration.CompilerValidationRequired.Should().BeFalse();
        data.Configuration.ClientSupportsElicitation.Should().BeTrue();
        data.Configuration.CommitConfirmationState.Should().Be("not-required");
        data.Configuration.WorkspaceAdmission.Should().Be("Unrestricted");
        data.Configuration.AllowedWorkspaceRootCount.Should().Be(0);
        data.Configuration.ExternalDocumentPolicy.Should().Be("allow-read-only");
        data.Configuration.ErrorReporting!.Provider.Should().Be("Dispatcher");
        data.StartupWarnings.Should().ContainSingle().Which.Should().Be(startupWarning);
        data.Plugins.Should().BeEquivalentTo(pluginSnapshot.Plugins);
        data.Recovery.Should().ContainSingle().Which.Should().Be(recovery);
    }

    [Theory]
    [InlineData((int)OperationalMode.Transactional, false, "transactional", true, false, "required")]
    [InlineData((int)OperationalMode.Transactional, true, "transactional", true, false, "approved-for-session")]
    [InlineData((int)OperationalMode.ApprovalRequired, false, "approval-required", false, true, "not-required")]
    [InlineData((int)OperationalMode.AutonomousTrusted, false, "autonomous-trusted", false, false, "not-required")]
    public async Task GIVEN_OperationalPolicy_WHEN_GettingFullStatus_THEN_ShouldPublishEffectivePrimitives(
        int modeValue,
        bool sessionApproved,
        string expectedMode,
        bool expectedConfirmation,
        bool expectedReceiptApproval,
        string expectedConfirmationState)
    {
        var mode = (OperationalMode)modeValue;
        _recoveryStore.Setup(item => item.GetStatusesAsync(CancellationToken.None)).ReturnsAsync([]);
        _commitConfirmationState.SetupGet(item => item.IsApprovedForSession).Returns(sessionApproved);
        var options = new StartupOptions
        {
            OperationalMode = mode,
            ExternalPluginsEnabled = true,
        };

        var policy = OperationalPolicyResolver.Resolve(mode);
        var target = CreateTarget(
            options,
            new PluginCatalogSnapshot(),
            operationalPolicy: policy);

        var result = await target.GetStatusAsync(StatusDetailLevel.Full, clientSupportsElicitation: false, CancellationToken.None);

        var configuration = result.Data!.Configuration!;
        configuration.OperationalMode.Should().Be(expectedMode);
        configuration.SourceMutationEnabled.Should().BeTrue();
        configuration.ExternalPluginsEnabled.Should().BeTrue();
        configuration.CommitConfirmationRequired.Should().Be(expectedConfirmation);
        configuration.ReceiptApprovalRequired.Should().Be(expectedReceiptApproval);
        configuration.ClientSupportsElicitation.Should().BeFalse();
        configuration.CommitConfirmationState.Should().Be(expectedConfirmationState);
    }

    [Fact]
    public async Task GIVEN_UnsupportedOperationalMode_WHEN_GettingFullStatus_THEN_ShouldRejectInvalidRuntimeState()
    {
        _recoveryStore.Setup(item => item.GetStatusesAsync(CancellationToken.None)).ReturnsAsync([]);
        var policy = new OperationalPolicy
        {
            Mode = (OperationalMode)999,
            SourceMutation = SourceMutationPolicy.Enabled,
            CommitAuthorisation = CommitAuthorisationPolicy.None,
            CommitValidation = CommitValidationPolicy.None,
        };

        var target = CreateTarget(
            new StartupOptions(),
            new PluginCatalogSnapshot(),
            operationalPolicy: policy);

        var action = async () => await target.GetStatusAsync(StatusDetailLevel.Full, clientSupportsElicitation: null, CancellationToken.None);

        await action.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("The operational mode is not supported.");
    }

    [Theory]
    [InlineData((int)ErrorReportingConsentMode.Never, (int)ErrorReportingConsentState.Disabled)]
    [InlineData((int)ErrorReportingConsentMode.Prompt, (int)ErrorReportingConsentState.PromptRequired)]
    [InlineData((int)ErrorReportingConsentMode.Always, (int)ErrorReportingConsentState.AlwaysApproved)]
    public async Task GIVEN_ConfiguredErrorReportingMode_WHEN_GettingFullStatus_THEN_ShouldPublishEffectiveConsent(
        int consentModeValue,
        int consentStateValue)
    {
        var consentMode = (ErrorReportingConsentMode)consentModeValue;
        var consentState = (ErrorReportingConsentState)consentStateValue;
        var options = new StartupOptions
        {
            ErrorReporting = new ErrorReportingOptions
            {
                ConsentMode = consentMode,
            },
        };

        _errorReportingConsentService.Setup(item => item.GetState()).Returns(consentState);
        _recoveryStore.Setup(item => item.GetStatusesAsync(CancellationToken.None)).ReturnsAsync([]);
        var target = CreateTarget(options, new PluginCatalogSnapshot());

        var result = await target.GetStatusAsync(StatusDetailLevel.Full, clientSupportsElicitation: null, CancellationToken.None);

        var errorReporting = result.Data!.Configuration!.ErrorReporting!;
        errorReporting.ConsentMode.Should().Be(consentMode.ToString());
        errorReporting.ConsentState.Should().Be(consentState.ToString());
    }

    [Fact]
    public async Task GIVEN_RestrictedAuthorityAndOutsideRecovery_WHEN_GettingFullStatus_THEN_ShouldProjectNonSensitiveAction()
    {
        _workspaceAuthority.SetupGet(item => item.IsRestricted).Returns(true);
        _workspaceAuthority.SetupGet(item => item.AllowedRootCount).Returns(2);
        _workspaceAuthority.SetupGet(item => item.ExternalDocumentPolicy).Returns(ExternalDocumentPolicy.RejectWorkspace);
        var recovery = new RecoveryStatus
        {
            CommitId = "CommitId",
            WorkspaceRoot = "/outside",
            SolutionPath = "/outside/Solution.slnx",
        };

        _recoveryStore.Setup(item => item.GetStatusesAsync(CancellationToken.None)).ReturnsAsync([recovery]);
        var target = CreateTarget(new StartupOptions(), new PluginCatalogSnapshot());

        var result = await target.GetStatusAsync(StatusDetailLevel.Full, clientSupportsElicitation: null, CancellationToken.None);

        var configuration = result.Data!.Configuration!;
        configuration.WorkspaceAdmission.Should().Be("Restricted");
        configuration.AllowedWorkspaceRootCount.Should().Be(2);
        configuration.ExternalDocumentPolicy.Should().Be("reject-workspace");
        var projectedRecovery = result.Data.Recovery.Should().ContainSingle().Which;
        projectedRecovery.Code.Should().Be("RecoveryOutsideWorkspaceAuthority");
        projectedRecovery.Message.Should().NotContain("/outside");
        projectedRecovery.SolutionPath.Should().BeEmpty();
    }

    [Fact]
    public async Task GIVEN_RestrictedAuthorityAndCertifiableRecovery_WHEN_GettingFullStatus_THEN_ShouldPreserveRecoveryEvidence()
    {
        _workspaceAuthority.SetupGet(item => item.IsRestricted).Returns(true);
        _workspaceAuthority.Setup(item => item.IsWorkspaceAllowed("/allowed/Solution.slnx", "/allowed")).Returns(true);
        var allowed = new RecoveryStatus
        {
            CommitId = "Allowed",
            WorkspaceRoot = "/allowed",
            SolutionPath = "/allowed/Solution.slnx",
        };

        _recoveryStore.Setup(item => item.GetStatusesAsync(CancellationToken.None)).ReturnsAsync([allowed]);
        var target = CreateTarget(new StartupOptions(), new PluginCatalogSnapshot());

        var result = await target.GetStatusAsync(StatusDetailLevel.Full, clientSupportsElicitation: null, CancellationToken.None);

        result.Data!.Recovery.Should().ContainSingle().Which.Should().Be(allowed);
    }

    [Fact]
    public async Task GIVEN_RestrictedAuthorityAndMalformedRecovery_WHEN_GettingFullStatus_THEN_ShouldHideUncertifiablePath()
    {
        _workspaceAuthority.SetupGet(item => item.IsRestricted).Returns(true);
        var recovery = new RecoveryStatus
        {
            CommitId = "CommitId",
            HasMalformedWorkspaceIdentity = true,
            WorkspaceRoot = "/malformed",
            SolutionPath = "/outside/Solution.slnx",
        };

        _recoveryStore.Setup(item => item.GetStatusesAsync(CancellationToken.None)).ReturnsAsync([recovery]);
        var target = CreateTarget(new StartupOptions(), new PluginCatalogSnapshot());

        var result = await target.GetStatusAsync(StatusDetailLevel.Full, clientSupportsElicitation: null, CancellationToken.None);

        var projectedRecovery = result.Data!.Recovery.Should().ContainSingle().Which;
        projectedRecovery.Code.Should().Be("RecoveryOutsideWorkspaceAuthority");
        projectedRecovery.SolutionPath.Should().BeEmpty();
        projectedRecovery.Message.Should().NotContain("/outside");
        _workspaceAuthority.Verify(
            item => item.IsWorkspaceAllowed(It.IsAny<string>(), It.IsAny<string>()),
            Times.Never);
    }

    [Fact]
    public async Task GIVEN_RestrictedAuthorityAndLegacyRecoveryWithoutWorkspaceRoot_WHEN_GettingFullStatus_THEN_ShouldHideUncertifiablePath()
    {
        _workspaceAuthority.SetupGet(item => item.IsRestricted).Returns(true);
        var recovery = new RecoveryStatus
        {
            CommitId = "CommitId",
            SolutionPath = "/legacy/Solution.slnx",
        };

        _recoveryStore.Setup(item => item.GetStatusesAsync(CancellationToken.None)).ReturnsAsync([recovery]);
        var target = CreateTarget(new StartupOptions(), new PluginCatalogSnapshot());

        var result = await target.GetStatusAsync(StatusDetailLevel.Full, clientSupportsElicitation: null, CancellationToken.None);

        var projectedRecovery = result.Data!.Recovery.Should().ContainSingle().Which;
        projectedRecovery.Code.Should().Be("RecoveryOutsideWorkspaceAuthority");
        projectedRecovery.SolutionPath.Should().BeEmpty();
        projectedRecovery.Message.Should().NotContain("/legacy");
        _workspaceAuthority.Verify(
            item => item.IsWorkspaceAllowed(It.IsAny<string>(), It.IsAny<string>()),
            Times.Never);
    }

    [Fact]
    public async Task GIVEN_RestrictedAuthorityAndRecoveryWithoutSolutionPath_WHEN_GettingFullStatus_THEN_ShouldHideUncertifiableIdentity()
    {
        _workspaceAuthority.SetupGet(item => item.IsRestricted).Returns(true);
        var recovery = new RecoveryStatus
        {
            CommitId = "CommitId",
            WorkspaceRoot = "/allowed",
        };

        _recoveryStore.Setup(item => item.GetStatusesAsync(CancellationToken.None)).ReturnsAsync([recovery]);
        var target = CreateTarget(new StartupOptions(), new PluginCatalogSnapshot());

        var result = await target.GetStatusAsync(StatusDetailLevel.Full, clientSupportsElicitation: null, CancellationToken.None);

        var projectedRecovery = result.Data!.Recovery.Should().ContainSingle().Which;
        projectedRecovery.Code.Should().Be("RecoveryOutsideWorkspaceAuthority");
        projectedRecovery.SolutionPath.Should().BeEmpty();
        _workspaceAuthority.Verify(
            item => item.IsWorkspaceAllowed(It.IsAny<string>(), It.IsAny<string>()),
            Times.Never);
    }

    [Fact]
    public async Task GIVEN_UnsupportedEffectiveExternalDocumentPolicy_WHEN_GettingFullStatus_THEN_ShouldRejectInvalidRuntimeState()
    {
        _workspaceAuthority.SetupGet(item => item.ExternalDocumentPolicy).Returns((ExternalDocumentPolicy)999);
        _recoveryStore.Setup(item => item.GetStatusesAsync(CancellationToken.None)).ReturnsAsync([]);
        var target = CreateTarget(new StartupOptions(), new PluginCatalogSnapshot());

        var action = async () => await target.GetStatusAsync(StatusDetailLevel.Full, clientSupportsElicitation: null, CancellationToken.None);

        await action.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("The external-document policy is not supported.");
    }

    [Fact]
    public async Task GIVEN_UnavailableCodeActions_WHEN_GettingStatus_THEN_ShouldReturnDisablementDiagnostics()
    {
        _codeActionComposition
            .SetupGet(item => item.Status)
            .Returns(CodeActionCompositionStatus.Unavailable("Code-action composition is unavailable."));

        var target = CreateTarget(new StartupOptions(), new PluginCatalogSnapshot());

        var result = await target.GetStatusAsync(StatusDetailLevel.Standard, clientSupportsElicitation: null, CancellationToken.None);

        var data = result.Data ?? throw new InvalidOperationException("The status response did not contain data.");
        var codeActions = data.CodeActions ?? throw new InvalidOperationException("The status response did not contain code-action status.");
        codeActions.IsAvailable.Should().BeFalse();
        codeActions.Message.Should().Be("Code-action composition is unavailable.");
    }

    [Fact]
    public async Task GIVEN_PluginAndCodeActionTools_WHEN_GettingStatus_THEN_ShouldCountEveryToolFamily()
    {
        var pluginTool = new Mock<IRegisteredPluginTool>();
        var firstCodeActionTool = new Mock<IRegisteredCodeActionTool>();
        var secondCodeActionTool = new Mock<IRegisteredCodeActionTool>();
        var pluginSnapshot = new PluginCatalogSnapshot
        {
            Tools = [pluginTool.Object],
        };

        var codeActionSnapshot = new CodeActionCatalogSnapshot
        {
            Tools =
            [
                firstCodeActionTool.Object,
                secondCodeActionTool.Object,
            ],
        };

        var target = CreateTarget(new StartupOptions(), pluginSnapshot, codeActionSnapshot);

        var result = await target.GetStatusAsync(StatusDetailLevel.Standard, clientSupportsElicitation: null, CancellationToken.None);

        result.Data!.ToolCount.Should().Be(
            3 + ServerOwnedToolRegistration.GetPublishedToolCount(
                new ErrorReportingOptions(),
                OperationalPolicyResolver.Resolve(OperationalMode.InspectionOnly)));
    }

    [Fact]
    public async Task GIVEN_RepeatedFullDetail_WHEN_GettingStatus_THEN_ShouldRefreshRuntimeConfigurationProjection()
    {
        _recoveryStore.Setup(item => item.GetStatusesAsync(It.IsAny<CancellationToken>())).ReturnsAsync([]);
        var target = CreateTarget(new StartupOptions(), new PluginCatalogSnapshot());

        var first = await target.GetStatusAsync(StatusDetailLevel.Full, clientSupportsElicitation: null, CancellationToken.None);
        var second = await target.GetStatusAsync(StatusDetailLevel.Full, clientSupportsElicitation: null, CancellationToken.None);

        first.Data!.Configuration.Should().NotBeSameAs(second.Data!.Configuration);
        first.Data.Configuration.Should().BeEquivalentTo(second.Data.Configuration);
    }

    [Fact]
    public async Task GIVEN_CancelledToken_WHEN_GettingStatus_THEN_ShouldThrowOperationCanceledException()
    {
        var target = CreateTarget(new StartupOptions(), new PluginCatalogSnapshot());
        using var cancellationSource = new CancellationTokenSource();
        await cancellationSource.CancelAsync();

        var action = async () => await target.GetStatusAsync(StatusDetailLevel.Standard, clientSupportsElicitation: null, cancellationSource.Token);

        await action.Should().ThrowAsync<OperationCanceledException>();
    }

    private ServerStatusService CreateTarget(
        StartupOptions options,
        PluginCatalogSnapshot pluginSnapshot,
        CodeActionCatalogSnapshot? codeActionSnapshot = null,
        IReadOnlyList<WarningInfo>? startupWarnings = null,
        OperationalPolicy? operationalPolicy = null)
    {
        var configuration = new StartupConfigurationSnapshot
        {
            Options = options,
            Warnings = startupWarnings ?? [],
        };

        codeActionSnapshot ??= new CodeActionCatalogSnapshot();
        var pluginRuntimeCatalog = new PluginRuntimeCatalogSnapshot
        {
            Catalog = pluginSnapshot,
        };

        var pluginCatalogState = new Mock<IPluginCatalogState>();
        pluginCatalogState.SetupGet(static state => state.Current).Returns(pluginRuntimeCatalog);
        operationalPolicy ??= OperationalPolicyResolver.Resolve(options.OperationalMode);

        return new ServerStatusService(
            Options.Create(options),
            operationalPolicy,
            configuration,
            pluginCatalogState.Object,
            codeActionSnapshot,
            _msBuildRegistrationService.Object,
            _codeActionComposition.Object,
            _recoveryStore.Object,
            _errorReportingConsentService.Object,
            _errorReportDispatcher.Object,
            _workspaceAuthority.Object,
            _commitConfirmationState.Object);
    }

    private static PluginCatalogSnapshot CreatePluginSnapshot()
    {
        return new PluginCatalogSnapshot
        {
            Plugins =
            [
                new PluginStatus
                {
                    PluginId = "PluginId",
                    DisplayName = "DisplayName",
                    Version = "1.0.0",
                    SupportedApiVersion = "1.0",
                    Enabled = true,
                },
            ],
        };
    }
}
