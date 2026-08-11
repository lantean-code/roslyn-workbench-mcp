using System.Reflection;
using System.Reflection.Emit;

namespace Roslyn.Workbench.Mcp.Plugins.Test.Validation;

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
    public void GIVEN_ReadonlyInjectedInstanceField_WHEN_Inspecting_THEN_ShouldReturnNoWarnings()
    {
        var result = _target.Inspect(typeof(ReadonlyInjectedHandler));

        result.Should().BeEmpty();
    }

    [Fact]
    public void GIVEN_ReadonlyInjectedAutoProperty_WHEN_Inspecting_THEN_ShouldReturnNoWarnings()
    {
        var result = _target.Inspect(typeof(ReadonlyInjectedPropertyHandler));

        result.Should().BeEmpty();
    }

    [Fact]
    public void GIVEN_HandlerInheritsInstanceState_WHEN_Inspecting_THEN_ShouldPublishStateWarning()
    {
        var result = _target.Inspect(typeof(InheritedStateHandler));

        result.Should().ContainSingle(static diagnostic => diagnostic.Id == "PluginHandlerInstanceState");
    }

    [Fact]
    public void GIVEN_HandlerOwnsDisposableField_WHEN_Inspecting_THEN_ShouldPublishDisposableFieldWarning()
    {
        var result = _target.Inspect(typeof(DisposableFieldHandler));

        result.Select(static diagnostic => diagnostic.Id).Should().Equal(
            "PluginHandlerInstanceState",
            "PluginHandlerDisposableField");
    }

    [Fact]
    public void GIVEN_HandlerOwnsDisposableAutoProperty_WHEN_Inspecting_THEN_ShouldPublishDisposableFieldWarning()
    {
        var result = _target.Inspect(typeof(DisposablePropertyHandler));

        result.Select(static diagnostic => diagnostic.Id).Should().Equal(
            "PluginHandlerInstanceState",
            "PluginHandlerDisposableField");
    }

    [Fact]
    public void GIVEN_HandlerOwnsReadonlyMutableCollection_WHEN_Inspecting_THEN_ShouldPublishStateWarning()
    {
        var result = _target.Inspect(typeof(ReadonlyCollectionHandler));

        result.Should().ContainSingle(static diagnostic => diagnostic.Id == "PluginHandlerInstanceState");
    }

    [Fact]
    public void GIVEN_ReadonlyFieldHasMalformedGeneratedName_WHEN_Inspecting_THEN_ShouldPublishStateWarning()
    {
        var assemblyName = new AssemblyName("AssemblyName");
        var assemblyBuilder = AssemblyBuilder.DefineDynamicAssembly(assemblyName, AssemblyBuilderAccess.Run);
        var moduleBuilder = assemblyBuilder.DefineDynamicModule("ModuleName");
        var typeBuilder = moduleBuilder.DefineType("HandlerType");
        _ = typeBuilder.DefineField(
            "<FieldName",
            typeof(object),
            FieldAttributes.Private | FieldAttributes.InitOnly);

        _ = typeBuilder.DefineDefaultConstructor(MethodAttributes.Public);
        var handlerType = typeBuilder.CreateType();

        var result = _target.Inspect(handlerType);

        result.Should().ContainSingle(static diagnostic => diagnostic.Id == "PluginHandlerInstanceState");
    }

    [Fact]
    public void GIVEN_HandlerHasRegisterMethodOnly_WHEN_Inspecting_THEN_ShouldNotPublishLegacyWarning()
    {
        var result = _target.Inspect(typeof(RegisterMethodHandler));

        result.Should().BeEmpty();
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

#pragma warning disable CA1001 // This fixture deliberately models unsupported disposable field ownership.

    private sealed class DisposableFieldHandler
    {
        private readonly MemoryStream _stream = new();

        public long GetLength()
        {
            return _stream.Length;
        }
    }

#pragma warning restore CA1001

#pragma warning disable CA1001 // This fixture deliberately models unsupported disposable property ownership.

    private sealed class DisposablePropertyHandler
    {
        public MemoryStream Stream { get; } = new();
    }

#pragma warning restore CA1001

    private sealed class ReadonlyCollectionHandler
    {
        private readonly List<string> _values = [];

        public int Count => _values.Count;
    }

    private sealed class ReadonlyInjectedHandler
    {
        private readonly RegisterMethodHandler _dependency;

        public ReadonlyInjectedHandler(RegisterMethodHandler dependency)
        {
            _dependency = dependency;
        }

        public void UseDependency()
        {
            _ = _dependency;
        }
    }

    private sealed class ReadonlyInjectedPropertyHandler
    {
        public RegisterMethodHandler Dependency { get; }

        public ReadonlyInjectedPropertyHandler(RegisterMethodHandler dependency)
        {
            Dependency = dependency;
        }
    }

    private sealed class RegisterMethodHandler
    {
        public static void Register()
        {
        }
    }

#pragma warning restore CA1812
}
