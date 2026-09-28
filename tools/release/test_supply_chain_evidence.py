#!/usr/bin/env python3
"""Tests for supply-chain-evidence.py."""

from __future__ import annotations

import hashlib
import importlib.util
import json
import tempfile
import unittest
from pathlib import Path


_SCRIPT_PATH = Path(__file__).with_name("supply-chain-evidence.py")
_SPEC = importlib.util.spec_from_file_location("supply_chain_evidence", _SCRIPT_PATH)
if _SPEC is None or _SPEC.loader is None:
    raise RuntimeError("Could not load supply-chain evidence module.")
_MODULE = importlib.util.module_from_spec(_SPEC)
_SPEC.loader.exec_module(_MODULE)


class SupplyChainEvidenceTests(unittest.TestCase):
    version = "1.2.3-beta.4"
    commit = "a" * 40

    def test_assemble_evidence_writes_sanitised_complete_evidence(self) -> None:
        with tempfile.TemporaryDirectory() as directory:
            fixture = self.create_fixture(Path(directory), severity=1, include_source_path=True)

            self.assemble(fixture)

            evidence_root = fixture["release_root"] / "supply-chain"
            evidence = self.read_json(evidence_root / "release-evidence.json")
            vulnerabilities = self.read_json(evidence_root / "dependency-vulnerabilities.json")
            validation = self.read_json(evidence_root / "sbom-validation.json")
            checksums = (fixture["release_root"] / "checksums.sha256").read_text(encoding="utf-8")

            self.assertEqual(2, len(evidence["subjects"]))
            self.assertEqual("moderate", vulnerabilities["highestSeverity"])
            self.assertEqual(1, vulnerabilities["projectCount"])
            self.assertEqual(1, vulnerabilities["packageCount"])
            self.assertEqual(
                ["src/Roslyn.Workbench.Mcp.Plugins.Core/Roslyn.Workbench.Mcp.Plugins.Core.csproj"],
                vulnerabilities["projects"],
            )
            self.assertEqual("success", validation["result"])
            self.assertNotIn(str(fixture["release_root"]), json.dumps(vulnerabilities))
            self.assertNotIn(str(fixture["release_root"]), json.dumps(validation))
            self.assertIn("supply-chain/release-evidence.json", checksums)
            self.assertNotIn("subjects.sha256", checksums)

    def test_assemble_evidence_rejects_retired_package_format(self) -> None:
        with tempfile.TemporaryDirectory() as directory:
            fixture = self.create_fixture(Path(directory))
            (fixture["release_root"] / "legacy.msi").write_bytes(b"installer")

            with self.assertRaisesRegex(ValueError, "Retired package formats"):
                self.assemble(fixture)

    def test_assemble_evidence_rejects_missing_or_extra_package(self) -> None:
        with tempfile.TemporaryDirectory() as directory:
            fixture = self.create_fixture(Path(directory))
            (fixture["release_root"] / "package" / "extra.nupkg").write_bytes(b"extra")

            with self.assertRaisesRegex(ValueError, "Expected only"):
                self.assemble(fixture)

    def test_assemble_evidence_rejects_sbom_hash_mismatch(self) -> None:
        with tempfile.TemporaryDirectory() as directory:
            fixture = self.create_fixture(Path(directory), corrupt_sbom_hash=True)

            with self.assertRaisesRegex(ValueError, "expected SHA-256"):
                self.assemble(fixture)

    def test_assemble_evidence_rejects_high_vulnerability(self) -> None:
        with tempfile.TemporaryDirectory() as directory:
            fixture = self.create_fixture(Path(directory), severity=2)

            with self.assertRaisesRegex(ValueError, "high or critical"):
                self.assemble(fixture)

    def test_assemble_evidence_rejects_unknown_vulnerability_severity(self) -> None:
        with tempfile.TemporaryDirectory() as directory:
            fixture = self.create_fixture(Path(directory), severity=4)

            with self.assertRaisesRegex(ValueError, "unknown vulnerability severity"):
                self.assemble(fixture)

    def test_assemble_evidence_rejects_malformed_vulnerability_schema(self) -> None:
        with tempfile.TemporaryDirectory() as directory:
            fixture = self.create_fixture(Path(directory))
            fixture["vulnerability_file"].write_text("[]\n", encoding="utf-8")

            with self.assertRaisesRegex(ValueError, "must contain a JSON object"):
                self.assemble(fixture)

    def test_assemble_evidence_rejects_missing_project_scan(self) -> None:
        with tempfile.TemporaryDirectory() as directory:
            fixture = self.create_fixture(Path(directory))
            fixture["vulnerability_file"].unlink()

            with self.assertRaisesRegex(ValueError, "missing: Roslyn.Workbench.Mcp.Plugins.Core.json"):
                self.assemble(fixture)

    def test_assemble_evidence_rejects_inventory_without_framework(self) -> None:
        with tempfile.TemporaryDirectory() as directory:
            fixture = self.create_fixture(Path(directory))
            inventory = self.read_json(fixture["inventory_file"])
            del inventory["projects"][0]["frameworks"]
            self.write_json(fixture["inventory_file"], inventory)

            with self.assertRaisesRegex(ValueError, "must contain exactly one framework"):
                self.assemble(fixture)

    def test_assemble_evidence_rejects_inventory_without_package_collection(self) -> None:
        with tempfile.TemporaryDirectory() as directory:
            fixture = self.create_fixture(Path(directory))
            inventory = self.read_json(fixture["inventory_file"])
            del inventory["projects"][0]["frameworks"][0]["transitivePackages"]
            self.write_json(fixture["inventory_file"], inventory)

            with self.assertRaisesRegex(ValueError, "transitivePackages must be an array"):
                self.assemble(fixture)

    def test_assemble_evidence_rejects_inventory_that_omits_locked_private_dependency(self) -> None:
        with tempfile.TemporaryDirectory() as directory:
            fixture = self.create_fixture(Path(directory))
            inventory = self.read_json(fixture["inventory_file"])
            inventory["projects"][0]["frameworks"][0]["topLevelPackages"] = []
            self.write_json(fixture["inventory_file"], inventory)

            with self.assertRaisesRegex(ValueError, "does not match its lock file"):
                self.assemble(fixture)

    def test_assemble_evidence_rejects_empty_vulnerability_sources(self) -> None:
        with tempfile.TemporaryDirectory() as directory:
            fixture = self.create_fixture(Path(directory))
            vulnerabilities = self.read_json(fixture["vulnerability_file"])
            vulnerabilities["sources"] = []
            self.write_json(fixture["vulnerability_file"], vulnerabilities)

            with self.assertRaisesRegex(ValueError, "at least one non-empty string"):
                self.assemble(fixture)

    def test_assemble_evidence_rejects_vulnerability_output_without_expected_project(self) -> None:
        with tempfile.TemporaryDirectory() as directory:
            fixture = self.create_fixture(Path(directory))
            vulnerabilities = self.read_json(fixture["vulnerability_file"])
            vulnerabilities["projects"] = []
            self.write_json(fixture["vulnerability_file"], vulnerabilities)

            with self.assertRaisesRegex(ValueError, "exactly one project"):
                self.assemble(fixture)

    def test_assemble_evidence_rejects_vulnerability_absent_from_locked_inventory(self) -> None:
        with tempfile.TemporaryDirectory() as directory:
            fixture = self.create_fixture(Path(directory), severity=1)
            vulnerabilities = self.read_json(fixture["vulnerability_file"])
            project = vulnerabilities["projects"][0]
            framework = project["frameworks"][0]
            package = framework["topLevelPackages"][0]
            package["id"] = "Uninventoried.Dependency"
            self.write_json(fixture["vulnerability_file"], vulnerabilities)

            with self.assertRaisesRegex(ValueError, "absent from its locked inventory"):
                self.assemble(fixture)

    def test_assemble_evidence_rejects_unsuccessful_sbom_validation(self) -> None:
        with tempfile.TemporaryDirectory() as directory:
            fixture = self.create_fixture(Path(directory))
            validation = self.read_json(fixture["validation"])
            validation["Result"] = "Failure"
            self.write_json(fixture["validation"], validation)

            with self.assertRaisesRegex(ValueError, "not successful"):
                self.assemble(fixture)

    def create_fixture(
        self,
        root: Path,
        severity: int | None = None,
        include_source_path: bool = False,
        corrupt_sbom_hash: bool = False,
    ) -> dict[str, Path]:
        release_root = root / "release"
        package_root = release_root / "package"
        symbols_root = release_root / "symbols"
        package_root.mkdir(parents=True)
        symbols_root.mkdir(parents=True)
        package = package_root / f"Lantean.Roslyn.Workbench.Mcp.{self.version}.nupkg"
        symbols = symbols_root / f"Lantean.Roslyn.Workbench.Mcp.{self.version}.snupkg"
        package.write_bytes(b"package")
        symbols.write_bytes(b"symbols")
        self.write_json(
            release_root / "release-manifest.json",
            {
                "packageId": "Lantean.Roslyn.Workbench.Mcp",
                "version": self.version,
                "commit": self.commit,
            },
        )

        sbom = root / "sbom.json"
        self.write_json(
            sbom,
            {
                "spdxVersion": "SPDX-2.2",
                "files": [
                    self.sbom_file(package, f"drop/{package.name}", corrupt_sbom_hash),
                    self.sbom_file(symbols, f"drop/{symbols.name}", False),
                ],
            },
        )
        validation = root / "validation.json"
        self.write_json(
            validation,
            {
                "Result": "Success",
                "ValidationErrors": {"Count": 0, "Errors": []},
                "Summary": {
                    "ValidationTelemetery": {
                        "FilesSuccessfulCount": 2,
                        "TotalFilesInManifest": 2,
                        "FilesValidatedCount": 2,
                        "FilesFailedCount": 0,
                        "TotalPackagesInManifest": 10,
                    },
                    "Parameters": {
                        "BuildDropPath": str(release_root),
                    },
                },
            },
        )

        dependency_root = root / "src"
        project_name = "Roslyn.Workbench.Mcp.Plugins.Core"
        project_root = dependency_root / project_name
        project_root.mkdir(parents=True)
        project_path = project_root / f"{project_name}.csproj"
        project_path.write_text("<Project />\n", encoding="utf-8")
        self.write_json(
            project_root / "packages.lock.json",
            {
                "version": 2,
                "dependencies": {
                    "net10.0": {
                        "AsyncFixer": {
                            "type": "Direct",
                            "requested": "[2.1.0, )",
                            "resolved": "2.1.0",
                            "contentHash": "fixture",
                        }
                    }
                },
            },
        )
        reported_project_path = str(project_path) if include_source_path else project_path.relative_to(root).as_posix()
        inventory_root = root / "inventory"
        vulnerability_root = root / "vulnerabilities"
        inventory_file = inventory_root / f"{project_name}.json"
        vulnerability_file = vulnerability_root / f"{project_name}.json"
        package_inventory = {
            "id": "AsyncFixer",
            "requestedVersion": "2.1.0",
            "resolvedVersion": "2.1.0",
        }
        self.write_json(
            inventory_file,
            {
                "version": 1,
                "parameters": "--include-transitive",
                "projects": [
                    {
                        "path": reported_project_path,
                        "frameworks": [
                            {
                                "framework": "net10.0",
                                "topLevelPackages": [package_inventory],
                                "transitivePackages": [],
                            }
                        ],
                    }
                ],
            },
        )
        package_values: list[dict[str, object]] = []
        if severity is not None:
            package_values.append(
                {
                    "id": "AsyncFixer",
                    "resolvedVersion": "2.1.0",
                    "vulnerabilities": [
                        {
                            "severity": severity,
                            "advisoryurl": "https://example.test/advisories/1",
                        }
                    ],
                }
            )
        project: dict[str, object] = {"path": reported_project_path}
        if package_values:
            project["frameworks"] = [
                {
                    "framework": "net10.0",
                    "topLevelPackages": package_values,
                    "transitivePackages": [],
                }
            ]
        self.write_json(
            vulnerability_file,
            {
                "version": 1,
                "parameters": "--vulnerable --include-transitive",
                "sources": ["https://api.nuget.org/v3/index.json"],
                "projects": [project],
            },
        )
        return {
            "release_root": release_root,
            "sbom": sbom,
            "validation": validation,
            "dependency_root": dependency_root,
            "inventory": inventory_root,
            "inventory_file": inventory_file,
            "vulnerabilities": vulnerability_root,
            "vulnerability_file": vulnerability_file,
        }

    def assemble(self, fixture: dict[str, Path]) -> None:
        _MODULE.assemble_evidence(
            fixture["release_root"],
            fixture["sbom"],
            fixture["validation"],
            fixture["dependency_root"],
            fixture["inventory"],
            fixture["vulnerabilities"],
            self.version,
            self.commit,
            "lantean-code/roslyn-workbench-mcp",
            "123",
            "1",
            "https://github.com/lantean-code/roslyn-workbench-mcp/actions/runs/123",
        )

    @staticmethod
    def sbom_file(path: Path, name: str, corrupt: bool) -> dict[str, object]:
        digest = hashlib.sha256(path.read_bytes()).hexdigest()
        return {
            "fileName": name,
            "checksums": [
                {
                    "algorithm": "SHA256",
                    "checksumValue": "0" * 64 if corrupt else digest,
                }
            ],
        }

    @staticmethod
    def write_json(path: Path, value: object) -> None:
        path.parent.mkdir(parents=True, exist_ok=True)
        path.write_text(json.dumps(value, indent=2) + "\n", encoding="utf-8")

    @staticmethod
    def read_json(path: Path) -> dict[str, object]:
        value = json.loads(path.read_text(encoding="utf-8"))
        if not isinstance(value, dict):
            raise AssertionError("Expected a JSON object.")
        return value


if __name__ == "__main__":
    unittest.main()
