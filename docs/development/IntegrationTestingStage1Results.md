# Integration Testing Stage 1 Results

Date: 17 July 2026

## Outcome

Stage 1 is complete. Generated source-workspace fixtures have been replaced by readable checked-in assets materialised into unique temporary scenario roots.

## Assets

- `Workspaces/SdkProject` owns the common .NET 10 SDK project used by Workspace, transaction and Host scenarios.
- `Workspaces/CompatibilitySamples` owns the legacy .NET Framework project, intentionally malformed SDK project and ambiguous two-project graph.
- `Workspaces/SolutionHierarchy` owns the two-project `.slnx` graph used by cross-project search and solution-folder projection.
- `Workspaces/InspectionSample/Base` owns the 24 invariant inspection documents and their project/configuration files.
- Eight explicit inspection profiles own nullable, editor-config, console document-set and analyzer-enabled differences without duplicating the invariant sources.

No `.sln` asset was needed. The final generator scan found and migrated the active `.slnx` hierarchy fixture. No token substitution was added because none of the catalogued shapes contains a genuinely variable value.

## Materialisation

The shared materialiser:

- copies exact file bytes into a unique `workspace` root;
- creates a separate `state` root under the same asynchronously owned scenario directory;
- excludes `bin`, `obj`, `.vs` and recovery directories at every depth;
- overlays explicit inspection profiles and applies checked-in `.asset-delete` manifests;
- creates the runtime-only `.git` directory after copying; and
- removes the complete scenario root during asynchronous disposal.

The asset README records immutability, project-scope, authoring and template-boundary rules.

## Migration

- All `TestWorkspaceFixture` factories and consumers now use checked-in assets.
- All `InspectionSampleFixture` consumers now use the base asset and one of the eight named `InspectionSampleProfile` values; the generation-era options record was removed.
- `SolutionHierarchyFixture` and its cross-project consumers now use the checked-in `.slnx` asset.
- `InspectionSampleFixture` was reduced from 1,183 lines of embedded project/source generation to an asset-backed selector helper.
- The superseded generated workspace strings and write logic were removed only after the affected consumer projects passed.
- Mutable, external-change, recovery and locking scenarios continue to receive isolated materialised copies. Read-only sharing was not introduced because it was unnecessary for this stage.

## Verification

- focused materialiser coverage verifies exact binary copying, generated/recovery-directory exclusions, profile overlay, profile deletion, separate workspace/state roots and asynchronous cleanup;
- Workspace integration: 65 passed;
- Code Actions integration: 11 passed;
- Plugins Core integration: 21 passed;
- Host integration: 23 passed;
- Code Action audit, including every inspection profile: 95 passed;
- complete repository suite: 1,970 passed.

Repeated test execution left the checked-in assets unchanged. The final asset tree contains no build output, recovery state or machine-specific paths.
