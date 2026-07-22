using System.Collections.Frozen;

namespace Roslyn.Workbench.Mcp.CodeActions.Resolution.Descriptors;

internal sealed class CodeActionDescriptorRegistry : ICodeActionDescriptorRegistry
{
    private static readonly CodeActionDescriptorEntry _hiddenDescriptor = Hidden();
    private static readonly CodeActionDescriptorEntry _replayDescriptor = Replay();
    private static readonly CodeActionProviderCapability _actionDependentCapability = new()
    {
        ShouldDiscover = true,
        Descriptor = _hiddenDescriptor,
        RequiresActionResolution = true,
    };

    private static readonly CodeActionProviderCapability _unknownCapability = new()
    {
        ShouldDiscover = false,
        Descriptor = _hiddenDescriptor,
    };

    private static readonly FrozenDictionary<string, CodeActionProviderCapability> _providerCapabilities = CreateProviderCapabilities();
    private readonly IReadOnlyList<CodeActionDescriptorOverride> _overrides;

    public CodeActionDescriptorRegistry()
        : this([])
    {
    }

    internal CodeActionDescriptorRegistry(IReadOnlyList<CodeActionDescriptorOverride> overrides)
    {
        _overrides = overrides;
    }

    public CodeActionProviderCapability GetProviderCapability(string providerId)
    {
        if (_overrides.Count > 0)
        {
            return _actionDependentCapability;
        }

        return _providerCapabilities.TryGetValue(providerId, out var capability)
            ? capability
            : _unknownCapability;
    }

    public CodeActionDescriptorEntry ResolveActionDependentDescriptor(CodeAction action, string providerId, string title)
    {
        var normalizedTitle = title.Trim();

        foreach (var descriptorOverride in _overrides)
        {
            var overriddenEntry = descriptorOverride(action, providerId, normalizedTitle);
            if (overriddenEntry is not null)
            {
                return overriddenEntry;
            }
        }

        if (!string.IsNullOrWhiteSpace(providerId)
            && _providerCapabilities.TryGetValue(providerId, out var capability))
        {
            return capability.Descriptor;
        }

        return _hiddenDescriptor;
    }

    private static FrozenDictionary<string, CodeActionProviderCapability> CreateProviderCapabilities()
    {
        var capabilities = new Dictionary<string, CodeActionProviderCapability>(StringComparer.Ordinal);
        foreach (var family in BuiltInCodeActionLedger.Families)
        {
            var descriptor = family.State switch
            {
                BuiltInCodeActionSupportState.SupportedReplay => _replayDescriptor,
                BuiltInCodeActionSupportState.SupportedParameterised => Parameterised(family.ExecutorTool, CodeActionDescriptorContextKind.None),
                _ => _hiddenDescriptor,
            };

            capabilities.Add(family.ProviderId, new CodeActionProviderCapability
            {
                ShouldDiscover = descriptor.IsVisible,
                Descriptor = descriptor,
            });
        }

        return capabilities.ToFrozenDictionary(StringComparer.Ordinal);
    }

    private static CodeActionDescriptorEntry Parameterised(string? executorTool, CodeActionDescriptorContextKind contextKind)
    {
        return new CodeActionDescriptorEntry
        {
            ExecutionMode = CodeActionExecutionMode.Parameterised,
            ExecutorTool = executorTool,
            DescribeTool = "describe-code-action",
            Requirements = ["requires-dedicated-tool", "requires-preflight-description"],
            ContextKind = contextKind,
        };
    }

    private static CodeActionDescriptorEntry Replay()
    {
        return new CodeActionDescriptorEntry
        {
            ExecutionMode = CodeActionExecutionMode.Replay,
            Requirements = ["deterministic-replay"],
            ContextKind = CodeActionDescriptorContextKind.None,
        };
    }

    private static CodeActionDescriptorEntry Hidden()
    {
        return new CodeActionDescriptorEntry
        {
            IsVisible = false,
            ExecutionMode = CodeActionExecutionMode.Unsupported,
            ContextKind = CodeActionDescriptorContextKind.Unsupported,
        };
    }
}
