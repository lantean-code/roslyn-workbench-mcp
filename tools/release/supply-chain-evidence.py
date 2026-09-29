#!/usr/bin/env python3
"""Validate and assemble release supply-chain evidence."""

from __future__ import annotations

import argparse
import hashlib
import json
import re
import sys
from pathlib import Path, PurePosixPath
from typing import NamedTuple


_PACKAGE_IDS = (
    "Lantean.Roslyn.Workbench.Mcp",
    "Lantean.Roslyn.Workbench.Mcp.Plugins",
)
_RETIRED_EXTENSIONS = {".deb", ".msi", ".msix", ".rpm"}
_SEVERITIES = {0: "low", 1: "moderate", 2: "high", 3: "critical"}
_SOURCE_PATH_PATTERN = re.compile(r"(?:(?<![A-Za-z])[A-Za-z]:[\\/]|/(?:home|mnt|private|tmp|Users)/)")
_LOCK_FRAMEWORK_ALIASES = {".NETStandard,Version=v2.0": "netstandard2.0"}


class LockedProject(NamedTuple):
    lock_path: Path
    relative_path: str


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--release-root", required=True, type=Path)
    parser.add_argument("--sbom", required=True, type=Path)
    parser.add_argument("--sbom-validation", required=True, type=Path)
    parser.add_argument("--dependency-root", required=True, type=Path)
    parser.add_argument("--dependency-inventory", required=True, type=Path)
    parser.add_argument("--vulnerabilities", required=True, type=Path)
    parser.add_argument("--version", required=True)
    parser.add_argument("--commit", required=True)
    parser.add_argument("--repository", required=True)
    parser.add_argument("--run-id", required=True)
    parser.add_argument("--run-attempt", required=True)
    parser.add_argument("--run-url", required=True)
    arguments = parser.parse_args()

    assemble_evidence(
        arguments.release_root,
        arguments.sbom,
        arguments.sbom_validation,
        arguments.dependency_root,
        arguments.dependency_inventory,
        arguments.vulnerabilities,
        arguments.version,
        arguments.commit,
        arguments.repository,
        arguments.run_id,
        arguments.run_attempt,
        arguments.run_url,
    )
    return 0


def assemble_evidence(
    release_root: Path,
    sbom_path: Path,
    sbom_validation_path: Path,
    dependency_root: Path,
    dependency_inventory_root: Path,
    vulnerabilities_root: Path,
    version: str,
    commit: str,
    repository: str,
    run_id: str,
    run_attempt: str,
    run_url: str,
) -> None:
    release_root = release_root.resolve()
    package_paths = require_release_subjects(
        release_root / "package",
        version,
        ".nupkg",
    )
    symbol_paths = require_release_subjects(
        release_root / "symbols",
        version,
        ".snupkg",
    )
    subject_paths = [*package_paths, *symbol_paths]
    reject_retired_packages(release_root)
    validate_release_manifest(release_root / "release-manifest.json", version, commit)

    supply_chain_root = release_root / "supply-chain"
    supply_chain_root.mkdir(parents=True, exist_ok=True)
    copied_sbom = supply_chain_root / f"roslyn-workbench-mcp-{version}.spdx.json"
    copied_validation = supply_chain_root / "sbom-validation.json"
    copy_json_document(sbom_path, copied_sbom)
    sanitise_sbom_validation(sbom_validation_path, copied_validation)

    subject_hashes = {
        relative_path(path, release_root): sha256(path)
        for path in subject_paths
    }
    validate_sbom(copied_sbom, subject_hashes)

    vulnerability_report, blocking_findings = sanitise_vulnerabilities(
        dependency_root,
        dependency_inventory_root,
        vulnerabilities_root,
    )
    vulnerability_output = supply_chain_root / "dependency-vulnerabilities.json"
    write_json(vulnerability_output, vulnerability_report)
    if blocking_findings:
        joined_findings = ", ".join(blocking_findings)
        raise ValueError(f"Release dependency scan found high or critical vulnerabilities: {joined_findings}.")

    evidence = {
        "schemaVersion": 2,
        "packageIds": list(_PACKAGE_IDS),
        "version": version,
        "commit": commit,
        "repository": repository,
        "workflowRun": {
            "id": run_id,
            "attempt": run_attempt,
            "url": run_url,
        },
        "subjects": [
            {"path": path, "sha256": digest}
            for path, digest in sorted(subject_hashes.items())
        ],
        "sbom": {
            "format": "SPDX",
            "version": "2.2",
            "path": relative_path(copied_sbom, release_root),
            "sha256": sha256(copied_sbom),
            "validation": relative_path(copied_validation, release_root),
        },
        "dependencyVulnerabilities": {
            "path": relative_path(vulnerability_output, release_root),
            "projectCount": vulnerability_report["projectCount"],
            "packageCount": vulnerability_report["packageCount"],
            "findingCount": vulnerability_report["findingCount"],
            "highestSeverity": vulnerability_report["highestSeverity"],
            "releaseBlockingFindingCount": 0,
        },
    }
    evidence_output = supply_chain_root / "release-evidence.json"
    write_json(evidence_output, evidence)
    reject_source_paths(copied_sbom, copied_validation, evidence_output, vulnerability_output)
    write_subject_checksums(supply_chain_root / "subjects.sha256", subject_paths)
    write_release_checksums(release_root)


