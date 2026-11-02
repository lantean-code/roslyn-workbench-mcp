"""Regression coverage for root-relative documentation URLs after domain migration."""
import tempfile
import unittest
from pathlib import Path
from unittest.mock import MagicMock, patch
import validate


class DomainValidationTests(unittest.TestCase):
    def test_custom_and_github_domain_roots_map_to_the_same_rendered_page(self):
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary).resolve()
            page = root / "index.html"
            page.write_text('<h1 id="home">Home</h1>', encoding="utf-8")
            parsed = validate.PageParser()
            parsed.feed(page.read_text())
            for link, version in (("/docs/index.html#home", None),
                                  ("/docs/1.2.0/index.html#home", "1.2.0"),
                                  ("/roslyn-workbench-mcp/docs/index.html#home", None),
                                  ("/roslyn-workbench-mcp/docs/1.2.0/index.html#home", "1.2.0")):
                with self.subTest(link=link):
                    checked = set()
                    validate.validate_internal_link(root, page, link, version, {page: parsed}, checked)
                    self.assertIn((page, "home"), checked)
            for link in ("/docs/missing.html", "/docs/../outside.html", "/docs/index.html#missing"):
                with self.subTest(link=link), self.assertRaises(ValueError):
                    validate.validate_internal_link(root, page, link, None, {page: parsed}, set())

    def test_owned_custom_domain_is_deferred_but_external_hosts_are_checked(self):
        response = MagicMock()
        response.__enter__.return_value.status = 200
        with patch.object(validate.urllib.request, "urlopen", return_value=response) as request:
            validate.validate_external_links({"https://roslyn-mcp.lantean-code.dev/docs/"}, True)
            request.assert_not_called()
            validate.validate_external_links({"https://roslyn-mcp.lantean-code.dev.evil.test/docs/"}, True)
            self.assertEqual(1, request.call_count)
            validate.validate_external_links({"https://roslyn-mcp.lantean-code.dev/docs/"}, False)
            self.assertEqual(2, request.call_count)


if __name__ == "__main__":
    unittest.main()
