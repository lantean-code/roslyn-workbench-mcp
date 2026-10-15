"""Tests for live catalogue capture and the offline lint gate/reporting boundary."""

import contextlib
import io
import json
import tempfile
import unittest
from pathlib import Path
from types import SimpleNamespace
from unittest.mock import AsyncMock, MagicMock, patch

import report
from mcp.types import Tool
from tdqs import lint_server, parse_tool_definitions


def catalogue(description="Returns the available source documents."):
    return {
        "serverName": "test-server",
        "tools": [{"name": "list-documents", "description": description, "inputSchema": {"type": "object"}}],
    }


def lint_report(value):
    parsed = parse_tool_definitions(value)
    return lint_server(server_name=parsed.server_name, tools=parsed.tools)


class CaptureCatalogueTests(unittest.IsolatedAsyncioTestCase):
    async def test_pages_preserve_protocol_fields_and_use_isolated_startup(self):
        first = Tool(
            name="first", description="Returns the first result.",
            inputSchema={"type": "object"}, annotations={"readOnlyHint": True},
        )
        second = Tool(
            name="second", description="Returns the second result.",
            inputSchema={"type": "object"}, outputSchema={"type": "object"},
            _meta={"example": "preserved"},
        )
        client = MagicMock()
        client.server_info.name = "test-server"
        client.list_tools = AsyncMock(side_effect=[
            SimpleNamespace(tools=[first], next_cursor="next"),
            SimpleNamespace(tools=[second], next_cursor=None),
        ])
        context = MagicMock()
        context.__aenter__ = AsyncMock(return_value=client)
        context.__aexit__ = AsyncMock(return_value=False)
        with tempfile.TemporaryDirectory() as temporary, patch.object(report, "Client", return_value=context), patch.object(report, "stdio_client") as transport:
            log = Path(temporary) / "host.log"
            host = Path(temporary) / "Host with spaces.dll"
            value = await report.capture_catalogue(host, "approval-required", "Full", log)
            parameters = transport.call_args.args[0]
            self.assertEqual(str(host), parameters.args[0])
            self.assertIn("approval-required", parameters.args)
            self.assertIn("Full", parameters.args)
            self.assertIn("prompt", parameters.args)
            self.assertNotIn("--enable-plugins", parameters.args)
            self.assertIsNone(parameters.env)
            self.assertFalse(Path(parameters.cwd).exists())
            self.assertTrue(log.exists())
        self.assertEqual("test-server", value["serverName"])
        self.assertEqual(["first", "second"], [tool["name"] for tool in value["tools"]])
        self.assertEqual({"example": "preserved"}, value["tools"][1]["_meta"])
        self.assertEqual({"type": "object"}, value["tools"][1]["outputSchema"])
        self.assertNotIn("outputSchema", value["tools"][0])
        self.assertTrue(value["tools"][0]["annotations"]["readOnlyHint"])

    async def test_repeated_pagination_cursor_fails_instead_of_publishing_partial_catalogue(self):
        client = MagicMock()
        client.list_tools = AsyncMock(return_value=SimpleNamespace(tools=[], next_cursor="same"))
        context = MagicMock()
        context.__aenter__ = AsyncMock(return_value=client)
        context.__aexit__ = AsyncMock(return_value=False)
        with tempfile.TemporaryDirectory() as temporary, patch.object(report, "Client", return_value=context), patch.object(report, "stdio_client"):
            with self.assertRaisesRegex(ValueError, "repeated"):
                await report.capture_catalogue(Path("Host.dll"), "inspection-only", "Omit", Path(temporary) / "host.log")


class OfflineLintTests(unittest.TestCase):
    def test_real_cli_keeps_warnings_advisory_and_writes_both_report_formats(self):
        with tempfile.TemporaryDirectory() as temporary:
            directory = Path(temporary)
            report.write_json(directory / "catalogue.json", catalogue())
            status, result = report.lint_catalogue(directory)
            self.assertEqual(0, status)
            self.assertTrue(any(finding.severity == "warning" for finding in result.findings))
            self.assertTrue((directory / "lint.json").is_file())
            self.assertIn("missing-annotations", (directory / "lint.md").read_text(encoding="utf-8"))

    def test_real_cli_gates_missing_and_tautological_descriptions_but_retains_reports(self):
        for description in ("", "list-documents"):
            with self.subTest(description=description), tempfile.TemporaryDirectory() as temporary:
                directory = Path(temporary)
                report.write_json(directory / "catalogue.json", catalogue(description))
                status, result = report.lint_catalogue(directory)
                self.assertEqual(1, status)
                self.assertTrue(any(finding.severity == "error" for finding in result.findings))
                self.assertTrue((directory / "lint.md").is_file())

    def test_real_cli_invalid_input_is_an_operational_failure(self):
        with tempfile.TemporaryDirectory() as temporary:
            directory = Path(temporary)
            report.write_json(directory / "catalogue.json", {"tools": []})
            with self.assertRaisesRegex(RuntimeError, "TDQS failed \\(exit 2\\)"):
                report.lint_catalogue(directory)