def require_release_subjects(directory: Path, version: str, extension: str) -> list[Path]:
    expected_names = {
        f"{package_id}.{version}{extension}"
        for package_id in _PACKAGE_IDS
    }
    matches = sorted(directory.glob(f"*{extension}"))
    actual_names = {path.name for path in matches}
    if actual_names != expected_names:
        expected = ", ".join(sorted(expected_names))
        found = ", ".join(sorted(actual_names)) or "none"
        raise ValueError(f"Expected exactly [{expected}] in '{directory.name}', found: {found}.")
    return matches


def reject_retired_packages(release_root: Path) -> None:
    retired = sorted(
        relative_path(path, release_root)
        for path in release_root.rglob("*")
        if path.is_file() and path.suffix.lower() in _RETIRED_EXTENSIONS
    )
    if retired:
        raise ValueError(f"Retired package formats are present: {', '.join(retired)}.")


def validate_release_manifest(path: Path, version: str, commit: str) -> None:
    manifest = read_json_object(path)
    packages = manifest.get("packages")
    expected_packages = [
        {
            "id": "Lantean.Roslyn.Workbench.Mcp",
            "kind": "dotnet-tool",
        },
        {
            "id": "Lantean.Roslyn.Workbench.Mcp.Plugins",
            "kind": "library",
        },
    ]
    if manifest.get("schemaVersion") != 2 or packages != expected_packages:
        raise ValueError("Release manifest packages do not identify the exact release product set.")
    if manifest.get("version") != version:
        raise ValueError("Release manifest version does not match the release version.")
    if manifest.get("commit") != commit:
        raise ValueError("Release manifest commit does not match the release commit.")


def copy_json_document(source: Path, destination: Path) -> None:
    value = read_json(source)
    write_json(destination, value)


