# Maintaining compatibility

The [published compatibility policy](../content/compatibility.md) owns the user-facing 1.x guarantees. This guide records the implementation and validation mechanisms.

## Catalogue baselines

The repository records canonical `tools/list` baselines for both supported output-schema publication modes. Each baseline preserves the exact built-in catalogue published by inspection-only, transactional, approval-required, autonomous-trusted and compiler-validation configurations, including intentional mode-specific contracts such as receipt-based `transaction-commit`. Tools are ordered by name and JSON object properties are ordered recursively before comparison; array order is retained because arrays can carry contract meaning. The baselines cover each built-in tool's name, title, description, annotations, input schema and, when enabled, output schema. Externally installed plugin tools are deliberately excluded because their contracts belong to their publishers.

## Recovery format mappings

`Current` identifies the format written by a Host; a separate supported-format set identifies every format it can read. The Host probes the common version field first and dispatches supported evidence through immutable, version-specific persisted models and explicit mappings into the version-neutral runtime recovery model. Writing follows the reverse mapping into the current persisted model. Advancing `Current` therefore requires adding both mappings for the new format while retaining every earlier 1.x reader and keeping the V1 recovery fixtures green; a contract test fails when the current version and emitted version diverge.

## Plugin API evidence

The repository maintains shipped public-API analyser baselines for both assemblies and focused plugin contract tests. Once the first stable plugin package is published, release validation also compares later packages with that 1.0 package by using .NET package/API compatibility tooling. Passing that binary check complements, but does not replace, behavioural contract tests and review of semantic changes.
