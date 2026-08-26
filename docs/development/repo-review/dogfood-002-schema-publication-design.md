# DOGFOOD-002 — Agent-compatible schema publication

## Purpose

DOGFOOD-002 corrects the MCP input-schema shapes exposed to agents. The Host currently publishes valid JSON Schema containing fragmentary `allOf`, `if`, `then`, `not`, `anyOf` and `oneOf` constraints. Codex's callable-type projection does not preserve several of these compositions: 51 of the 56 observed dogfood tool declarations contained `unknown`, 18 contained opaque intersections such as `unknown & unknown`, and nested selector members were partially or completely lost.

The underlying C# contracts and runtime validation remain correct. This work concerns compatibility between the Host's composed input schemas and agent-facing type projection, not missing contract validation.

## Published dogfood evidence

The first complete-alternative implementation was independently reviewed, corrected, built and tested. A pre-commit publish then proved that Codex still projected literal `unknown` in 35 of 56 tool declarations. One occurrence was the harmless open argument map for `workspace-list`; the other 34 hid material nested selector members. The implementation removed every `unknown & unknown` intersection and made `WorkspaceSelector`, direct `ProjectSelector`, direct `DocumentSelector` and `SnapshotPrecondition` concrete, but nested project, document, location, span, selection and symbol-selector members still collapsed.

The published comparison is important: `transaction-preview` projected its document and nested project selector completely, while `find-references` reduced equivalent nested selectors to `unknown`. This initially appeared to be a Codex composition or complexity boundary that valid JSON Schema and unit-level projection proxies did not reproduce. Raw-schema inspection after the second candidate showed that the apparent boundary was instead caused by unresolved local references. The first complete-alternative design remains unacceptable and the candidate was removed from the configured `current` dogfood target.

The second candidate removed at-least-one and exactly-one schema alternatives while retaining complete discriminator branches. After promotion and a desktop MCP restart, it reduced the affected declarations from 35 to 26 and continued to eliminate every `unknown & unknown` intersection. The result still failed the acceptance boundary: 25 non-parameterless tools contained material `unknown` fields, principally `DocumentSelector.project` inside selections and document-scope branches, while a few declarations reduced complete location alternatives to `unknown`. Equivalent selectors remained concrete in simpler tools because those occurrences were inlined instead of represented by a broken `$ref`. The second candidate was therefore also removed from the configured `current` dogfood target.

## Root-cause analysis

The failed candidate published 38 local `$ref` values across exactly the 25 non-parameterless tools whose Codex declarations contained material `unknown`. Every one of those references was unresolved in the published input schema.

There are two causes:

1. `McpSdkSchemaProvider` asks `McpServerTool` to generate a schema for a probe method whose parameter is named `request`, extracts `root.properties.request` and publishes only that fragment. The SDK emits repeated types as absolute same-document JSON Pointers such as `#/properties/request/properties/symbol/...`. `ToolSchemaBuilder.NormalizeExportedSchema` copies root `$defs` but does not rebase pointers into the extracted fragment. Thirty-six references therefore retained a path through the discarded `request` wrapper.
2. The discriminator transformation replaces `ScopeSelector.properties` with complete `oneOf` branches. In `analyze-nullability`, the SDK had already chosen `scope.properties.document.properties.project` as the canonical occurrence of `ProjectSelector`. Moving that occurrence invalidated two later references from location selectors even after the wrapper prefix was removed.

The current tests missed the defect because they inspect inlined representative occurrences or synthetic `$defs` references; none asserts that every same-document reference in the final published input schema resolves from that schema's root. The current design's statement that nested contracts are always emitted through `$defs` is incorrect for ModelContextProtocol 1.4.1. The SDK also deduplicates by emitting absolute pointers to the first occurrence within `properties`.

A temporary diagnostic using the exact second-candidate assemblies verified the path analysis: generating the request type without the probe wrapper produced root-relative references such as `#/properties/symbol/...`, and removing the shape-moving discriminator transformation kept `#/properties/scope/properties/document/properties/project` valid. This diagnostic isolates the causes but is not the proposed production wiring because bypassing `McpServerTool.Create` would make the Host responsible for tracking future MCP SDK schema-generation behaviour.

## Approved third-candidate design

### Preserve MCP SDK schema generation

