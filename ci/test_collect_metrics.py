import json
import tempfile
import unittest
from pathlib import Path

from collect_metrics import main


NUNIT = """<?xml version="1.0" encoding="utf-8"?>
<test-run result="Failed" total="3" passed="2" failed="1" skipped="0">
  <test-suite type="Assembly" />
</test-run>
"""

PLAY = """<?xml version="1.0" encoding="utf-8"?>
<test-run result="Passed" total="2" passed="2" failed="0" skipped="0">
</test-run>
"""


class CollectMetricsTests(unittest.TestCase):
    def test_appends_real_counts_and_keeps_previous_runs(self) -> None:
        with tempfile.TemporaryDirectory() as tmp:
            root = Path(tmp)
            results = root / "results"
            (results / "editmode").mkdir(parents=True)
            (results / "playmode").mkdir()
            (results / "editmode" / "editmode-results.xml").write_text(NUNIT, encoding="utf-8")
            (results / "playmode" / "playmode-results.xml").write_text(PLAY, encoding="utf-8")
            (root / "validation.json").write_text(
                json.dumps({"schemaVersion": 1, "errorCount": 0, "warningCount": 2, "issues": []}),
                encoding="utf-8",
            )
            (root / "player.json").write_text(
                json.dumps({"schemaVersion": 1, "result": "Succeeded", "durationSeconds": 12.5, "totalSizeBytes": 10, "outputPath": "build"}),
                encoding="utf-8",
            )
            (root / "test-timing.json").write_text(json.dumps({"durationSeconds": 40}), encoding="utf-8")
            (root / "build-timing.json").write_text(json.dumps({"durationSeconds": 70}), encoding="utf-8")
            history = root / "history.json"
            history.write_text(
                json.dumps({"schemaVersion": 1, "runs": [{"commitSha": "older", "runId": "1"}]}),
                encoding="utf-8",
            )

            code = main(
                [
                    "--history",
                    str(history),
                    "--out-run",
                    str(root / "run.json"),
                    "--test-results-dir",
                    str(results),
                    "--validation",
                    str(root / "validation.json"),
                    "--player-build",
                    str(root / "player.json"),
                    "--test-timing",
                    str(root / "test-timing.json"),
                    "--build-timing",
                    str(root / "build-timing.json"),
                    "--sha",
                    "abc123",
                    "--branch",
                    "main",
                    "--event",
                    "push",
                    "--run-id",
                    "99",
                    "--run-url",
                    "https://example.test/runs/99",
                    "--build-job-result",
                    "success",
                    "--recorded-utc",
                    "2026-11-24T00:00:00Z",
                ]
            )

            self.assertEqual(0, code)
            payload = json.loads(history.read_text(encoding="utf-8"))
            self.assertEqual(["older", "abc123"], [run["commitSha"] for run in payload["runs"]])
            latest = payload["runs"][1]
            self.assertEqual(5, latest["tests"]["total"])
            self.assertEqual(4, latest["tests"]["passed"])
            self.assertEqual(1, latest["tests"]["failed"])
            self.assertEqual(110, latest["pipelineDurationSeconds"])
            self.assertEqual(12.5, latest["playerBuildDurationSeconds"])
            self.assertTrue(latest["validator"]["ran"])
            self.assertEqual(2, latest["validator"]["warnings"])
            self.assertEqual("Succeeded", latest["buildResult"])

    def test_missing_build_is_not_recorded_as_zero_warnings(self) -> None:
        with tempfile.TemporaryDirectory() as tmp:
            root = Path(tmp)
            results = root / "results"
            results.mkdir()
            (results / "editmode-results.xml").write_text(NUNIT, encoding="utf-8")
            history = root / "history.json"
            main(
                [
                    "--history",
                    str(history),
                    "--out-run",
                    str(root / "run.json"),
                    "--test-results-dir",
                    str(results),
                    "--sha",
                    "def456",
                    "--branch",
                    "feature",
                    "--event",
                    "pull_request",
                    "--run-id",
                    "7",
                    "--run-url",
                    "https://example.test/runs/7",
                    "--build-job-result",
                    "skipped",
                    "--recorded-utc",
                    "2026-11-24T01:00:00Z",
                ]
            )
            latest = json.loads(history.read_text(encoding="utf-8"))["runs"][0]
            self.assertFalse(latest["validator"]["ran"])
            self.assertIsNone(latest["validator"]["errors"])
            self.assertIsNone(latest["validator"]["warnings"])
            self.assertIsNone(latest["buildDurationSeconds"])
            self.assertEqual("not-run", latest["buildResult"])
            self.assertEqual(3, latest["tests"]["total"])

    def test_same_run_id_replaces_instead_of_duplicating(self) -> None:
        with tempfile.TemporaryDirectory() as tmp:
            root = Path(tmp)
            results = root / "results"
            results.mkdir()
            (results / "results.xml").write_text(PLAY, encoding="utf-8")
            history = root / "history.json"
            args = [
                "--history",
                str(history),
                "--out-run",
                str(root / "run.json"),
                "--test-results-dir",
                str(results),
                "--sha",
                "abc",
                "--branch",
                "main",
                "--event",
                "push",
                "--run-id",
                "5",
                "--run-url",
                "https://example.test/runs/5",
                "--build-job-result",
                "failure",
                "--recorded-utc",
                "2026-11-24T02:00:00Z",
            ]
            main(args)
            main(args)
            runs = json.loads(history.read_text(encoding="utf-8"))["runs"]
            self.assertEqual(1, len(runs))
            self.assertEqual("Failed", runs[0]["buildResult"])


if __name__ == "__main__":
    unittest.main()