def sanitise_sbom_validation(source: Path, destination: Path) -> None:
    validation = read_json_object(source)
    errors = validation.get("ValidationErrors")
    summary = validation.get("Summary")
    if validation.get("Result") != "Success" or not isinstance(errors, dict) or not isinstance(summary, dict):
        raise ValueError("SBOM validation result is not successful or has an unsupported shape.")
    if errors.get("Count") != 0 or errors.get("Errors") != []:
        raise ValueError("SBOM validation result contains errors.")

    telemetry = summary.get("ValidationTelemetery")
    if not isinstance(telemetry, dict):
        raise ValueError("SBOM validation result is missing file telemetry.")
    expected_counts = {
        "FilesSuccessfulCount": 4,
        "TotalFilesInManifest": 4,
        "FilesValidatedCount": 4,
        "FilesFailedCount": 0,
    }
    for name, expected_value in expected_counts.items():
        if telemetry.get(name) != expected_value:
            raise ValueError(f"SBOM validation result '{name}' must be {expected_value}.")

    package_count = telemetry.get("TotalPackagesInManifest")
    if not isinstance(package_count, int) or isinstance(package_count, bool) or package_count < 1:
        raise ValueError("SBOM validation result must contain at least one package.")

    sanitised = {
        "schemaVersion": 1,
        "result": "success",
        "filesSuccessful": telemetry["FilesSuccessfulCount"],
        "filesInManifest": telemetry["TotalFilesInManifest"],
        "filesValidated": telemetry["FilesValidatedCount"],
        "filesFailed": telemetry["FilesFailedCount"],
        "packagesInManifest": package_count,
    }
    write_json(destination, sanitised)


def validate_sbom(path: Path, expected_files: dict[str, str]) -> None:
    sbom = read_json_object(path)
    if sbom.get("spdxVersion") != "SPDX-2.2":
        raise ValueError("SBOM must use SPDX 2.2.")

    actual_hashes: dict[str, str] = {}
    files = sbom.get("files")
    if not isinstance(files, list):
        raise ValueError("SBOM files must be an array.")
    for entry in files:
        if not isinstance(entry, dict) or not isinstance(entry.get("fileName"), str):
            raise ValueError("Every SBOM file must have a fileName.")
        checksums = entry.get("checksums")
        if not isinstance(checksums, list):
            raise ValueError("Every SBOM file must have checksums.")
        for checksum in checksums:
            if not isinstance(checksum, dict):
                raise ValueError("SBOM checksums must be objects.")
            if checksum.get("algorithm") == "SHA256" and isinstance(checksum.get("checksumValue"), str):
                normalised_name = normalise_sbom_path(entry["fileName"])
                actual_hashes[normalised_name] = checksum["checksumValue"].lower()

    for expected_path, expected_hash in expected_files.items():
        expected_name = PurePosixPath(expected_path).name
        matching_hashes = [
            digest
            for name, digest in actual_hashes.items()
            if PurePosixPath(name).name == expected_name
        ]
        if matching_hashes != [expected_hash]:
            raise ValueError(f"SBOM does not contain the expected SHA-256 for '{expected_name}'.")


def normalise_sbom_path(value: str) -> str:
    return value.replace("\\", "/").removeprefix("./")


def sanitise_vulnerabilities(
    dependency_root: Path,
    inventory_root: Path,
    vulnerabilities_root: Path,
) -> tuple[dict[str, object], list[str]]:
    locked_projects = discover_locked_projects(dependency_root)
    validate_report_set(inventory_root, locked_projects, "inventory")
    validate_report_set(vulnerabilities_root, locked_projects, "vulnerability")

    all_packages: set[tuple[str, str]] = set()
    all_sources: set[str] = set()
    findings_by_identity: dict[tuple[str, ...], dict[str, str]] = {}
    project_paths: list[str] = []
    for project_name, project in locked_projects.items():
        inventory, target_framework = read_inventory(inventory_root / f"{project_name}.json", project)
        findings, sources = read_vulnerability_scan(
            vulnerabilities_root / f"{project_name}.json",
            project,
            inventory,
            target_framework,
        )
        all_packages.update(inventory)
        all_sources.update(sources)
        project_paths.append(project.relative_path)
        for finding in findings:
            identity = tuple(finding[key] for key in sorted(finding))
            findings_by_identity[identity] = finding

    findings = sorted(
        findings_by_identity.values(),
        key=lambda finding: (finding["packageId"].lower(), finding["resolvedVersion"], finding["advisoryUrl"]),
    )
    blocking: list[str] = []
    for finding in findings:
        if finding["severity"] in {"high", "critical"}:
            blocking.append(f"{finding['packageId']} {finding['resolvedVersion']} ({finding['severity']})")

    severity_rank = {value: key for key, value in _SEVERITIES.items()}
    highest = max((severity_rank[finding["severity"]] for finding in findings), default=None)
    report: dict[str, object] = {
        "schemaVersion": 1,
        "projects": project_paths,
        "projectCount": len(project_paths),
        "packageCount": len(all_packages),
        "sources": sorted(all_sources),
        "findingCount": len(findings),
        "highestSeverity": _SEVERITIES[highest] if highest is not None else None,
        "findings": findings,
    }
    return report, blocking


