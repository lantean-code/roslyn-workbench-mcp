# Code Action provenance

## Status

Approved for implementation as roadmap item 7.

## Purpose

Code Action provenance identifies the runtime component that supplied a transformation so an operator can correlate prepared and staged work with the installed Roslyn build. It is primarily audit and diagnostic information. It is not an additional approval decision, a trust assertion, a package attribution mechanism or a substitute for inspecting the proposed source change.

The connecting agent already receives the title, action kind, source location, diagnostic context and supported Fix All scopes needed to select an action. Routine discovery must not repeat lower-level runtime metadata that does not help that selection.

## Provenance model

The Host derives provider identity from the actual loaded provider type. The concise identity contains only:

- the fully qualified provider type name;
- the assembly simple name; and
- the assembly version.

The internal audit record additionally retains the Code Action family, diagnostic identifiers, equivalence key and, for Fix All, the actual Fix All provider and selected scope. These fields support structured logging and replay correlation but are not all useful to an agent.

Assembly paths, culture, public-key tokens and duplicate full display names are excluded from the agent-facing projection. Package identity is also excluded because the current composition boundary cannot authoritatively map a loaded assembly to a NuGet package. An assembly file name or path is not sufficient evidence of package ownership.

## MCP projection

`list-code-actions` gains an `includeProvenance` Boolean that defaults to `false`. Ordinary discovery therefore retains its existing compact response. When explicitly requested, each returned action contains a response-local `providerId`, and the response contains a `providers` JSON object keyed by those identifiers. Each distinct identity is emitted once. The existing action kind and diagnostic context remain authoritative and are not duplicated inside the provider projection.

Provider identifiers are assigned when each distinct identity is first encountered in the already deterministic response order. Projection builds the provider dictionary and response entries together in one pass; it does not collect or sort provider identities separately. Identifiers are scoped to one response, are not persisted and are not security identities. Clients resolve the JSON object by key and do not depend on discovery or serialization order, and must not compare identifiers across responses.

Successful `prepare-fix-all` responses do not repeat provenance. The originating action already supplied it when requested, and the prepared reference retains the information internally.

`transaction-preview` and `transaction-review` use the same compact dictionary shape for each active revision created by `stage-code-action`: Code Action entries contain `providerId` and, for Fix All, `fixAllProviderId`, while a response-level `providers` object contains each distinct identity once. This is the point at which runtime attribution is relevant to reviewing a concrete staged transformation. Plugin and other mutation revisions omit the Code Action field, and a transaction response without Code Action provenance omits the provider dictionary.

Undo removes provenance for revisions no longer active; redo restores it with the corresponding immutable revision. Provenance does not change the canonical persistence-set digest because that digest intentionally identifies paths, operations and bytes. Receipt identity continues to bind the transaction revision and snapshot independently.

## Structured logging

The Host writes one structured information event after a Fix All operation has been prepared and one after a Code Action candidate has been staged successfully. Discovery alone produces no provenance event.

Logging records provider type, assembly name and version, action family, diagnostic identifiers and equivalence key. Fix All logging also records the Fix All provider and requested scope. Action titles are not added to provenance logging because provider-generated titles can contain source-derived symbol names. No log event is emitted for a candidate that fails Workspace staging.

Provider metadata is captured when the fixed provider catalogue is constructed and reused by discovery and staging. Fix All provider metadata is captured when that provider is selected. The implementation does not scan package directories, hash binaries or perform network access.

## Approval and policy

Provenance does not trigger elicitation. Transactional confirmation and exact-change approval already ask the user about the persistence decision. A provider rejected by Host policy fails deterministically; the agent cannot obtain an interactive exception to immutable startup policy.

The current production Host composes the pinned Roslyn assemblies shipped with the application. The compatibility audit locks the complete built-in provider inventory, and runtime policy excludes providers that require unsupported editor, project, package or external-intelligence capabilities. There is no production configuration for loading arbitrary Code Action assemblies.

Provider allow-listing is therefore not part of this item. Namespace allow-listing must not be used because a namespace is neither an assembly identity nor a security boundary. If external Code Action assemblies are supported later, admission must occur before MEF loading or provider activation, be explicitly enabled at startup, deny external providers by default and match an exact provider type together with an authoritative assembly or binary identity. No runtime elicitation may weaken that policy. Such admission remains an operational guardrail rather than a sandbox because accepted extensions execute in-process with the user's authority.

## Validation

Unit and contract coverage must verify provider identity construction, catalogue reuse, default omission and requested publication, Fix All audit metadata, structured logging, staging retention, undo and redo projection, preview and review schemas, and omission for non-Code-Action revisions. New implementation files require full line and branch coverage unless an existing repository exception applies.

Integration coverage must use controlled Code Fix, refactoring and Fix All providers to prove that discovery, preparation, staging, preview and exact-change review retain the correct identity. Published-Host acceptance must verify the opt-in discovery projection and staged review projection, and the complete acceptance wrapper must run because checked-in schema baselines change.

The Code Action compatibility audit must remain green. Cold and warm `document-code-fixes` scenarios across GuardClauses, Serilog and EF Core, together with the existing Fix All preparation and staging scenarios, will verify that cached identity projection adds bounded response data without material per-action computation.
