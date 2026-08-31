# DOGFOOD-015 — Document-options response size

**Status:** Confirmed through published dogfood validation.

## Issue validation

The published `get-document-options` response for `GetDocumentOptionsTool.cs` contained 120 effective analyser-config options. The complete structured result occupied approximately 9,557 JSON characters, of which the options map occupied 8,223 characters, or 86%. The language, nullable context, parse options and applied config paths were accurate and comparatively small.

The existing implementation deliberately enumerates and orders every effective option. Focused unit tests cover document-resolution rejection, C# and unsupported-language projections, complete option ordering, missing syntax trees and cancellation. Integration coverage verifies effective build properties from a real loaded Workspace, and Host contract coverage verifies the required document selector. No acceptance test or Scenario Runner scenario specifically exercises this tool.

The measured size is therefore an agent-facing usability gap rather than a correctness defect or an untested accidental behaviour.

## Approved design

`get-document-options` will make its two detailed top-level projections explicitly optional:

- the default response retains the document reference, language version and nullable context;
- `includeParseOptions` includes the complete parse-options projection;
- `includeAnalyzerConfig` includes the complete effective analyser-config projection;
- both options default to `false` and can be combined when both detailed projections are genuinely required.

The initially implemented named-key discovery design was rejected after live validation. Although it made the default and targeted-value responses concise, returning 120 available names still occupied approximately 7,839 JSON characters compared with 9,573 for the complete option map. Most entries represented flattened naming-rule definitions that agents can normally inspect from project configuration directly and rarely need as evaluated document context.

The revised design keeps the common response concise and makes the exceptional detailed projections straightforward to request. It avoids introducing key discovery, targeted key filtering, categories or pagination for information that is generally not code-driven, while preserving complete effective Roslyn configuration when it is explicitly needed.

## Validation scope

Focused unit coverage must exercise the concise default, independent and combined detailed projections, complete analyser-config ordering, missing syntax trees and cancellation. The existing real-Workspace integration test must explicitly request both detailed projections and verify representative parse and effective analyser values. Host schema coverage must confirm that the two new request properties are optional booleans. Acceptance and Scenario Runner assets remain unchanged because the behaviour is covered below the published-process boundary and neither suite currently owns this tool.
