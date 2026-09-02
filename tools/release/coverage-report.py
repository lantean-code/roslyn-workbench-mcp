#!/usr/bin/env python3
"""Merge native Coverlet results into a traceable, advisory coverage baseline."""

from __future__ import annotations

import argparse
import hashlib
import json
import os
import platform
import subprocess
import xml.etree.ElementTree as ET
from pathlib import Path


def source_name(value: str) -> str | None:
    # PDB paths may come from another OS or from a deterministic /_/ build root.
    parts = value.replace("\\", "/").split("/")
    if "src" not in parts:
        return None
    parts = parts[parts.index("src"):]
    if "obj" in parts or "bin" in parts:
        return None
    return "/".join(parts)


def collect_classes(reports: list[Path], production_assemblies: set[str]) -> list[dict]:
    classes = {}
    for report in reports:
        modules = json.loads(report.read_text(encoding="utf-8-sig"))
        for assembly, documents in modules.items():
            if assembly not in production_assemblies:
                continue
            for document, types in documents.items():
                filename = source_name(document)
                if filename is None:
                    continue
                for name, methods in types.items():
                    key = (assembly, filename, name)
                    lines, branches = classes.setdefault(key, ({}, {}))
                    for method, points in methods.items():
                        for line, hits in points["Lines"].items():
                            lines[line] = lines.get(line, False) or hits > 0
                        for branch in points["Branches"]:
                            branch_key = (
                                method, branch["Line"], branch["Offset"],
                                branch["EndOffset"], branch["Path"], branch["Ordinal"],
                            )
                            branches[branch_key] = branches.get(branch_key, False) or branch["Hits"] > 0

    results = []
    for (assembly, filename, name), (lines, branches) in sorted(classes.items()):
        results.append({
            "assembly": assembly,
            "file": filename,
            "class": name,
            "linesCovered": sum(lines.values()),
            "linesTotal": len(lines),
            "branchesCovered": sum(branches.values()),
            "branchesTotal": len(branches),
        })
    if not results:
        raise ValueError("No production src files were present in the coverage reports.")
    return results


def totals(classes: list[dict]) -> dict:
    return {
        key: sum(item[key] for item in classes)
        for key in ("linesCovered", "linesTotal", "branchesCovered", "branchesTotal")
    }


def percentage(values: dict, kind: str) -> float | None:
    total = values[kind + "Total"]
    return 100 * values[kind + "Covered"] / total if total else None


def display(values: dict, kind: str) -> str:
    value = percentage(values, kind)
    if value is None:
        return "n/a (0 points)"
    return f"{value:.2f}% ({values[kind + 'Covered']}/{values[kind + 'Total']})"


def delta(current: dict, previous: dict | None, kind: str) -> str:
    if previous is None:
        return "n/a"
    before = percentage(previous, kind)
    after = percentage(current, kind)
    if before is None or after is None:
        return "n/a"
    return f"{after - before:+.2f} pp"