Retain `McpServerTool.Create`, the probe method and the existing `SchemaCreateOptions` and serializer wiring in `McpSdkSchemaProvider.CreateInputSchemaCore`. The MCP SDK remains responsible for function-schema generation so future SDK changes continue to flow into the Host without parallel wiring or duplicated defaults.

Make the input fragment extraction reference-safe. When publishing `root.properties.request`, rebase same-document references within that subtree from `#/properties/request` to the extracted schema root. For example, `#/properties/request/properties/symbol/...` becomes `#/properties/symbol/...`. Preserve references such as `#/$defs/...` because copied root definitions remain rooted correctly. Reject a local reference that escapes the extracted request subtree or does not resolve after extraction.

Apply rebasing only to input-schema extraction in this work item. Do not change value/output schema generation; its nested wrapper supplies intentional top-level value nullability semantics and DOGFOOD-002 concerns input declarations.

### ModelContextProtocol 2.0 assessment

Do not upgrade the SDK solely for DOGFOOD-002. ModelContextProtocol 2.0.0 retains method-parameter schema generation: parameters remain properties in the root argument dictionary, and a complex `request` parameter remains nested beneath `request`. `McpServerToolCreateOptions` provides an explicit output-schema override but no input-schema override or supported complex-parameter flattening option. The upstream discussion requesting an “explode” facility remains unanswered by an SDK feature.

Version 2.0.0 does contain analogous reference rewriting when the SDK moves an output schema beneath a compatibility `result` wrapper. That implementation explicitly notes that System.Text.Json emits absolute JSON Pointers and rewrites them after relocating the schema. This supports applying the inverse operation at the Host-owned input-fragment extraction boundary, but it does not solve that boundary automatically.

The 2.0.0 upgrade primarily adopts the MCP 2026-07-28 protocol, including discovery-first negotiation, stateless-by-default HTTP behaviour, extension packaging and other protocol changes. Those changes require a separately designed dependency upgrade and broader compatibility validation; they provide no narrow replacement for DOGFOOD-002's reference-aware extraction.

### Do not move SDK-generated object members

Simplify `InputContractSchemaTransformer` so it only applies property-local metadata and validates attribute configuration. It must not replace an object with alternatives or move properties to new JSON Pointer locations.

- `RequiresAtLeastOneAttribute` and `RequiresExactlyOneAttribute`: validate referenced serialized members, publish the SDK object unchanged and retain runtime enforcement.
- `RequiredWhenAttribute` and `ProhibitedUnlessAttribute`: validate the controlling member and expected value, publish the SDK object unchanged and retain runtime enforcement.
- `NonEmptyGuidAttribute`: retain the SDK UUID schema without a Boolean exclusion and retain runtime enforcement.
- nullability and `DefaultValueAttribute`: continue applying property-local metadata because they do not move schema nodes.

`ScopeSelector` will therefore project as one complete construction-guidance object rather than a discriminated union:

```text
{
  kind?: "Solution" | "Project" | "Document" | "Projects";
  project?: ProjectSelector;
  document?: DocumentSelector;
  projects?: ProjectSelector[];
}
```

Runtime validation continues to require only the member appropriate to `kind` and to reject members belonging to other cases. The existing `DefaultValue(ScopeKind.Solution)` metadata remains useful construction guidance.

### Reject unresolved local references

Add a narrow final input-schema integrity check that traverses the completed schema and resolves each same-document `$ref` (`#` or `#/...`) as an RFC 6901 JSON Pointer from the published root. Throw an actionable configuration/startup exception when a local target does not exist. Preserve non-local references without attempting to canonicalise them.

This is a guard against silent agent-facing `unknown`, not a general schema-rewriting engine. It neither dereferences nor rewrites valid references.

### Revised tests

- Update focused transformer tests so `ScopeSelector` is asserted as one complete object with no `oneOf`, conditional fragments or moved properties.
- Retain coverage for serialized names, defaults, nullability, runtime-only cross-member attributes, invalid member names and invalid expected values.
- Add provider integration coverage proving extracted request references are rebased to the published root while `$defs` references remain unchanged.
- Add representative integration coverage for `FindReferencesRequest`, `GetControlFlowGraphRequest` and `AnalyzeNullabilityRequest` that asserts every local reference resolves.
- Add catalogue-wide non-acceptance coverage that validates every bundled published input schema contains no unresolved local reference.
- Retain 100% line and branch coverage for the simplified transformer and the new integrity checker.
- Do not modify acceptance-test assets. Publish the staged candidate and use the real Codex declaration projection as the final acceptance boundary.

