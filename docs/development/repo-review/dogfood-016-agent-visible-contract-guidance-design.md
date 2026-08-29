# DOGFOOD-016 — Agent-visible contract guidance

**Status:** Implemented and validated; awaiting manual confirmation.

## Issue validation

Published MCP schemas currently describe property names, JSON types, required membership, defaults and validation constraints, but they do not include the guidance already maintained in contract XML documentation. Live inspection of the published `get-document-options` input schema confirmed that `document`, `includeParseOptions`, `includeAnalyzerConfig` and `workspace` had no `description` keyword. The omission makes concise switches and selectors syntactically discoverable without explaining their intended use.

The schema provider uses the MCP SDK schema generator for both input contracts and configured output contracts. The SDK-supported extensibility point is schema-node transformation, and `DescriptionAttribute` provides runtime property metadata without introducing a separate schema-description registry. XML documentation alone is compiler documentation and is not available to the schema generator as property metadata.

## Approved design

- Add `DescriptionAttribute` to every serialized public property on published server-owned, shared Workspace, bundled Core plugin and Code Action request and response contracts.
- Use the existing XML `<summary>` as the semantic source, but write the attribute as concise agent-facing guidance rather than copying C# documentation conventions such as "Gets". Replace XML references with the serialized values the agent sees, and add usage guidance where a property participates in a conditional or mutually exclusive shape. Do not change the XML documentation as part of this schema-publication work.
- Extend the central schema transformation so the same property descriptions are emitted for input schemas and configured output/value schemas, including nested contract objects.
- Keep XML documentation as the public API documentation and keep the adjacent runtime description aligned with it. Do not introduce a central lookup table, source-shape redesign or tool-specific special case.
- Add an area-specific metadata completeness test for server/shared, Core plugin and Code Action contracts, plus generated-schema assertions for representative input, output and nested properties.

## Work allocation

The mechanical contract updates are split into three non-overlapping workstreams: server/shared contracts and central publication, bundled Core plugin contracts, and Code Action contracts. Each workstream owns a distinct source boundary and a uniquely named integration-test file. The combined change must be audited centrally after the parallel work completes.

## Validation requirements

- Every serialized public property in the three published contract areas has a non-empty `DescriptionAttribute`.
- Representative request and response schemas contain the expected `description` keywords.
- Nested shared selector and result properties retain their descriptions after schema extraction and reference rebasing.
- Existing schema shape, validation, enum, default and nullability tests remain unchanged and pass.
- A full build, affected non-acceptance tests and `latest-all` analyzer builds pass before manual confirmation.

## Implementation evidence

The implementation adds runtime descriptions across server-owned and shared Workspace contracts, bundled Core plugin contracts and Code Action contracts. A narrow description transformer is shared by input and configured-output schema generation; input-only nullability, default and conditional-validation behaviour remains isolated in `InputContractSchemaTransformer`. Hand-built result-envelope, mutation and bounded-collection schemas publish descriptions at their construction boundary.

Completeness checks cover every serialized public property in the three built-in contract areas, including the error-reporting DTO graph. Generated-schema checks cover direct and nested input/output metadata, while the existing `McpSdkSchemaProvider`, `ToolSchemaBuilder` and `ToolSchemaFactory` test suites own the server schema assertions.
