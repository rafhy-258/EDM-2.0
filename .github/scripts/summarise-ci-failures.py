#!/usr/bin/env python3
"""Publish build and test failures from CI logs as GitHub annotations.

Reads the saved step logs (ci-restore.log, ci-build.log, ci-test.log) and emits one
::error annotation per distinct problem. Messages keep their line breaks (encoded
as %0A) so an xUnit assertion can be read without downloading the log.
"""
import re
import sys
from pathlib import Path

LOGS = ["ci-restore.log", "ci-build.log", "ci-test.log"]
MAX_ANNOTATIONS = 25
MAX_CHARS = 2500


def escape(text: str) -> str:
    return text.replace("%", "%25").replace("\r", "%0D").replace("\n", "%0A")


def emit(title: str, message: str) -> None:
    if len(message) > MAX_CHARS:
        message = message[:MAX_CHARS] + " ...(truncated)"
    print(f"::error title={escape(title)}::{escape(message)}")


def build_problems(lines):
    seen = set()
    for line in lines:
        if re.search(r"\berror (CS|NU|MSB|NETSDK)\d+", line) or re.search(r": error ", line):
            clean = re.sub(r"\s+\[[^\]]+\]\s*$", "", line.strip())
            if clean not in seen:
                seen.add(clean)
                yield clean


def test_failures(lines):
    i = 0
    while i < len(lines):
        match = re.match(r"^\s*Failed (\S+)", lines[i])
        if not match:
            i += 1
            continue
        name = match.group(1)
        block = [lines[i].strip()]
        i += 1
        collecting_message = False
        stack_lines = 0
        while i < len(lines) and not re.match(r"^\s*(Failed|Passed) \S", lines[i]) and not lines[i].startswith("Test Run"):
            text = lines[i].rstrip()
            if text.strip() == "Error Message:":
                collecting_message = True
            elif text.strip() == "Stack Trace:":
                collecting_message = False
                block.append("Stack:")
            elif collecting_message and text.strip():
                block.append(text.strip())
            elif text.strip().startswith("at ") and stack_lines < 3:
                block.append(text.strip())
                stack_lines += 1
            i += 1
        yield name, "\n".join(block)


def main() -> int:
    count = 0
    any_found = False
    for log in LOGS:
        path = Path(log)
        if not path.exists():
            continue
        lines = path.read_text(encoding="utf-8", errors="replace").splitlines()
        if log == "ci-test.log":
            for name, block in test_failures(lines):
                any_found = True
                if count < MAX_ANNOTATIONS:
                    emit(f"test: {name.rsplit('.', 1)[-1]}", block)
                count += 1
        else:
            for problem in build_problems(lines):
                any_found = True
                if count < MAX_ANNOTATIONS:
                    emit(log, problem)
                count += 1
    if not any_found:
        emit("ci", "No error lines matched. See the uploaded backend-ci-output artefact.")
    print(f"Published {min(count, MAX_ANNOTATIONS)} of {count} problems.")
    return 0


if __name__ == "__main__":
    sys.exit(main())
