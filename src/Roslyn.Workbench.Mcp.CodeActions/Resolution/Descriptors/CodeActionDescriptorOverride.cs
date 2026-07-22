namespace Roslyn.Workbench.Mcp.CodeActions.Resolution.Descriptors;

internal delegate CodeActionDescriptorEntry? CodeActionDescriptorOverride(
    CodeAction action,
    string providerId,
    string title);
