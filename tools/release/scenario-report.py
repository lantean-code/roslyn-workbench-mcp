#!/usr/bin/env python3
"""Summarise retained scenario evidence without running or changing any workspace."""

from __future__ import annotations

import argparse
import hashlib
import json
import math
import re
import statistics
from datetime import datetime
from pathlib import Path


METRICS = (
    "elapsedMilliseconds", "hostCpuMilliseconds", "workingSetBytes",
    "workingSetDeltaBytes", "peakWorkingSetBytes", "responseBytes",
)
ENVIRONMENT = ("frameworkDescription", "operatingSystem", "processArchitecture", "processorCount")
IDENTITY = ("hostCommit", "hostVersion", "suiteSha256", "runnerCommit", "command", "machineLabel")


def timestamp(value: str | None) -> datetime | None:
    if not value:
        return None
    parsed = datetime.fromisoformat(value)
    return parsed if parsed.tzinfo else None


def read(path: Path):
    return json.loads(path.read_text(encoding="utf-8-sig"))


def digest(path: Path) -> str:
    return hashlib.sha256(path.read_bytes()).hexdigest()


def statistics_for(samples: list[dict]) -> dict:
    metrics = {}
    for name in METRICS:
        values = [sample[name] for sample in samples if name in sample]
        if not values:
            continue
        if len(values) != len(samples) or any(not isinstance(value, (int, float)) or isinstance(value, bool) or not math.isfinite(value) for value in values):
            raise ValueError(f"Incomplete or non-numeric observations for {name}.")
        ordered = sorted(values)
        metrics[name] = {
            "samples": len(values), "minimum": min(values), "maximum": max(values),
            "mean": statistics.mean(values), "median": statistics.median(values),
            "p95": ordered[math.ceil(len(values) * 0.95) - 1],
        }
    return metrics


def collect(root: Path) -> list[dict]:
    runs = []
    # Validation files identify completed attempts, including unsuccessful ones.
    # An interrupted attempt without validation is retained separately below.
    result_names = {"validation.json", "measurements.json", "profile.json", "concurrency.json", "commit.json", "cancellation.json", "commit-cancellation.json", "conflict.json", "crash-recovery.json", "state-sequence.json"}
    directories = {path.parent for path in root.rglob("*.json") if path.name in result_names}
    for directory in sorted(directories):
        validation_path = directory / "validation.json"
        validation = read(validation_path) if validation_path.exists() else {}
        validations = validation if isinstance(validation, list) else [validation]
        terminal_success = bool(validations) and all(item.get("succeeded") is True for item in validations)
        identity_path = directory / "run-identity.json"
        identity = read(identity_path) if identity_path.exists() else {}
        missing = [key for key in IDENTITY if not isinstance(identity.get(key), str) or not identity[key].strip()]
        for name, length in (("hostCommit", 40), ("runnerCommit", 40), ("suiteSha256", 64)):
            if name not in missing and not re.fullmatch(f"[0-9a-fA-F]{{{length}}}", identity[name]):
                missing.append(name)
        evidence = {
            path.name: digest(path) for path in sorted(directory.glob("*.json"))
        }
        measurement_path = directory / "measurements.json"
        observations = read(measurement_path) if measurement_path.exists() else []
        if not observations:
            runs.append({
                "run": directory.name, "kind": "validation-only",
                "validated": terminal_success,
                "comparisonUnavailable": "No common measurement series; retain the family-specific results for diagnosis.",
                "evidence": evidence,
            })
        for observation in observations:
            samples = observation["measurements"]
            environment = {key: observation.get("environment", {}).get(key) for key in ENVIRONMENT}
            absent = missing + [key for key, value in environment.items() if value is None]
            for key in ("repository", "commit", "scenario", "tool", "warmupCount"):
                if observation.get(key) is None:
                    absent.append(key)
            if timestamp(observation.get("startedAtUtc")) is None:
                absent.append("startedAtUtc")
            failures = sum(sample.get("isError") is not False for sample in samples)
            validated = terminal_success and failures == 0 and bool(samples)
            reason = None
            if absent:
                reason = "Missing or invalid provenance: " + ", ".join(absent)
            elif not validated:
                reason = "Unsuccessful or incomplete measurement/terminal validation."
            runs.append({
                "run": directory.name, "kind": "measurement",
                "repository": observation.get("repository"),
                "targetCommit": observation.get("commit"),
                "scenario": observation.get("scenario"), "tool": observation.get("tool"),
                "startedAtUtc": observation.get("startedAtUtc"),
                "identity": {key: identity.get(key) for key in IDENTITY},
                "environment": environment, "warmupCount": observation.get("warmupCount"),
                "sampleCount": len(samples), "failedSamples": failures,
                "validated": validated, "comparisonUnavailable": reason,
                "metrics": statistics_for(samples), "evidence": evidence,
            })
    if not runs:
        raise ValueError("No scenario measurement or terminal validation files found.")
    return runs