### Expected dogfood result

After publishing and restarting, the only expected literal `unknown` is the intentionally open argument map for parameterless `workspace-list`. No declaration should contain `unknown & unknown`, and all seven focused contracts must expose their material members in simple and previously failing tools.

### Approved follow-up and final outcome

Two approved follow-up changes supersede the corresponding expectations above without changing the third candidate's production design:

- Close an otherwise-unconstrained empty request object with `additionalProperties: false`. This makes `workspace-list` project as `args: {}` rather than an open map and produced a final 56-tool dogfood catalogue with no literal `unknown` or `unknown & unknown` declarations. A real `workspace-list({})` call succeeded against the published instance.
- Update the existing published-Host acceptance test after final review identified that it still required the superseded composed schema shapes. The corrected test asserts complete construction-guidance objects, nested reference resolution, retained default and UUID metadata, and absence of the removed composition. The complete platform acceptance wrapper passed all 64 tests.

The earlier instruction not to modify acceptance-test assets and the expectation that `workspace-list` would retain the sole `unknown` describe the pre-follow-up validation plan and are no longer the final acceptance requirements.

### Required approval and review gates

The user approved this proposal before production changes began. After implementation and non-acceptance validation, present the unstaged comparison and evidence for manual confirmation. Only then stage the new baseline and invoke a fresh context-free Review Agent. After final confirmation, publish the exact staged pre-commit candidate for the Codex projection check.

## Superseded second-candidate design

The remaining sections record the previously approved design and are retained until the third-candidate proposal is accepted and incorporated. They are not authority for further implementation after the second published failure.

### Runtime validation remains authoritative

The recursive request-validation pipeline continues to enforce every contract rule. JSON Schema is construction guidance for an agent, not a replacement for runtime validation.

The following remain runtime-only:

- rejection of `Guid.Empty`;
- whitespace-only strings being treated as absent;
- empty collections and dictionaries being treated as absent;
- Workspace, filesystem-path and symbol semantic invariants;
- plugin-specific validation that cannot be represented cleanly as a complete schema shape.

The schema should publish types, nullability, ordinary required members, UUID format, defaults, ranges, collection limits and discriminator shapes that project cleanly. At-least-one and exactly-one combinations remain runtime-only when publishing them would make nested member types opaque.

### Keep one contract definition

Selectors remain defined in `Roslyn.Workbench.Mcp.Abstractions`. Their existing validation attributes remain the single schema-neutral source of cross-member rules. Do not create hard-coded Host-side JSON definitions for `ProjectSelector`, `DocumentSelector` or any other selector.

For example, `ProjectSelector` remains a C# contract decorated with `RequiresAtLeastOneAttribute`. `InputContractSchemaTransformer` validates that the attribute still references published contract members, while recursive runtime validation enforces the combination.

### Use the SDK-generated `$defs`

Microsoft.Extensions.AI generates reusable nested contract definitions inside each tool's `inputSchema.$defs`. Properties refer to those definitions with local `$ref` values such as `#/$defs/ProjectSelector`. The exact definition key is SDK-controlled and must be resolved through `$ref`; implementation and tests must not assume that the key is literally the CLR type name.

Occurrences of a selector within one tool schema share the same `$defs` entry. Definitions are intentionally repeated across tools because MCP `tools/list` does not provide an OpenAPI-style global component registry shared by every tool input schema.

The transformer reshapes the SDK-generated object node before it is emitted into `$defs`; it does not maintain a parallel schema registry.

### Prefer projection-compatible complete objects

`RequiresAtLeastOneAttribute` and `RequiresExactlyOneAttribute` must not create `anyOf` or `oneOf` alternatives. Preserve the SDK-generated single object with every material property visible and let runtime validation enforce the cross-member rule. This is deliberately construction guidance rather than a complete validation schema.

Paired conditional attributes may reshape the complete owning object into a discriminator union because the discriminator materially guides construction. Every discriminator branch must remain a complete object and use the simplified nested selector objects. Do not append Boolean conditional fragments to a separate base object.

Simple constraints that do not change the object shape, such as defaults and non-nullable property metadata, continue to apply directly to the SDK-generated property schema.

### Avoid a general-purpose schema-rewriting engine

