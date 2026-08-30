# DOGFOOD-016 — Agent-visible contract guidance

**Status:** Confirmed through published dogfood validation.

## Issue validation

Published MCP schemas currently describe property names, JSON types, required membership, defaults and validation constraints, but they do not include the guidance already maintained in contract XML documentation. Live inspection of the published `get-document-options` input schema confirmed that `document`, `includeParseOptions`, `includeAnalyzerConfig` and `workspace` had no `description` keyword. The omission makes concise switches and selectors syntactically discoverable without explaining their intended use.

The schema provider uses the MCP SDK schema generator for both input contracts and configured output contracts. The SDK-supported extensibility point is schema-node transformation, and `DescriptionAttribute` provides runtime property metadata without introducing a separate schema-description registry. XML documentation alone is compiler documentation and is not available to the schema generator as property metadata.

## Approved design

- Add `DescriptionAttribute` to serialized public properties on published server-owned, shared Workspace, bundled Core plugin and Code Action request and response contracts when the description provides agent guidance beyond the property name and generated schema.
- Use the existing XML `<summary>` as the semantic source, but write the attribute as concise agent-facing guidance rather than copying C# documentation conventions such as "Gets". Replace XML references with the serialized values the agent sees, and add usage guidance where a property participates in a conditional or mutually exclusive shape. Do not change the XML documentation as part of this schema-publication work.
- Extend the central schema transformation so the same property descriptions are emitted for input schemas and configured output/value schemas, including nested contract objects.
- Keep XML documentation as the public API documentation and keep the adjacent runtime description aligned with it. Do not introduce a central lookup table, source-shape redesign or tool-specific special case.
- Add generated-schema assertions for representative input, output, nested and cross-property guidance. Do not require an attribute on properties whose names and generated schemas are already self-explanatory.
- Treat `SnapshotPrecondition` as an input-schema convention. The input transformer publishes one authoritative instruction for every property of that type, including nullable properties, after ordinary attribute descriptions. Individual request properties do not repeat or override that guidance; output schemas retain their separate result-oriented descriptions.
- Publish each `RequiresExactlyOneAttribute` or `RequiresAtLeastOneAttribute` rule once as a concise type-level description. Keep participating properties optional but publish their supplied value type without `null`, so clients can see that omission is different from an explicit null value. Runtime validation remains authoritative for the cross-property rule and for whether strings and collections are meaningfully non-empty.
- Do not add root `oneOf`, `anyOf`, `allOf` or equivalent conditional composition for these rules. Although structurally accurate, live dogfood showed that a client can flatten composed object alternatives into unions of open `unknown` maps instead of retaining the useful property declarations. The type-level guidance and ordinary property schemas are deliberately more portable across clients while the server still enforces the attributes.
- Retain the request type description in the root input schema and copy that generated root description into the published MCP tool description as `Input:` guidance. This keeps the request attribute and schema pipeline as the single source of truth while supporting clients such as Codex that retain nested schema descriptions but omit the root input-schema description from their callable declaration.

## Work allocation

The mechanical contract updates are split into three non-overlapping workstreams: server/shared contracts and central publication, bundled Core plugin contracts, and Code Action contracts. Each workstream owns a distinct source boundary and a uniquely named integration-test file. The combined change must be audited centrally after the parallel work completes.

## Validation requirements

- Descriptions communicate purpose, units, conditional use, cross-property rules, sentinel meanings or operational effects; descriptions that only restate property names are omitted.
- Representative request and response schemas contain the expected `description` keywords.
- Nested shared selector and result properties retain their descriptions after schema extraction and reference rebasing.
- Existing schema shape, validation, enum, default and nullability tests remain unchanged and pass.
- A full build, affected non-acceptance tests and `latest-all` analyzer builds pass before manual confirmation.

## Implementation evidence

The implementation adds runtime descriptions across server-owned and shared Workspace contracts, bundled Core plugin contracts and Code Action contracts. A narrow description transformer is shared by input and configured-output schema generation; input-only nullability, default and conditional-validation behaviour remains isolated in `InputContractSchemaTransformer`. Hand-built result-envelope, mutation and bounded-collection schemas publish descriptions at their construction boundary.

Generated-schema checks cover representative direct and nested input/output metadata and cross-property guidance, while the existing `McpSdkSchemaProvider`, `ToolSchemaBuilder` and `ToolSchemaFactory` test suites own the server schema assertions. A later portable-schema extension replaces blanket description-presence checks with a 5,000-byte input-schema budget, retains meaningful descriptions and omits attributes that merely repeat the property name or generated schema. It also centralises the `SnapshotPrecondition` input instruction in `InputContractSchemaTransformer`, publishes each `RequiresExactlyOneAttribute` and `RequiresAtLeastOneAttribute` rule as type-level guidance, removes `null` from participating supplied values, copies root request guidance from the generated schema into MCP tool metadata and audits every built-in request schema for that portable shape.
