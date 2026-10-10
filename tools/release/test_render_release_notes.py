"""Regression coverage for preview and versioned release-note links."""

import subprocess
import sys
import tempfile
import unittest
from pathlib import Path


class RenderReleaseNotesTests(unittest.TestCase):
    def render(self, version: str | None = None) -> str:
        renderer = Path(__file__).with_name("render-release-notes.py")
        with tempfile.TemporaryDirectory() as directory:
            output = Path(directory) / "notes.md"
            command = [sys.executable, str(renderer), "--output", str(output)]
            if version is not None:
                command.extend(["--version", version])
            subprocess.run(command, check=True, capture_output=True, text=True)
            return output.read_text(encoding="utf-8")

    def test_preview_uses_develop_source_link_and_keeps_installation_placeholder(self) -> None:
        notes = self.render()
        self.assertIn("Unpublished release-notes preview", notes)
        self.assertIn("/blob/develop/CONTRIBUTING.md", notes)
        self.assertIn("Mcp@VERSION", notes)
        self.assertIn("/dev/compatibility.html", notes)
        self.assertNotIn("/blob/VERSION/", notes)
        self.assertNotIn("{{", notes)

    def test_release_links_and_installation_use_exact_version(self) -> None:
        for version in ("1.0.0", "1.0.0-rc.1"):
            with self.subTest(version=version):
                notes = self.render(version)
                self.assertIn(f"/blob/{version}/CONTRIBUTING.md", notes)
                self.assertIn(f"Mcp@{version}", notes)
                self.assertIn(f"/{version}/compatibility.html", notes)
                self.assertNotIn("Unpublished release-notes preview", notes)
                self.assertNotIn("VERSION", notes)
                self.assertNotIn("{{", notes)

    def test_invalid_release_identity_is_rejected(self) -> None:
        with self.assertRaises(subprocess.CalledProcessError):
            self.render("develop")


if __name__ == "__main__":
    unittest.main()