The implementation must not attempt to normalise arbitrary JSON Schema. In particular, do not add machinery to flatten arbitrary composition, rewrite arbitrary polymorphism, reconcile keyword collisions, normalise every possible nullable representation or transform SDK-owned output/value schemas.

If a plugin's conditional rule cannot be translated safely into the supported discriminator shape, leave the published schema permissive and retain authoritative runtime validation. Do not reject the plugin solely because runtime validation is stricter than its published construction guidance.

## Supported schema shapes

### `RequiresAtLeastOneAttribute`

Publish the SDK-generated single object and retain every configured member as an optional property. Validate that every attribute member resolves to the JSON contract, but do not publish the at-least-one combination.

Conceptually, `ProjectSelector` becomes:

```text
{ projectId?: string; name?: string; path?: string; targetFramework?: string }
```

The same generic transformation applies to `WorkspaceSelector` and plugin contracts using the public attribute.

### `RequiresExactlyOneAttribute`

Publish the SDK-generated single object and retain every configured member as an optional property. Validate that every attribute member resolves to the JSON contract, but do not publish the exactly-one combination.

Conceptually, `DocumentSelector` becomes:

```text
{ path?: string; documentId?: string; project?: ProjectSelector }
```

The same transformation applies to `LocationSelector`, `SymbolSelector` and plugin contracts using the public attribute.

### Paired `RequiredWhenAttribute` and `ProhibitedUnlessAttribute`

When every conditional property forms a complete pair against the same enum discriminator, publish the owning object as a complete discriminated union. Do not publish separate `if`/`then` and negated prohibition fragments.

`ScopeSelector` becomes conceptually:

```text
{ kind?: "Solution" }
|
{ kind: "Project"; project: ProjectSelector }
|
{ kind: "Document"; document: DocumentSelector }
|
{ kind: "Projects"; projects: ProjectSelector[] }
```

Every branch omits properties belonging to the other discriminator cases and is closed to undeclared properties. The default enum case may omit `kind` only when normal deserialisation would produce that case; non-default cases require it explicitly.

If conditional attributes are incomplete, use different controlling properties, use incompatible expected values or otherwise cannot form a complete enum-discriminated union, do not publish fragmentary conditional constraints. Runtime validation remains authoritative.

### `NonEmptyGuidAttribute`

Publish the normal string/UUID schema supplied by the SDK, but do not publish a special `not: { const: "00000000-..." }` constraint. `Guid.Empty` remains invalid and is rejected recursively by the runtime validation pipeline. This avoids a complex schema fragment for a value an agent is very unlikely to construct accidentally.

## Transformation boundaries

The input transformer must:

- run only through `McpSdkSchemaProvider`'s input-schema creation options;
- leave output and value schemas untouched;
- preserve SDK-generated descriptions, ordinary property constraints, defaults, references and nested definitions;
- use serialized JSON property names, including `JsonPropertyNameAttribute` and configured naming policy;
- validate attribute member names sufficiently to avoid publishing a silently incorrect owned contract;
- avoid assuming that referenced definitions use CLR type names;
- avoid special-casing individual selector CLR types when the attribute expresses the required shape;
- leave unsupported plugin composition permissive rather than constructing misleading fragments.

## Focused owned-contract scope

The first implementation and dogfood candidate must verify these owned contracts:

- `WorkspaceSelector`;
- `ProjectSelector`;
- `DocumentSelector`;
- `LocationSelector`;
- `SymbolSelector`;
- `ScopeSelector`;
- `SnapshotPrecondition`.

This is not a promise that no published tool declaration will contain `unknown`. Parameterless requests and intentionally open dictionary-shaped contracts may still project that way. The acceptance target is that these seven contracts and the validation attributes applied to them do not introduce opaque projections.

## Test design

### Unit and contract tests

Update `InputContractSchemaTransformerTests` to exercise the transformed SDK-generated schema rather than private implementation details. Coverage must include:

- single complete at-least-one and exactly-one object shapes with every serialized property name visible;
- absence of `anyOf`, `oneOf` and fragmentary conditional composition for those two attribute families;
- nested, collection and dictionary occurrences resolving through `$defs`;
- the four complete `ScopeSelector` discriminator alternatives;
- default discriminator omission only for the deserialised default case;
- ordinary defaults and nullable-reference handling remaining intact;
- `NonEmptyGuidAttribute` no longer producing a special Boolean fragment;
- incomplete or unsupported conditional plugin metadata remaining schema-permissive while runtime validation still rejects invalid values;
- input transformations not being applied to output/value schemas;
- unknown referenced validation members producing an actionable startup/configuration failure where the contract could not otherwise be represented correctly.

