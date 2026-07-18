# Test Project Restructure Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Rework the test suite so unit, contract, integration, and audit coverage are explicitly separated as far as the current production-facing internal visibility rules allow.

**Architecture:** Keep the current `*.Test` assemblies for the unit and contract suites, create new integration-focused test projects, add targeted `InternalsVisibleTo` entries where the split requires internal access, isolate host plugin-discovery fixtures into dedicated one-plugin assemblies, and use xUnit `Trait("Category", ...)` metadata to classify the remaining mixed or blocked areas.

**Tech Stack:** .NET 10 SDK, xUnit v3, Moq, AwesomeAssertions, `dotnet test`, `dotnet format`

---

### Task 1: Lock The Taxonomy

**Files:**
- Modify: `test/AGENTS.md`
- Modify: `docs/test-project-audit-2026-07-07.md`

- [x] Define the enforced categories in `test/AGENTS.md`
- [x] Add execution guidance for default local runs, integration gates, and audit gates
- [x] Update the audit document with an implementation-status note so the design and the code changes stay aligned

### Task 2: Decouple Shared Test Fixtures

**Files:**
- Create: `test/Roslyn.Workbench.Mcp.TestSupport/HostValidQueryPlugin.cs`
- Create: `test/Roslyn.Workbench.Mcp.TestSupport/HostValidMutationPlugin.cs`
- Create: `test/Roslyn.Workbench.Mcp.TestSupport/TestWorkspaceFixture.cs`
- Modify: `test/Roslyn.Workbench.Mcp.TestSupport/GlobalUsings.cs`
- Delete or stop compiling: `test/Roslyn.Workbench.Mcp.Workspace.Test/HostValidQueryPlugin.cs`
- Delete or stop compiling: `test/Roslyn.Workbench.Mcp.Plugins.Core.Test/HostValidMutationPlugin.cs`
- Delete or stop compiling: `test/Roslyn.Workbench.Mcp.Workspace.Test/TestWorkspaceFixture.cs`

- [x] Move the reusable temporary-workspace fixture into `TestSupport`
- [x] Update namespaces/usings so host and integration projects can depend on `TestSupport` instead of other test projects
- [x] Isolate the host plugin-discovery fixtures into dedicated one-plugin fixture assemblies so the loader sees exactly one `IRoslynPlugin` per assembly

### Task 3: Split The Host Test Project

**Files:**
- Create: `test/Roslyn.Workbench.Mcp.IntegrationTest/Roslyn.Workbench.Mcp.IntegrationTest.csproj`
- Create: `test/Roslyn.Workbench.Mcp.IntegrationTest/GlobalUsings.cs`
- Create: `test/Roslyn.Workbench.Mcp.IntegrationTest/TestAssemblyInitialization.cs`
- Move: `test/Roslyn.Workbench.Mcp.Test/CodeActionMcpToolTests.cs`
- Move: `test/Roslyn.Workbench.Mcp.Test/InspectionMcpToolTests.cs`
- Move: `test/Roslyn.Workbench.Mcp.Test/PluginDiscoveryAndMcpToolTests.cs`
- Move: `test/Roslyn.Workbench.Mcp.Test/WorkspaceLifecycleToolTests.cs`
- Move: `test/Roslyn.Workbench.Mcp.Test/WorkspaceStatusToolTests.cs`
- Modify: `test/Roslyn.Workbench.Mcp.Test/Roslyn.Workbench.Mcp.Test.csproj`
- Modify: `Roslyn.Workbench.Mcp.slnx`

- [x] Create the host integration project
- [x] Move integration/component tests into it
- [x] Add `Trait("Category", "Integration")` at class level in the moved files
- [x] Remove the host unit project references to other test projects
- [x] Keep only the unit/contract-style host tests in `Roslyn.Workbench.Mcp.Test`

### Task 4: Split The Plugins-Core Test Project

**Files:**
- Create: `test/Roslyn.Workbench.Mcp.Plugins.Core.IntegrationTest/Roslyn.Workbench.Mcp.Plugins.Core.IntegrationTest.csproj`
- Create: `test/Roslyn.Workbench.Mcp.Plugins.Core.IntegrationTest/GlobalUsings.cs`
- Create: `test/Roslyn.Workbench.Mcp.Plugins.Core.IntegrationTest/TestAssemblyInitialization.cs`
- Move integration files from `test/Roslyn.Workbench.Mcp.Plugins.Core.Test`
- Modify mixed files that must keep unit tests in the unit project but need integration methods split out
- Modify: `test/Roslyn.Workbench.Mcp.Plugins.Core.Test/Roslyn.Workbench.Mcp.Plugins.Core.Test.csproj`
- Modify: `Roslyn.Workbench.Mcp.slnx`

- [x] Create the core integration project
- [x] Move real-workspace inspection, code-action, and refactoring flow tests into it
- [x] Split mixed files where one class currently combines handler-unit coverage and real-workspace coverage
- [x] Mark replay-ledger coverage as `Trait("Category", "Audit")`
- [x] Leave small handler/service tests in `Roslyn.Workbench.Mcp.Plugins.Core.Test`

### Task 5: Categorise The Workspace Project And Record The Blocker

**Files:**
- Modify: `test/Roslyn.Workbench.Mcp.Workspace.Test/*.cs`
- Modify: `docs/test-project-audit-2026-07-07.md`

- [x] Mark file-system/workspace lifecycle tests as `Trait("Category", "Integration")`
- [x] Mark reflection/surface-lock tests as `Trait("Category", "Contract")` where appropriate
- [x] Keep fast seam-focused tests unlabelled or categorised as `Unit`
- [x] Document that a full physical split is currently constrained by direct internal coverage and mixed-seam tests, which would require later production-side seam work

### Task 6: Add Additional Unit Coverage Without Production Changes

**Files:**
- Modify or create focused tests in `test/Roslyn.Workbench.Mcp.Test`
- Modify or create focused tests in `test/Roslyn.Workbench.Mcp.Plugins.Core.Test`
- Modify or create focused tests in `test/Roslyn.Workbench.Mcp.Workspace.Test`

- [x] Add at least a small set of true unit tests around existing public or supported internal seams
- [x] Prefer Moq-driven collaborator testing or in-memory Roslyn helpers already used elsewhere
- [x] Note any remaining high-value areas that still need production refactoring before they can be unit tested properly

### Task 7: Verify, Format, And Normalise

**Files:**
- Modify: all changed test/docs/solution files from this task

- [ ] Run `dotnet format --include <changed files> --artifacts-path=/tmp/artifacts/roslyn-workbench-mcp`
- [ ] Run `unix2dos` on changed CRLF-governed files
- [ ] Run targeted test projects, then the filtered fast suite, then at least one broader integration pass
- [x] Update the audit document with actual completion status and remaining refactor-required items