def discover_locked_projects(dependency_root: Path) -> dict[str, LockedProject]:
    dependency_root = dependency_root.resolve()
    lock_paths = sorted(dependency_root.glob("Roslyn.Workbench.Mcp*/packages.lock.json"))
    if not lock_paths:
        raise ValueError("The dependency root does not contain source-project lock files.")

    projects: dict[str, LockedProject] = {}
    for lock_path in lock_paths:
        project_name = lock_path.parent.name
        project_path = lock_path.with_name(f"{project_name}.csproj")
        if not project_path.is_file():
            raise ValueError(f"Locked source project '{project_name}' does not contain its expected project file.")
        projects[project_name] = LockedProject(
            lock_path=lock_path,
            relative_path=project_path.relative_to(dependency_root.parent).as_posix(),
        )
    return projects


def validate_report_set(directory: Path, projects: dict[str, LockedProject], report_kind: str) -> None:
    expected_names = {f"{project_name}.json" for project_name in projects}
    actual_names = {path.name for path in directory.glob("*.json") if path.is_file()}
    if actual_names != expected_names:
        missing = ", ".join(sorted(expected_names - actual_names)) or "none"
        additional = ", ".join(sorted(actual_names - expected_names)) or "none"
        raise ValueError(
            f"Dependency {report_kind} reports do not match locked source projects; "
            f"missing: {missing}; additional: {additional}."
        )


def read_inventory(path: Path, project: LockedProject) -> tuple[set[tuple[str, str]], str]:
    source = read_package_list(path, "inventory")
    project_entry = read_single_project(source, project, "inventory")
    target_framework, locked_packages = read_locked_packages(project.lock_path)
    framework = read_required_framework(project_entry, "inventory", target_framework)
    packages = read_framework_packages(framework, "inventory")
    if packages != locked_packages:
        raise ValueError(f"Dependency inventory for '{project.relative_path}' does not match its lock file.")
    return packages, target_framework


def read_locked_packages(path: Path | str) -> tuple[str, set[tuple[str, str]]]:
    lock = read_json_object(Path(path))
    dependencies = lock.get("dependencies")
    if lock.get("version") != 2 or not isinstance(dependencies, dict) or len(dependencies) != 1:
        raise ValueError(f"Dependency lock '{Path(path).name}' must contain one schema-v2 framework graph.")
    lock_framework, framework = next(iter(dependencies.items()))
    if not isinstance(lock_framework, str):
        raise ValueError(f"Dependency lock '{Path(path).name}' has an invalid framework name.")
    target_framework = _LOCK_FRAMEWORK_ALIASES.get(lock_framework, lock_framework)
    if not isinstance(framework, dict):
        raise ValueError(f"Dependency lock '{Path(path).name}' has an invalid framework graph.")

    packages: set[tuple[str, str]] = set()
    for package_id, value in framework.items():
        if not isinstance(package_id, str) or not isinstance(value, dict):
            raise ValueError(f"Dependency lock '{Path(path).name}' contains an invalid package entry.")
        if value.get("type") == "Project":
            continue
        packages.add((package_id.lower(), required_string(value, "resolved")))
    return target_framework, packages


def read_package_list(path: Path, report_kind: str) -> dict[str, object]:
    source = read_json_object(path)
    if source.get("version") != 1:
        raise ValueError(f"Dependency {report_kind} output must use JSON schema version 1.")
    return source