The transformed production implementation must achieve the repository-required 100% line and branch coverage unless the user explicitly approves a documented exception.

### Integration tests

Retain or update Host integration coverage around `ToolSchemaFactory`/`McpSdkSchemaProvider` to confirm that:

- the transformer is used for input schemas;
- generated `$defs` and `$ref` relationships remain valid;
- output/value schemas do not receive input-contract transformations;
- representative nested selector definitions survive the complete Host schema-publication path as direct object properties.

### Published dogfood validation

Do not add or modify acceptance-test assets for this revision. Unit and integration coverage lock the intended raw schema, while the configured dogfood instance validates Codex's proprietary callable projection that managed projection proxies cannot reproduce.

For each candidate:

1. Publish the exact staged pre-commit candidate to an isolated directory.
2. Smoke-test MCP `initialize` and `tools/list`.
3. Promote the candidate to the configured dogfood `current` target and restart the Codex MCP connection.
4. Inspect all 56 callable declarations for literal `unknown` and `unknown & unknown` projections.
5. Confirm the seven focused contracts expose their material members in representative simple and composition-heavy tools.
6. Restore the previous stable dogfood target when the candidate fails validation.

## Agent-facing acceptance criteria

After publishing and restarting Codex against the candidate build:

- `WorkspaceSelector`, `ProjectSelector`, `DocumentSelector`, `LocationSelector`, `SymbolSelector`, `ScopeSelector` and `SnapshotPrecondition` expose their material members;
- validation attributes owned by Roslyn Workbench do not introduce `unknown & unknown` intersections;
- representative tools expose complete nested selector object properties without material `unknown` placeholders;
- `ScopeSelector` appears as useful kind-specific alternatives rather than a base object followed by unknown conditional intersections;
- `SnapshotPrecondition` exposes `workspaceId`, `workspaceEpoch`, `snapshotId` and `transactionRevision`;
- runtime validation continues to reject empty GUIDs, blank selector strings, empty selector collections and semantically invalid requests;
- output and value schemas remain unchanged.

## Explicit non-goals

- Do not redesign selector C# contracts.
- Do not introduce an OpenAPI-style global schema registry.
- Do not hard-code JSON schemas for individual selectors.
- Do not weaken or replace recursive runtime validation.
- Do not encode `Guid.Empty` exclusion in JSON Schema.
- Do not add Node as a Windows acceptance-test prerequisite.
- Do not build a general JSON Schema canonicaliser.
- Do not attempt to eliminate every `unknown` from every published tool declaration.
- Do not invoke the Review Agent before the user has inspected and confirmed the implementation.

## Implementation and review process

Follow the repository remediation process:

1. Treat this document as the approved design; request clarification before departing from it.
2. Implement the production and test changes.
3. Run scoped formatting, latest-all analyzers, affected unit/contract tests and integration tests. Do not run the acceptance suite because no acceptance asset changes in this revision.
4. Present the implementation and validation evidence for the user's first confirmation.
5. Only after that confirmation, stage all current implementation changes so subsequent review corrections remain visible as an unstaged diff.
6. Spawn a fresh context-free Review Agent subagent and supply reusable validation evidence as required by `AGENTS.md`.
7. Correct validated findings, rerun only stale or materially affected validation and use a second fresh Review Agent after material review-driven changes.
8. Obtain the user's final confirmation.
9. Update the DOGFOOD-002 worklist status and usage evidence.
10. Publish and inspect a staged pre-commit dogfood candidate because Codex projection is the acceptance boundary, then let the user commit only after that validation succeeds.

Every dogfood MCP request made during the work must continue to be recorded in `docs/development/repo-review/dogfood-improvement-usage.md` until the user explicitly ends that requirement.

## Suggested opening message for a fresh task

> Continue DOGFOOD-002 using `docs/development/repo-review/dogfood-002-schema-publication-design.md` as the approved revised design and handoff. Preserve the staged first-confirmed baseline while keeping the projection-compatibility correction unstaged until the user compares it. Do not restore at-least-one or exactly-one schema alternatives, and continue logging every dogfood MCP request.
