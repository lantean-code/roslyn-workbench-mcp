"""Behavioural coverage for the complete Pages tree and NuGet release selection."""

import gzip
import json
from pathlib import Path
import tempfile
import unittest
from unittest.mock import patch

import website


class WebsiteTests(unittest.TestCase):
    release = {"package": website.PACKAGE, "version": "1.2.0", "sha256": "verified"}

    def source(self, root: Path) -> Path:
        root.mkdir()
        (root / "versions.json").write_text(json.dumps([
            {"version": "dev", "aliases": []},
            {"version": "1.2.0", "aliases": ["latest"]},
            {"version": "1.1.0", "aliases": []},
        ]), encoding="utf-8")
        for version in ("dev", "1.2.0", "1.1.0"):
            folder = root / version
            folder.mkdir()
            (folder / "index.html").write_text(
                '<html><head><link rel="canonical" href="' + website.DEFAULT_URL + version + '/index.html"></head>'
                '<body><h1 id="home">Home</h1><a href="nested/page.html#details">Details</a></body></html>', encoding="utf-8")
            (folder / "nested").mkdir()
            (folder / "nested/page.html").write_text(
                '<h1 id="details">Details</h1><a href="../index.html">Home</a><img src="../image.svg">', encoding="utf-8")
            for name in ("getting-started.html", "code-actions.html"):
                (folder / name).write_text('<h1>Guide</h1><a href="index.html">Home</a>', encoding="utf-8")
            (folder / "image.svg").write_bytes(b"<svg>unchanged</svg>")
            (folder / "catalog.json").write_bytes(b'{"url":"https://example.test/schema"}')
            (folder / "sitemap.xml").write_text(
                '<urlset xmlns="http://www.sitemaps.org/schemas/sitemap/0.9"><url><loc>' + website.DEFAULT_URL + version + '/index.html</loc></url></urlset>', encoding="utf-8")
            (folder / "sitemap.xml.gz").write_bytes(gzip.compress((folder / "sitemap.xml").read_bytes(), mtime=0))
        (root / "latest").mkdir()
        for name in ("sitemap.xml", "sitemap.xml.gz", "image.svg", "catalog.json"):
            (root / "latest" / name).write_bytes((root / "1.2.0" / name).read_bytes())
        (root / "index.html").write_text(website.redirect("latest/"), encoding="utf-8")
        return root

    def test_composed_tree_preserves_assets_and_maps_every_legacy_page(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            source = self.source(root / "source")
            output = root / "output"
            manifest = website.compose(source, output, self.release)
            self.assertEqual("1.2.0", manifest["latestDocs"])
            for version in ("dev", "1.2.0", "1.1.0"):
                for name in ("catalog.json", "image.svg", "sitemap.xml", "sitemap.xml.gz"):
                    self.assertEqual((source / version / name).read_bytes(), (output / version / name).read_bytes())
                    if not name.startswith("sitemap"):
                        self.assertEqual((source / version / name).read_bytes(), (output / "docs" / version / name).read_bytes())
            self.assertIn('../../docs/1.2.0/nested/page.html', (output / "1.2.0/nested/page.html").read_text(encoding='utf-8'))
            self.assertIn('location.search + location.hash', (output / "latest/nested/page.html").read_text(encoding='utf-8'))
            self.assertIn('../../1.2.0/nested/page.html', (output / "docs/latest/nested/page.html").read_text(encoding='utf-8'))
            self.assertIn('docs/1.2.0/index.html', (output / "docs/1.2.0/index.html").read_text(encoding='utf-8'))
            sitemap = (output / "sitemap.xml").read_text(encoding='utf-8')
            self.assertNotIn('/latest/', sitemap)
            self.assertNotIn('/dev/', sitemap)
            self.assertIn('/docs/1.1.0/index.html', sitemap)
            self.assertNotIn('http-equiv="refresh"', (output / "index.html").read_text(encoding='utf-8'))

    def test_composition_is_repeatable_and_preserves_retired_compatibility_assets(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            source = self.source(root / "source")
            first, second = root / "first", root / "second"
            website.compose(source, first, self.release)
            website.compose(first, second, self.release)
            self.assertEqual((first / "index.html").read_bytes(), (second / "index.html").read_bytes())
            for version in ("1.2.0", "1.1.0", "latest"):
                for path in (source / version).rglob("*"):
                    if path.is_file() and path.suffix != ".html":
                        relative = path.relative_to(source)
                        self.assertEqual(path.read_bytes(), (first / relative).read_bytes(), str(relative))
                        self.assertEqual(path.read_bytes(), (second / relative).read_bytes(), str(relative))
            inventory = json.loads((first / "docs/versions.json").read_text(encoding='utf-8'))
            (first / "docs/versions.json").write_text(json.dumps(inventory[:2]))
            third = root / "third"
            website.compose(first, third, self.release)
            self.assertEqual((source / "1.1.0/catalog.json").read_bytes(), (third / "1.1.0/catalog.json").read_bytes())

    def test_public_base_and_fresh_development_are_independent_of_repository_path(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            source = self.source(root / "source")
            output = root / "output"
            website.compose(source, output, self.release, "https://roslyn-mcp.lantean-code.dev/", source / "dev")
            home = (output / "index.html").read_text(encoding='utf-8')
            self.assertIn('href="https://roslyn-mcp.lantean-code.dev/"', home)
            self.assertIn('href="docs/latest/getting-started.html"', home)

    def test_bad_inventory_cannot_escape_or_ambiguously_choose_latest(self):
        for entries in ([{"version": "../outside", "aliases": []}],
                        [{"version": "1.0.0", "aliases": ["../outside"]}],
                        [{"version": "1.0.0", "aliases": ["latest"]}, {"version": "2.0.0", "aliases": ["latest"]}],
                        [{"version": "1.0.0", "aliases": []}, {"version": "1.0.0", "aliases": []}], []):
            with self.subTest(entries=entries), tempfile.TemporaryDirectory() as directory:
                root = Path(directory)
                (root / "versions.json").write_text(json.dumps(entries))
                with self.assertRaises(ValueError):
                    website.read_inventory(root)

    def test_unsafe_public_base_is_rejected(self):
        for base in ("http://example.test/", "https://example.test/?query", "https://example.test/#fragment", "relative/"):
            with self.subTest(base=base), self.assertRaises(ValueError):
                website.public_url(base)

    def test_existing_output_and_unverified_release_are_rejected(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            source = self.source(root / "source")
            with self.assertRaises(ValueError):
                website.compose(source, source, self.release)
            with self.assertRaises(ValueError):
                website.compose(source, root / "out", {"package": "other", "version": "1.2.0"})

    def test_missing_snapshot_fails_before_publication(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            source = self.source(root / "source")
            (source / "dev/index.html").unlink()
            with self.assertRaisesRegex(ValueError, "Missing published"):
                website.compose(source, root / "out", self.release)

    def test_copy_script_uses_the_visible_command_without_a_second_version(self):
        script = (website.REPOSITORY / "docs/website/copy.js").read_text(encoding='utf-8')
        self.assertIn('writeText(command.textContent)', script)
        self.assertNotIn('1.2.0', script)
        self.assertIn('range.selectNodeContents(command)', script)

    def test_version_selector_assets_are_relative_and_not_duplicated_on_republication(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            version = root / "docs/1.2.0"
            nested = version / "reference/tools"
            nested.mkdir(parents=True)
            path = nested / "index.html"
            path.write_text('<html><head></head><body><h1>Tools</h1></body></html>', encoding="utf-8")
            website.add_version_selector_assets(root, version)
            first = path.read_bytes()
            website.add_version_selector_assets(root, version)
            self.assertEqual(first, path.read_bytes())
            text = path.read_text(encoding="utf-8")
            self.assertIn('href="../../../../assets/docs-version.css"', text)
            self.assertIn('src="../../../../assets/docs-version.js"', text)

    def test_new_navigation_becomes_relative_without_rewriting_examples_or_canonicals(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            folder = root / "docs/dev"
            folder.mkdir(parents=True)
            path = folder / "page.html"
            original = website.DEFAULT_URL + "dev/index.html?query=1#home"
            path.write_text(f'<a href="{original}">Home</a><code>{original}</code>'
                            f'<link rel="canonical" href="{original}">', encoding="utf-8")
            website.relativise_navigation(root, folder, {"dev"}, website.DEFAULT_URL)
            updated = path.read_text(encoding="utf-8")
            self.assertIn('<a href="index.html?query=1#home">', updated)
            self.assertIn(f'<code>{original}</code>', updated)
            self.assertIn(f'rel="canonical" href="{original}"', updated)

    def test_retiring_last_production_version_keeps_legacy_alias_usable(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            source = self.source(root / "source")
            first = root / "first"
            website.compose(source, first, self.release)
            (first / "docs/versions.json").write_text('[{"version":"dev","aliases":[]}]', encoding="utf-8")
            output = root / "out"
            website.compose(first, output, self.release)
            self.assertIn('Documentation retired', (output / "latest/index.html").read_text(encoding="utf-8"))
            self.assertIn('docs/dev/getting-started.html', (output / "index.html").read_text(encoding="utf-8"))

    def test_alias_assets_advance_to_the_new_target_without_copying_relocated_bytes(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            source = self.source(root / "source")
            first = root / "first"
            website.compose(source, first, self.release)
            inventory = json.loads((first / "docs/versions.json").read_text(encoding="utf-8"))
            for entry in inventory:
                entry["aliases"] = ["latest"] if entry["version"] == "1.1.0" else []
            (first / "docs/versions.json").write_text(json.dumps(inventory), encoding="utf-8")
            output = root / "out"
            website.compose(first, output, self.release)
            for name in ("sitemap.xml", "sitemap.xml.gz"):
                self.assertEqual((source / "1.1.0" / name).read_bytes(), (output / "latest" / name).read_bytes())
                self.assertEqual((output / "docs/1.1.0" / name).read_bytes(), (output / "docs/latest" / name).read_bytes())

    def metadata(self):
        service = {"resources": [{"@type": "RegistrationsBaseUrl/3.6.0", "@id": "https://example.test/registration/"},
                                  {"@type": "PackageBaseAddress/3.0.0", "@id": "https://example.test/content/"}]}
        leaves = [{"catalogEntry": {"id": website.PACKAGE, "version": version, "listed": listed}}
                  for version, listed in (("1.0.0", True), ("1.2.0", True), ("2.0.0-rc.1", True), ("3.0.0", False))]
        return [json.dumps(service).encode(), json.dumps({"items": [{"@id": "https://example.test/page"}]}).encode(),
                json.dumps({"items": leaves}).encode(), b"PKpackage"]

    def test_nuget_uses_listed_stable_exact_host_and_checks_package_download(self):
        with patch.object(website, "download", side_effect=self.metadata()) as download:
            release = website.resolve_release()
            self.assertEqual("1.2.0", release["version"])
            self.assertTrue(download.call_args.args[0].endswith('/lantean.roslyn.workbench.mcp/1.2.0/lantean.roslyn.workbench.mcp.1.2.0.nupkg'))
            self.assertEqual(64, len(release["sha256"]))

    def test_nuget_propagation_and_bad_content_fail_closed(self):
        with patch.object(website, "download", side_effect=self.metadata()), self.assertRaisesRegex(ValueError, "expected stable"):
            website.resolve_release("1.3.0")
        responses = self.metadata()
        responses[-1] = b"not a package"
        with patch.object(website, "download", side_effect=responses), self.assertRaisesRegex(ValueError, "archive"):
            website.resolve_release()

    def test_nuget_inline_page_and_no_stable_release(self):
        responses = self.metadata()
        page = json.loads(responses.pop(2))
        responses[1] = json.dumps({"items": [page]}).encode()
        with patch.object(website, "download", side_effect=responses):
            self.assertEqual("1.2.0", website.resolve_release()["version"])
        responses = self.metadata()
        responses[2] = b'{"items": []}'
        with patch.object(website, "download", side_effect=responses), self.assertRaisesRegex(ValueError, "no listed stable"):
            website.resolve_release()


if __name__ == "__main__":
    unittest.main()