def comparison_key(run: dict) -> str | None:
    if run.get("kind") != "measurement" or not run.get("validated") or run.get("comparisonUnavailable"):
        return None
    identity = run["identity"]
    # Host commit/version are the variable being compared, not comparability keys.
    key = {name: run[name] for name in ("repository", "targetCommit", "scenario", "tool", "environment", "warmupCount", "sampleCount")}
    key["execution"] = {name: identity[name] for name in ("suiteSha256", "runnerCommit", "command", "machineLabel")}
    return json.dumps(key, sort_keys=True)


def comparisons(current: dict, previous: dict | None) -> list[dict]:
    if previous is None or previous.get("schemaVersion") != current["schemaVersion"] or previous.get("aggregationPolicy") != current["aggregationPolicy"]:
        return []
    prior = {}
    for run in previous["runs"]:
        key = comparison_key(run)
        if key:
            prior.setdefault(key, []).append(run)
    results = []
    for run in current["runs"]:
        matches = prior.get(comparison_key(run), [])
        if not matches:
            continue
        # Multiple repetitions are evidence, not an invitation to select the best
        # result. Compare with the most recent preceding comparable observation.
        eligible = [old for old in matches if timestamp(old["startedAtUtc"]) < timestamp(run["startedAtUtc"])]
        if not eligible:
            continue
        old = max(eligible, key=lambda item: timestamp(item["startedAtUtc"]))
        changes = {}
        for name, values in run["metrics"].items():
            if name in old["metrics"]:
                changes[name] = values["median"] - old["metrics"][name]["median"]
        results.append({"run": run["run"], "previousRun": old["run"], "hostCommit": run["identity"]["hostCommit"], "previousHostCommit": old["identity"]["hostCommit"], "medianChanges": changes})
    return results


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--results-root", type=Path, required=True)
    parser.add_argument("--output", type=Path, required=True)
    parser.add_argument("--baseline", type=Path)
    args = parser.parse_args()
    runs = collect(args.results_root)
    report = {"schemaVersion": 1, "aggregationPolicy": "common-measurements-nearest-rank-p95-v1", "runs": runs}
    previous = read(args.baseline) if args.baseline else None
    report["baselineStatus"] = "No baseline supplied"
    if previous:
        report["baselineStatus"] = "Comparable observations only"
        if previous.get("schemaVersion") != report["schemaVersion"] or previous.get("aggregationPolicy") != report["aggregationPolicy"]:
            report["baselineStatus"] = "Incompatible baseline schema or aggregation policy"
    report["comparisons"] = comparisons(report, previous)
    args.output.mkdir(parents=True, exist_ok=True)
    (args.output / "scenario-summary.json").write_text(json.dumps(report, indent=2, allow_nan=False) + "\n", encoding="utf-8")
    measured = sum(run["kind"] == "measurement" for run in runs)
    comparable = sum(comparison_key(run) is not None for run in runs)
    summary = (
        "# Manual scenario evidence\n\n"
        f"Retained observations: {len(runs)}; common measurement series: {measured}; complete comparable identities: {comparable}; comparisons: {len(report['comparisons'])}.\n\n"
        f"Baseline: {report['baselineStatus']}.\n\n"
        "Missing provenance, unsuccessful attempts and family-specific validation remain explicit in scenario-summary.json. No missing baseline is treated as zero. Medians and nearest-rank P95 values summarise recorded samples only; timing changes are advisory, not release gates.\n\n"
        "Keep the original run directories with this aggregate: evidence hashes identify their JSON files without copying logs, source paths or diagnostic payloads into the aggregate. Publish raw diagnostics only after checking their contents.\n"
    )
    (args.output / "scenarios.md").write_text(summary, encoding="utf-8")
    print(summary)


if __name__ == "__main__":
    main()
