"""Verify the separately installable skill and its generated web representation."""

import re
import tempfile
import unittest
from pathlib import Path
from zipfile import ZipFile

from skill import SKILL_FILES, publish_skill


class SkillDistributionTests(unittest.TestCase):
    def test_bundle_and_pages_preserve_canonical_bytes_and_relative_references(self):
        root = Path(__file__).resolve().parent.parent
        source = root / "skills/roslyn-workbench"
        self.assertEqual(
            set(SKILL_FILES),
            {path.relative_to(source).as_posix() for path in source.rglob("*") if path.is_file()},
        )
        with tempfile.TemporaryDirectory() as directory:
            content = Path(directory)
            archive_path = publish_skill(root, content)
            with ZipFile(archive_path) as archive:
                self.assertEqual(set(archive.namelist()), {f"roslyn-workbench/{name}" for name in SKILL_FILES})
                archive.extractall(content / "installed")
                for name in SKILL_FILES:
                    expected = (root / "skills/roslyn-workbench" / name).read_bytes()
                    self.assertEqual(archive.read(f"roslyn-workbench/{name}"), expected)
                    self.assertEqual((content / "agent/skill" / name).read_bytes(), expected)
                    self.assertEqual((content / "installed/roslyn-workbench" / name).read_bytes(), expected)
            initial_archive = archive_path.read_bytes()
            publish_skill(root, content)
            self.assertEqual(archive_path.read_bytes(), initial_archive)

    def test_mcp_resources_and_web_guide_cover_all_canonical_markdown(self):
        root = Path(__file__).resolve().parent.parent
        markdown_files = {name for name in SKILL_FILES if name.endswith(".md")}
        publisher = (root / "src/Roslyn.Workbench.Mcp/Hosting/RoslynWorkbenchSkillResources.cs").read_text(encoding="utf-8")
        self.assertEqual(
            set(re.findall(r'UriTemplate = "skill://roslyn-workbench/([^"]+)"', publisher)),
            markdown_files,
        )
        guide = (root / "docs/content/agent/index.md").read_text(encoding="utf-8")
        self.assertEqual(set(re.findall(r'\]\(skill/([^)]+)\)', guide)), markdown_files)

    def test_client_metadata_uses_the_bundled_application_logo(self):
        root = Path(__file__).resolve().parent.parent
        source = root / "skills/roslyn-workbench"
        self.assertEqual((source / "assets/icon.svg").read_bytes(), (root / "assets/roslyn-workbench-mcp-icon.svg").read_bytes())
        metadata = (source / "agents/openai.yaml").read_text(encoding="utf-8")
        icons = re.findall(r"icon_(?:small|large): (.+)", metadata)
        self.assertEqual(len(icons), 2)
        for icon in icons:
            self.assertIn(icon, SKILL_FILES)

    def test_republishing_removes_obsolete_generated_pages(self):
        root = Path(__file__).resolve().parent.parent
        with tempfile.TemporaryDirectory() as directory:
            content = Path(directory)
            obsolete = content / "agent/skill/references/inspection.md"
            obsolete.parent.mkdir(parents=True)
            obsolete.write_text("# Obsolete\n", encoding="utf-8")
            publish_skill(root, content)
            self.assertFalse(obsolete.exists())

    def test_missing_reference_fails_before_writing_an_incomplete_download(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            skill = root / "skills/roslyn-workbench"
            for name in SKILL_FILES:
                path = skill / name
                path.parent.mkdir(parents=True, exist_ok=True)
                path.write_text("# Guidance\n", encoding="utf-8")
            (skill / "SKILL.md").write_text("[missing](references/missing.md)", encoding="utf-8")
            with self.assertRaisesRegex(ValueError, "not included in the bundle"):
                publish_skill(root, root / "content")
            self.assertFalse((root / "content").exists())

    def test_existing_reference_outside_the_manifest_is_rejected(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            skill = root / "skills/roslyn-workbench"
            for name in SKILL_FILES:
                path = skill / name
                path.parent.mkdir(parents=True, exist_ok=True)
                path.write_text("# Guidance\n", encoding="utf-8")
            (skill / "references/extra.md").write_text("# Extra\n", encoding="utf-8")
            (skill / "SKILL.md").write_text("[extra](references/extra.md)", encoding="utf-8")
            with self.assertRaisesRegex(ValueError, "not included in the bundle"):
                publish_skill(root, root / "content")

    def test_reference_escaping_the_skill_is_rejected(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            skill = root / "skills/roslyn-workbench"
            for name in SKILL_FILES:
                path = skill / name
                path.parent.mkdir(parents=True, exist_ok=True)
                path.write_text("# Guidance\n", encoding="utf-8")
            (skill / "SKILL.md").write_text("[outside](../outside.md)", encoding="utf-8")
            with self.assertRaisesRegex(ValueError, "does not resolve inside the bundle"):
                publish_skill(root, root / "content")


if __name__ == "__main__":
    unittest.main()
