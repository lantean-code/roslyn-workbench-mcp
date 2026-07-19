namespace Roslyn.Workbench.Mcp.Plugins.Test;

public sealed class PluginHandlerWarningInspectorTests
{
    private readonly PluginHandlerWarningInspector _target;

    public PluginHandlerWarningInspectorTests()
    {
        _target = new PluginHandlerWarningInspector();
    }

    [Fact]
    public void GIVEN_StatefulLegacyHandler_WHEN_Inspecting_THEN_ShouldPublishEveryWarning()
    {
        var result = _target.Inspect(typeof(StatefulLegacyHandler));

        result.Should().HaveCount(4);
        result.Should().OnlyContain(static diagnostic => diagnostic.Severity == DiagnosticSeverity.Warning);
        result.Select(static diagnostic => diagnostic.Id).Should().Equal(
            "PluginHandlerInstanceState",
            "PluginHandlerMutableMembers",
            "PluginHandlerStaticState",
            "PluginLegacyRegistration");
    }

    [Fact]
    public void GIVEN_OnlyConstantAndReadonlyStaticFields_WHEN_Inspecting_THEN_ShouldReturnNoWarnings()
    {
        var result = _target.Inspect(typeof(ReadonlyStaticHandler));

        result.Should().BeEmpty();
    }

    [Fact]
    public void GIVEN_HandlerInheritsInstanceState_WHEN_Inspecting_THEN_ShouldPublishStateWarning()
    {
        var result = _target.Inspect(typeof(InheritedStateHandler));

        result.Should().ContainSingle(static diagnostic => diagnostic.Id == "PluginHandlerInstanceState");
    }

#pragma warning disable CA1812 // Handler fixtures are inspected for reflected state and legacy registration shapes.
    private sealed class StatefulLegacyHandler
    {
        private const int _constantState = 1;
        private static readonly ToolRegistrationMetadata _metadata = new();
        private static int _staticState;
        private int _instanceState;

        public string Value { get; set; } = string.Empty;

        public event EventHandler? Changed;

        public static void Register()
        {
            _ = _constantState;
            _ = _metadata;
        }

        public void Update()
        {
            _instanceState++;
            _staticState++;
            Changed?.Invoke(this, EventArgs.Empty);
        }
    }

#pragma warning disable CA1802 // This fixture must retain readonly static state so the inspector can distinguish it from a constant.
    private sealed class ReadonlyStaticHandler
    {
        private const int _constantValue = 1;
        private static readonly int _readonlyValue = 2;

        public static int GetValue()
        {
            return _constantValue + _readonlyValue;
        }
    }
#pragma warning restore CA1802

    private abstract class StatefulHandlerBase
    {
        private int _state;

        protected void UpdateState()
        {
            _state++;
        }
    }

    private sealed class InheritedStateHandler : StatefulHandlerBase
    {
        public void Update()
        {
            UpdateState();
        }
    }
#pragma warning restore CA1812
}