def read_single_project(
    source: dict[str, object],
    expected_project: LockedProject,
    report_kind: str,
) -> dict[str, object]:
    projects = source.get("projects")
    if not isinstance(projects, list) or len(projects) != 1 or not isinstance(projects[0], dict):
        raise ValueError(f"Dependency {report_kind} output must contain exactly one project.")
    project = projects[0]
    actual_path = required_string(project, "path")
    expected_path = expected_project.relative_path
    if not path_has_suffix(actual_path, expected_path):
        raise ValueError(f"Dependency {report_kind} output does not identify '{expected_path}'.")
    return project


def path_has_suffix(actual_path: str, expected_path: str) -> bool:
    actual_parts = PurePosixPath(actual_path.replace("\\", "/")).parts
    expected_parts = PurePosixPath(expected_path).parts
    return len(actual_parts) >= len(expected_parts) and actual_parts[-len(expected_parts):] == expected_parts


def read_required_framework(
    project: dict[str, object],
    report_kind: str,
    target_framework: str,
) -> dict[str, object]:
    frameworks = project.get("frameworks")
    return validate_single_framework(frameworks, report_kind, target_framework)


def read_optional_framework(
    project: dict[str, object],
    report_kind: str,
    target_framework: str,
) -> dict[str, object] | None:
    frameworks = project.get("frameworks")
    if frameworks is None:
        return None
    return validate_single_framework(frameworks, report_kind, target_framework)


def validate_single_framework(
    frameworks: object,
    report_kind: str,
    target_framework: str,
) -> dict[str, object]:
    if not isinstance(frameworks, list) or len(frameworks) != 1 or not isinstance(frameworks[0], dict):
        raise ValueError(f"Dependency {report_kind} output must contain exactly one framework.")
    framework = frameworks[0]
    if framework.get("framework") != target_framework:
        raise ValueError(f"Dependency {report_kind} output must target {target_framework}.")
    return framework


def read_framework_packages(framework: dict[str, object], report_kind: str) -> set[tuple[str, str]]:
    packages: set[tuple[str, str]] = set()
    for dependency_kind in ("topLevelPackages", "transitivePackages"):
        values = framework.get(dependency_kind)
        if not isinstance(values, list):
            raise ValueError(f"Dependency {report_kind} {dependency_kind} must be an array.")
        for value in values:
            if not isinstance(value, dict):
                raise ValueError(f"Dependency {report_kind} packages must be objects.")
            package = (required_string(value, "id").lower(), required_string(value, "resolvedVersion"))
            if package in packages:
                raise ValueError(f"Dependency {report_kind} contains a duplicate package.")
            packages.add(package)
    return packages


def read_vulnerability_scan(
    path: Path,
    project: LockedProject,
    inventory: set[tuple[str, str]],
    target_framework: str,
) -> tuple[list[dict[str, str]], list[str]]:
    source = read_package_list(path, "vulnerability")
    sources = read_sources(source)
    project_entry = read_single_project(source, project, "vulnerability")
    framework = read_optional_framework(project_entry, "vulnerability", target_framework)
    if framework is None:
        return [], sources

    findings = read_framework_findings(framework)
    for finding in findings:
        package = (finding["packageId"].lower(), finding["resolvedVersion"])
        if package not in inventory:
            raise ValueError(
                f"Dependency vulnerability finding for '{finding['packageId']}' is absent from its locked inventory."
            )
    return findings, sources


def read_sources(source: dict[str, object]) -> list[str]:
    raw_sources = source.get("sources")
    if (
        not isinstance(raw_sources, list)
        or not raw_sources
        or not all(isinstance(item, str) and item for item in raw_sources)
    ):
        raise ValueError("Dependency vulnerability sources must contain at least one non-empty string.")
    return sorted(set(raw_sources))


