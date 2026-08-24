# Post-RWMCP3 dogfood usage

**Status:** Active until the user explicitly ends dogfood usage logging.

This log records every request sent to the configured published Roslyn Workbench dogfood server after the RWMCP3 usage analysis. It covers implementation of the [approved dogfood improvement worklist](dogfood-analysis.md) and all other repository operations. Failed requests, retries, blank client projections and abandoned approaches remain part of the evidence.

The completed historical calls that informed the worklist remain in the separate [RWMCP3 dogfood usage log](dogfood-usage.md).

## Recording format

Create a section for each work item or distinct repository activity and number its calls in execution order. Record each call as:

### 1. `tool-name`

**Purpose:** Why the dogfood request was made.

**Request:** The material request shape, with incidental machine-specific paths redacted.

**Outcome:** Whether it succeeded or failed, the useful result or error code, any continuation followed and whether the client exposed the response content.

## Usage

## DOGFOOD-001 — Structured-result text fallback

### 1. `workspace-list`

**Purpose:** Confirm the published dogfood Workspace state before inspecting the shared MCP result-construction path.

**Request:** `{}`

**Outcome:** Succeeded and reported the existing `rwmcp3` Workspace at epoch 1 with no transaction owner. The client-visible response again contained empty `content` alongside populated `structuredContent`, directly reproducing DOGFOOD-001.

### 2. `search-symbols`

**Purpose:** Locate `McpServerToolBase` and its tests through the published dogfood Workspace.

**Request:** `{"workspace":{"alias":"rwmcp3"},"query":"McpServerToolBase","kinds":["NamedType"],"symbolsLimit":20}`

**Outcome:** Failed with `WorkspaceOutOfDate`. The continuation instructed the caller to invoke `workspace-reload` before retrying.

### 3. `workspace-reload`

**Purpose:** Follow the authoritative continuation from the failed symbol query.

**Request:** `{"workspace":{"alias":"rwmcp3"}}`

**Outcome:** Succeeded at Workspace epoch 2 with 30 projects and 1,589 documents. The existing unresolved generated-analyser warning remained. The client-visible response again contained empty `content` alongside populated `structuredContent`.

### 4. `search-symbols`

**Purpose:** Retry discovery of the shared result-construction path after reloading the Workspace.

**Request:** `{"workspace":{"alias":"rwmcp3"},"query":"McpServerToolBase","kinds":["NamedType"],"symbolsLimit":20}`

**Outcome:** Succeeded with the production `McpServerToolBase<TRequest>` and `McpServerToolBaseTests` types. The client-visible response again contained empty `content` alongside populated `structuredContent`.