def render(current: dict, previous: dict | None, reason: str) -> str:
    lines = [
        "# Unit and contract coverage", "",
        f"Source: `{current['commit']}`; configuration: {current['configuration']}; SDK: {current['sdk']}; uncommitted changes: {current['workingTreeDirty']}.", "",
        reason, "",
        "Advisory only. Overall figures do not replace the 100% line-and-branch requirement for new or materially changed unit-testable implementation.", "",
        "| Assembly | Lines | Change | Branches | Change |",
        "| --- | --- | --- | --- | --- |",
    ]
    for assembly, values in current["assemblies"].items():
        prior = previous["assemblies"].get(assembly) if previous else None
        lines.append(f"| {assembly} | {display(values, 'lines')} | {delta(values, prior, 'lines')} | {display(values, 'branches')} | {delta(values, prior, 'branches')} |")
    values = current["totals"]
    prior = previous["totals"] if previous else None
    lines.append(f"| Overall | {display(values, 'lines')} | {delta(values, prior, 'lines')} | {display(values, 'branches')} | {delta(values, prior, 'branches')} |")
    lines.extend(["", "Per-class counts are retained in coverage-summary.json; native JSON and Cobertura reports contain the individual uncovered locations.", ""])
    return "\n".join(lines)


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--results", type=Path, required=True)
    parser.add_argument("--output", type=Path, required=True)
    parser.add_argument("--configuration", choices=("Debug", "Release"), required=True)
    parser.add_argument("--expected-reports", type=int, default=6)
    parser.add_argument("--baseline", type=Path)
    parser.add_argument("--commit", required=True)
    parser.add_argument("--version", default="development")
    args = parser.parse_args()
    reports = sorted(args.results.rglob("coverage.*.json"))
    if len(reports) != args.expected_reports:
        raise ValueError(f"Expected {args.expected_reports} fresh native Coverlet reports, found {len(reports)}.")

    repo = Path(__file__).resolve().parents[2]
    production_assemblies = {path.stem + ".dll" for path in (repo / "src").glob("*/*.csproj")}
    classes = collect_classes(reports, production_assemblies)
    test_modules = []
    for result in sorted(args.results.rglob("*.trx")):
        tree = ET.parse(result)
        summary = tree.find(".//{*}ResultSummary")
        counters = summary.find("{*}Counters") if summary is not None else None
        unsuccessful = ("failed", "error", "timeout", "aborted", "inconclusive",
                        "passedButRunAborted", "notRunnable", "disconnected", "inProgress", "pending")
        if (summary is None or summary.get("outcome") not in {"Completed", "Passed"}
                or counters is None or int(counters.get("passed", "0")) == 0
                or any(int(counters.get(name, "0")) for name in unsuccessful)):
            raise ValueError("Coverage baselines require successful, non-empty test results.")
        test_modules.append(result.name)
    if len(test_modules) != args.expected_reports:
        raise ValueError("Each coverage module must have a corresponding successful TRX result.")
    assemblies = {}
    for name in sorted({item["assembly"] for item in classes}):
        assemblies[name] = totals([item for item in classes if item["assembly"] == name])
    policy = hashlib.sha256()
    # Test implementations may evolve without invalidating a comparison. Changes to
    # the collector, SDK, module selection or aggregation must establish a new baseline.
    policy_files = (
        Path(__file__), repo / "test/Directory.Packages.props", repo / "global.json",
        repo / ".github/workflows/tests.yml", repo / ".github/workflows/release.yml",
    )
    for path in policy_files:
        policy.update(path.read_text(encoding="utf-8").replace("\r\n", "\n").encode())
    current = {
        "schemaVersion": 1,
        "kind": "unit-contract",
        "policy": policy.hexdigest(),
        "configuration": args.configuration,
        "sdk": subprocess.check_output(["dotnet", "--version"], text=True).strip(),
        "os": platform.system(),
        "architecture": platform.machine(),
        "commit": args.commit,
        "version": args.version,
        "runId": os.environ.get("GITHUB_RUN_ID"),
        "reportCount": len(reports),
        "testModules": test_modules,
        "workingTreeDirty": bool(subprocess.check_output(
            ["git", "status", "--porcelain", "--untracked-files=normal"], cwd=repo, text=True,
        ).strip()),
        "assemblies": assemblies,
        "totals": totals(classes),
        "classes": classes,
    }
    previous = None
    reason = "No baseline available; this result establishes a baseline, not a change from zero."
    if args.baseline and args.baseline.is_file():
        candidate = json.loads(args.baseline.read_text(encoding="utf-8"))
        identity = ("schemaVersion", "kind", "policy", "configuration", "sdk", "os", "architecture", "reportCount", "testModules")
        drift = [key for key in identity if candidate.get(key) != current[key]]
        if drift:
            reason = "Baseline is not comparable: " + ", ".join(drift) + "."
        else:
            previous = candidate
            reason = f"Compared with source `{previous['commit']}` (build {previous.get('runId') or 'local'})."

    args.output.mkdir(parents=True, exist_ok=True)
    (args.output / "coverage-summary.json").write_text(json.dumps(current, indent=2) + "\n", encoding="utf-8")
    report = render(current, previous, reason)
    (args.output / "coverage.md").write_text(report, encoding="utf-8")
    print(report)
    summary = os.environ.get("GITHUB_STEP_SUMMARY")
    if summary:
        with Path(summary).open("a", encoding="utf-8") as stream:
            stream.write(report)


if __name__ == "__main__":
    main()