def read_framework_findings(framework: object) -> list[dict[str, str]]:
    if not isinstance(framework, dict):
        raise ValueError("Dependency vulnerability frameworks must be objects.")
    findings: list[dict[str, str]] = []
    for dependency_kind in ("topLevelPackages", "transitivePackages"):
        packages = framework.get(dependency_kind)
        if not isinstance(packages, list):
            raise ValueError(f"Dependency vulnerability {dependency_kind} must be an array.")
        for package in packages:
            findings.extend(read_package_findings(package, dependency_kind))
    return findings


def read_package_findings(package: object, dependency_kind: str) -> list[dict[str, str]]:
    if not isinstance(package, dict):
        raise ValueError("Dependency vulnerability packages must be objects.")
    package_id = required_string(package, "id")
    resolved_version = required_string(package, "resolvedVersion")
    vulnerabilities = package.get("vulnerabilities", [])
    if not isinstance(vulnerabilities, list):
        raise ValueError("Package vulnerabilities must be an array.")

    findings: list[dict[str, str]] = []
    for vulnerability in vulnerabilities:
        if not isinstance(vulnerability, dict):
            raise ValueError("Package vulnerabilities must be objects.")
        severity_value = vulnerability.get("severity")
        if not isinstance(severity_value, int) or isinstance(severity_value, bool) or severity_value not in _SEVERITIES:
            raise ValueError(f"Package '{package_id}' has an unknown vulnerability severity.")
        findings.append(
            {
                "packageId": package_id,
                "resolvedVersion": resolved_version,
                "dependencyKind": "top-level" if dependency_kind == "topLevelPackages" else "transitive",
                "severity": _SEVERITIES[severity_value],
                "advisoryUrl": required_string(vulnerability, "advisoryurl"),
            }
        )
    return findings


def required_string(values: dict[str, object], key: str) -> str:
    value = values.get(key)
    if not isinstance(value, str) or not value:
        raise ValueError(f"Dependency evidence is missing '{key}'.")
    return value


def reject_source_paths(*paths: Path) -> None:
    for path in paths:
        text = path.read_text(encoding="utf-8")
        if _SOURCE_PATH_PATTERN.search(text):
            raise ValueError(f"Generated evidence '{path.name}' contains a source-machine path.")


def write_subject_checksums(path: Path, subject_paths: list[Path]) -> None:
    lines = [
        f"{sha256(subject_path)}  {subject_path.name}"
        for subject_path in sorted(subject_paths)
    ]
    path.write_text("\n".join(lines) + "\n", encoding="utf-8", newline="\n")


def write_release_checksums(release_root: Path) -> None:
    checksum_path = release_root / "checksums.sha256"
    included_paths = sorted(
        path
        for path in release_root.rglob("*")
        if path.is_file()
        and path != checksum_path
        and path.name != "subjects.sha256"
    )
    lines = [f"{sha256(path)}  {relative_path(path, release_root)}" for path in included_paths]
    checksum_path.write_text("\n".join(lines) + "\n", encoding="utf-8", newline="\n")


def read_json(path: Path) -> object:
    return json.loads(path.read_text(encoding="utf-8"))


def read_json_object(path: Path) -> dict[str, object]:
    value = read_json(path)
    if not isinstance(value, dict):
        raise ValueError(f"'{path.name}' must contain a JSON object.")
    return value


def write_json(path: Path, value: object) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(json.dumps(value, indent=2) + "\n", encoding="utf-8", newline="\n")


def sha256(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as stream:
        for block in iter(lambda: stream.read(1024 * 1024), b""):
            digest.update(block)
    return digest.hexdigest()


def relative_path(path: Path, root: Path) -> str:
    return path.resolve().relative_to(root.resolve()).as_posix()


if __name__ == "__main__":
    try:
        raise SystemExit(main())
    except (OSError, ValueError, json.JSONDecodeError) as exception:
        print(f"Supply-chain evidence error: {exception}", file=sys.stderr)
        raise SystemExit(2) from None
