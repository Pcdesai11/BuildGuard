#!/usr/bin/env python3
"""Join Unity test results, the validator report, and CI timings into one history file.

The script does not invent measurements. Missing inputs stay null and are recorded
as not run, so a failed test job is not stored as a successful build with zero warnings.
"""

from __future__ import annotations

import argparse
import json
import sys
import xml.etree.ElementTree as ET
from datetime import datetime, timezone
from pathlib import Path


SCHEMA_VERSION = 1
MAX_RUNS = 100


def main(argv: list[str] | None = None) -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--history", required=True, type=Path)
    parser.add_argument("--out-run", required=True, type=Path)
    parser.add_argument("--test-results-dir", required=True, type=Path)
    parser.add_argument("--validation", type=Path)
    parser.add_argument("--player-build", type=Path)
    parser.add_argument("--test-timing", type=Path)
    parser.add_argument("--build-timing", type=Path)
    parser.add_argument("--sha", required=True)
    parser.add_argument("--branch", required=True)
    parser.add_argument("--event", required=True)
    parser.add_argument("--run-id", required=True)
    parser.add_argument("--run-url", required=True)
    parser.add_argument("--build-job-result", required=True, choices=("success", "failure", "skipped", "cancelled"))
    parser.add_argument("--recorded-utc", default=None)
    args = parser.parse_args(argv)

    tests, result_files = read_test_results(args.test_results_dir)
    validator = read_validator(args.validation)
    player = read_player_build(args.player_build)
    test_seconds = read_duration(args.test_timing)
    build_seconds = read_duration(args.build_timing)
    pipeline = sum_durations(test_seconds, build_seconds)

    if args.build_job_result == "success" and player is not None:
        build_result = player["result"]
    elif args.build_job_result == "skipped":
        build_result = "not-run"
    elif args.build_job_result == "cancelled":
        build_result = "cancelled"
    else:
        build_result = player["result"] if player is not None else "Failed"

    recorded = args.recorded_utc or datetime.now(timezone.utc).strftime("%Y-%m-%dT%H:%M:%SZ")
    run = {
        "commitSha": args.sha,
        "branch": args.branch,
        "event": args.event,
        "runId": str(args.run_id),
        "runUrl": args.run_url,
        "recordedUtc": recorded,
        "pipelineDurationSeconds": pipeline,
        "testDurationSeconds": test_seconds,
        "buildDurationSeconds": build_seconds,
        "playerBuildDurationSeconds": None if player is None else player["durationSeconds"],
        "tests": tests,
        "testResultFiles": result_files,
        "validator": validator,
        "buildResult": build_result,
    }

    history = load_history(args.history)
    history["runs"] = append_run(history["runs"], run)
    args.history.parent.mkdir(parents=True, exist_ok=True)
    args.out_run.parent.mkdir(parents=True, exist_ok=True)
    write_json(args.history, history)
    write_json(args.out_run, run)
    return 0


def read_test_results(directory: Path) -> tuple[dict, list[str]]:
    totals = {"total": 0, "passed": 0, "failed": 0, "skipped": 0}
    if not directory.is_dir():
        return totals, []

    files = sorted(path for path in directory.rglob("*.xml") if path.is_file())
    preferred = [path for path in files if "result" in path.name.lower()]
    chosen = preferred or files
    used: list[str] = []
    for path in chosen:
        try:
            root = ET.parse(path).getroot()
        except ET.ParseError:
            continue
        if root.tag != "test-run":
            continue
        totals["total"] += int(root.attrib.get("total", "0"))
        totals["passed"] += int(root.attrib.get("passed", "0"))
        totals["failed"] += int(root.attrib.get("failed", "0"))
        totals["skipped"] += int(root.attrib.get("skipped", "0"))
        used.append(path.name)
    return totals, used


def read_validator(path: Path | None) -> dict:
    if path is None or not path.is_file():
        return {"ran": False, "errors": None, "warnings": None}
    payload = json.loads(path.read_text(encoding="utf-8"))
    return {
        "ran": True,
        "errors": int(payload["errorCount"]),
        "warnings": int(payload["warningCount"]),
    }


def read_player_build(path: Path | None) -> dict | None:
    if path is None or not path.is_file():
        return None
    payload = json.loads(path.read_text(encoding="utf-8"))
    return {
        "result": str(payload["result"]),
        "durationSeconds": float(payload["durationSeconds"]),
    }


def read_duration(path: Path | None) -> int | None:
    if path is None or not path.is_file():
        return None
    payload = json.loads(path.read_text(encoding="utf-8"))
    return int(payload["durationSeconds"])


def sum_durations(test_seconds: int | None, build_seconds: int | None) -> int | None:
    if test_seconds is None and build_seconds is None:
        return None
    return (test_seconds or 0) + (build_seconds or 0)


def load_history(path: Path) -> dict:
    if not path.is_file():
        return {"schemaVersion": SCHEMA_VERSION, "runs": []}
    payload = json.loads(path.read_text(encoding="utf-8"))
    version = payload.get("schemaVersion")
    if version != SCHEMA_VERSION:
        raise SystemExit(f"{path} has schemaVersion {version}, expected {SCHEMA_VERSION}")
    runs = payload.get("runs")
    if not isinstance(runs, list):
        raise SystemExit(f"{path} is missing a runs array")
    return {"schemaVersion": SCHEMA_VERSION, "runs": runs}


def append_run(runs: list, run: dict) -> list:
    kept = [
        existing
        for existing in runs
        if not (existing.get("commitSha") == run["commitSha"] and str(existing.get("runId")) == run["runId"])
    ]
    kept.append(run)
    if len(kept) > MAX_RUNS:
        kept = kept[-MAX_RUNS:]
    return kept


def write_json(path: Path, payload: dict) -> None:
    path.write_text(json.dumps(payload, indent=2) + "\n", encoding="utf-8")


if __name__ == "__main__":
    sys.exit(main())
