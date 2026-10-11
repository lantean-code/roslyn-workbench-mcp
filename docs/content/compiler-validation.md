# Compiler-impact validation

This optional gate prevents a transaction from committing new compiler errors relative to its captured baseline. It does not establish behavioural correctness or replace repository builds and tests.

## Enable the gate

An operator can independently enable `--commit-validation no-new-compiler-errors` for any mutation-enabled operational mode. When enabled, call `transaction-validate` with the current snapshot after staging to see whether the transaction introduced compiler errors before attempting review or commit. Review and both commit workflows enforce the same gate automatically; commit performs an authoritative final validation before its filesystem work.

## What is compared

Validation compares the transaction-start solution with the current staged revision for changed loaded project evaluations and their loaded transitive dependants.

A transaction at revision zero is a complete empty comparison and preserves the normal no-change commit result. Existing compiler errors are permitted. A moved error is not considered new, while an additional occurrence of the same error is.

The result reports evaluated projects, error counts, elapsed duration, introduced diagnostics, structured incomplete reasons and limitations. It covers only the target-framework evaluations loaded by the Workspace and does not run third-party analyser validation or tests.

## Cost and timing

The baseline is the solution captured when the transaction starts, not a baseline created on every Workspace load. Compiler comparison is calculated on explicit validation, review or commit, not on every staged mutation. Cost follows the changed loaded project evaluations and their loaded transitive dependants: a leaf change can be much cheaper than a change to a widely referenced project.

## Recover from a failed validation

If new errors are reported, correct or restage the transaction and validate the new snapshot.

Incomplete validation cannot be recovered by restaging the same transaction because its result is bound to the captured baseline and loaded project evaluations. Follow the rollback continuation, resolve the reported load, compilation or external-input condition, reload the Workspace and start a new transaction.

A failed validation writes no files, creates no review receipt and leaves the transaction active until it is rolled back.

| Result | Next step |
| --- | --- |
| New compiler errors | Correct or restage the change, then validate the new snapshot. |
| Incomplete validation | Roll back, resolve the reported condition, reload and start a new transaction. |
| Successful validation followed by another stage | Validate the new revision; do not reuse an earlier result as approval. |

Continue with the [transaction workflow](workspaces-and-transactions.md).
