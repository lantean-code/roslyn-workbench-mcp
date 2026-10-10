"""Tests for exact-package Registry preparation and anonymous verification."""

import copy
import contextlib
import importlib.util
import io
import json
import tempfile
import unittest
import urllib.error
import zipfile
from pathlib import Path
from unittest.mock import patch


SPEC = importlib.util.spec_from_file_location("registry_entry", Path(__file__).with_name("registry-entry.py"))
MODULE = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(MODULE)


class RegistryEntryTests(unittest.TestCase):
    version = "1.0.1"

    def setUp(self):
        self.enterContext(contextlib.redirect_stdout(io.StringIO()))

    def manifest(self):
        return {
            "$schema": "https://static.modelcontextprotocol.io/schemas/2025-12-11/server.schema.json",
            "name": MODULE.SERVER_NAME,
            "title": "Roslyn Workbench MCP",
            "description": "Local Roslyn-powered code analysis.",
            "version": self.version,
            "websiteUrl": f"https://lantean-code.github.io/roslyn-workbench-mcp/{self.version}/",
            "repository": {"url": MODULE.REPOSITORY_URL, "source": "github"},
            "packages": [{
                "registryType": "nuget",
                "registryBaseUrl": "https://api.nuget.org/v3/index.json",
                "identifier": MODULE.PACKAGE_ID,
                "version": self.version,
                "runtimeHint": "dnx",
                "transport": {"type": "stdio"},
                "packageArguments": [],
            }],
            "icons": [{
                "src": f"https://raw.githubusercontent.com/lantean-code/roslyn-workbench-mcp/{self.version}/assets/icons/roslyn-workbench-mcp-128.png",
                "mimeType": "image/png",
                "sizes": ["128x128"],
            }],
        }

    def entry(self, manifest):
        return {
            "server": copy.deepcopy(manifest),
            "_meta": {"io.modelcontextprotocol.registry/official": {"status": "active"}},
        }

    def package(self, manifest_bytes, include_manifest=True):
        stream = io.BytesIO()
        with zipfile.ZipFile(stream, "w") as archive:
            archive.writestr("README.md", "README")
            if include_manifest:
                archive.writestr(".mcp/server.json", manifest_bytes)

        return stream.getvalue()

    def downloads(self, package_bytes, readme=None):
        if readme is None:
            readme = f"<!-- mcp-name: {MODULE.SERVER_NAME} -->".encode()

        return [package_bytes, readme, b"docs", b"icon"]

    def test_prepare_preserves_exact_manifest_bytes_and_records_public_hashes(self):
        manifest_bytes = json.dumps(self.manifest(), indent=4).encode() + b"\r\n"
        package_bytes = self.package(manifest_bytes)

        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            github_output = root / "github-output"
            with patch.object(MODULE, "download", side_effect=self.downloads(package_bytes)), patch.object(MODULE, "read_public_entry", return_value=None):
                MODULE.prepare(self.version, root, github_output)

            self.assertEqual(manifest_bytes, (root / "server.json").read_bytes())
            evidence = json.loads((root / "preflight.json").read_text())
            self.assertEqual(MODULE.hashlib.sha256(package_bytes).hexdigest(), evidence["packageSha256"])
            self.assertEqual(MODULE.hashlib.sha256(manifest_bytes).hexdigest(), evidence["manifestSha256"])
            self.assertFalse(evidence["alreadyPublished"])
            self.assertEqual("already-published=false\n", github_output.read_text())

    def test_prepare_recognises_matching_existing_entry(self):
        manifest = self.manifest()
        package_bytes = self.package(json.dumps(manifest).encode())

        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            with patch.object(MODULE, "download", side_effect=self.downloads(package_bytes)), patch.object(MODULE, "read_public_entry", return_value=self.entry(manifest)):
                MODULE.prepare(self.version, root, None)

            self.assertTrue(json.loads((root / "preflight.json").read_text())["alreadyPublished"])

    def test_prepare_rejects_missing_manifest_or_public_ownership_marker(self):
        manifest_bytes = json.dumps(self.manifest()).encode()
        cases = [
            self.downloads(self.package(manifest_bytes, include_manifest=False)),
            self.downloads(self.package(manifest_bytes), readme=b"Missing marker"),
        ]

        for responses in cases:
            with self.subTest(responses=len(responses)), tempfile.TemporaryDirectory() as directory:
                with patch.object(MODULE, "download", side_effect=responses), self.assertRaises(ValueError):
                    MODULE.prepare(self.version, Path(directory), None)

                self.assertFalse((Path(directory) / "server.json").exists())

    def test_manifest_rejects_wrong_identity_version_transport_runtime_and_metadata(self):
        changes = [
            ("name", "io.github.someone/other"),
            ("version", "1.0.0"),
            ("websiteUrl", "https://example.com/"),
            ("repository", {}),
            ("icons", []),
            ("remotes", [{"type": "streamable-http"}]),
        ]

        for field, value in changes:
            with self.subTest(field=field):
                manifest = self.manifest()
                manifest[field] = value
                with self.assertRaises(ValueError):
                    MODULE.validate_manifest(manifest, self.version)

        package_changes = [
            ("identifier", "Lantean.Roslyn.Workbench.Mcp.Plugins"),
            ("version", "1.0.0"),
            ("runtimeHint", "npx"),
            ("transport", {"type": "http"}),
            ("packageArguments", ["--unsafe"]),
        ]

        for field, value in package_changes:
            with self.subTest(package_field=field):
                manifest = self.manifest()
                manifest["packages"][0][field] = value
                with self.assertRaises(ValueError):
                    MODULE.validate_manifest(manifest, self.version)

    def test_invalid_versions_fail_before_network_access(self):
        for version in ["", "v1.0.1", "../1.0.1", "1.0.1\n", "1.0.1+build", "01.0.1", "$(command)"]:
            with self.subTest(version=version), patch.object(MODULE, "download") as download:
                with self.assertRaises(ValueError):
                    MODULE.prepare(version, Path("unused"), None)

                download.assert_not_called()

        MODULE.validate_version("1.0.0-rc.1")

    def test_only_registry_404_is_treated_as_absent(self):
        for status in [401, 403, 404, 429, 500]:
            error = urllib.error.HTTPError("https://example.com", status, "Failure", {}, None)
            with self.subTest(status=status), patch.object(MODULE, "download", side_effect=error):
                if status == 404:
                    self.assertIsNone(MODULE.read_public_entry(self.version))
                else:
                    with self.assertRaises(urllib.error.HTTPError):
                        MODULE.read_public_entry(self.version)

    def test_public_entry_uses_encoded_name_and_anonymous_download(self):
        entry = self.entry(self.manifest())

        with patch.object(MODULE, "download", return_value=json.dumps(entry).encode()) as download:
            self.assertEqual(entry, MODULE.read_public_entry(self.version))

        download.assert_called_once_with(
            "https://registry.modelcontextprotocol.io/v0.1/servers/io.github.lantean-code%2Froslyn-workbench-mcp/versions/1.0.1"
        )

    def test_download_has_timeout_and_no_authorisation_header(self):
        with patch.object(MODULE.urllib.request, "urlopen") as urlopen:
            urlopen.return_value.__enter__.return_value.read.return_value = b"public bytes"
            self.assertEqual(b"public bytes", MODULE.download("https://example.com"))

        request = urlopen.call_args.args[0]
        self.assertEqual("https://example.com", request.full_url)
        self.assertIsNone(request.get_header("Authorization"))
        self.assertEqual(60, urlopen.call_args.kwargs["timeout"])

    def test_cli_routes_operations_without_authentication_or_publication(self):
        for operation in ["prepare", "verify"]:
            arguments = ["registry-entry.py", operation, "--version", self.version, "--output", "evidence"]

            with self.subTest(operation=operation), patch("sys.argv", arguments):
                with patch.object(MODULE, "prepare") as prepare, patch.object(MODULE, "verify") as verify:
                    MODULE.main()

                if operation == "prepare":
                    prepare.assert_called_once_with(self.version, Path("evidence"), None)
                    verify.assert_not_called()
                else:
                    verify.assert_called_once_with(self.version, Path("evidence"))
                    prepare.assert_not_called()

    def test_existing_entry_must_match_and_be_active(self):
        manifest = self.manifest()
        for field in ["name", "version", "websiteUrl", "icons", "repository", "packages", "description"]:
            with self.subTest(field=field):
                entry = self.entry(manifest)
                entry["server"][field] = None
                with self.assertRaises(ValueError):
                    MODULE.assert_matching_entry(entry, manifest)

        entry = self.entry(manifest)
        entry["server"]["remotes"] = [{"type": "http"}]
        with self.assertRaises(ValueError):
            MODULE.assert_matching_entry(entry, manifest)

        entry = self.entry(manifest)
        entry["_meta"]["io.modelcontextprotocol.registry/official"]["status"] = "deleted"
        with self.assertRaises(ValueError):
            MODULE.assert_matching_entry(entry, manifest)

    def test_registry_may_omit_schema_and_empty_package_arguments(self):
        manifest = self.manifest()
        entry = self.entry(manifest)
        del entry["server"]["$schema"]
        del entry["server"]["packages"][0]["packageArguments"]
        MODULE.assert_matching_entry(entry, manifest)

    def test_verify_retains_only_successful_public_response(self):
        manifest = self.manifest()

        for entry in [None, self.entry(manifest)]:
            with self.subTest(present=entry is not None), tempfile.TemporaryDirectory() as directory:
                root = Path(directory)
                (root / "server.json").write_text(json.dumps(manifest))
                with patch.object(MODULE, "read_public_entry", return_value=entry):
                    if entry is None:
                        with self.assertRaises(ValueError):
                            MODULE.verify(self.version, root)

                        self.assertFalse((root / "registry-response.json").exists())
                    else:
                        MODULE.verify(self.version, root)
                        self.assertEqual(entry, json.loads((root / "registry-response.json").read_text()))


if __name__ == "__main__":
    unittest.main()