class GenerateReportsTests(unittest.TestCase):
    def run_generation(self, directory, capture, lint):
        host = directory / "Host.dll"
        host.write_bytes(b"built-host")
        with patch.object(report.anyio, "run", side_effect=capture), patch.object(report, "lint_catalogue", side_effect=lint), contextlib.redirect_stdout(io.StringIO()), contextlib.redirect_stderr(io.StringIO()):
            return report.generate_reports(host, directory / "reports")

    def write_lint(self, directory, status=0):
        result = lint_report(catalogue("" if status else "Returns the available source documents."))
        report.write_json(directory / "lint.json", result.model_dump(mode="json"))
        (directory / "lint.md").write_text(report.format_lint_report_markdown(result), encoding="utf-8")
        return status, result

    def test_each_mode_and_schema_gets_its_own_live_catalogue_and_advisory_summary(self):
        def capture(function, host, mode, schema, log):
            value = catalogue()
            value["tools"][0]["description"] = f"Returns the {mode} {schema} catalogue."
            return value

        with tempfile.TemporaryDirectory() as temporary:
            directory = Path(temporary)
            self.assertEqual(0, self.run_generation(directory, capture, self.write_lint))
            output = directory / "reports"
            summary = (output / "summary.md").read_text(encoding="utf-8")
            for mode in report.OPERATIONAL_MODES:
                for schema in report.OUTPUT_SCHEMA_MODES:
                    profile = f"{mode}-{schema.lower()}"
                    captured = json.loads((output / profile / "catalogue.json").read_text(encoding="utf-8"))
                    self.assertIn(f"{mode} {schema}", captured["tools"][0]["description"])
                    self.assertIn(f"| {profile} | 1 | 0 | 1 | 1 |", summary)
            metadata = json.loads((output / "run.json").read_text(encoding="utf-8"))
            self.assertEqual(64, len(metadata["hostSha256"]))
            self.assertEqual("0.1.0", metadata["tdqsVersion"])

    def test_lint_errors_fail_after_the_other_profiles_are_reported(self):
        def lint(directory):
            return self.write_lint(directory, int(directory.name == "transactional-omit"))

        with tempfile.TemporaryDirectory() as temporary:
            directory = Path(temporary)
            self.assertEqual(1, self.run_generation(directory, lambda *args: catalogue(), lint))
            output = directory / "reports"
            self.assertEqual(8, len(list(output.glob("*/lint.json"))))
            self.assertIn("| transactional-omit | 1 | 1 |", (output / "summary.md").read_text(encoding="utf-8"))

    def test_capture_failure_retains_diagnostics_clears_stale_reports_and_continues(self):
        def capture(function, host, mode, schema, log):
            if mode == "inspection-only" and schema == "Omit":
                raise ExceptionGroup("transport", [RuntimeError("Host could not start")])
            return catalogue()

        with tempfile.TemporaryDirectory() as temporary:
            directory = Path(temporary)
            failed = directory / "reports" / "inspection-only-omit"
            failed.mkdir(parents=True)
            for name in ("catalogue.json", "lint.json", "lint.md"):
                (failed / name).write_text("stale", encoding="utf-8")
            self.assertEqual(2, self.run_generation(directory, capture, self.write_lint))
            self.assertEqual("Host could not start\n", (failed / "failure.txt").read_text(encoding="utf-8"))
            self.assertFalse((failed / "catalogue.json").exists())
            self.assertFalse((failed / "lint.json").exists())
            self.assertFalse((failed / "lint.md").exists())
            self.assertEqual(7, len(list((directory / "reports").glob("*/lint.json"))))
            self.assertIn("Capture/lint failed", (directory / "reports" / "summary.md").read_text(encoding="utf-8"))


if __name__ == "__main__":
    unittest.main()
