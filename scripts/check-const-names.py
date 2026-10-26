#!/usr/bin/env python3
"""Fail when a unified diff adds a C# const whose name is not UPPER_SNAKE.

Reads the diff on stdin. Consts already in the tree are not checked.
Generated designer, snapshot, and source-generator files are skipped.

Exit 0 when every added const is UPPER_SNAKE, else 1.
"""

from __future__ import annotations

import re
import sys

NAME = re.compile(r"^[A-Z][A-Z0-9]*(_[A-Z0-9]+)*$")
DECL = re.compile(
    r"^\+\s*(?:\[[^\]]*\]\s*)*"
    r"(?:(?:public|private|protected|internal|static|new|unsafe|readonly|volatile)\s+)*"
    r"const\s+[\w.]+\s+([A-Za-z_][A-Za-z0-9_]*)\b"
)
SKIP = re.compile(r"(?:Designer\.cs|ModelSnapshot\.cs|\.g\.cs)$")


def added_consts(diff: str) -> list[tuple[str, str]]:
    path = ""
    skip = False
    found: list[tuple[str, str]] = []
    for line in diff.splitlines():
        if line.startswith("diff --git "):
            parts = line.split(" b/", 1)
            path = parts[1].strip() if len(parts) == 2 else ""
            skip = bool(SKIP.search(path))
            continue
        if skip or not line.startswith("+") or line.startswith("+++"):
            continue
        match = DECL.match(line)
        if match is not None:
            found.append((path, match.group(1)))
    return found


def main() -> int:
    bad = [(path, name) for path, name in added_consts(sys.stdin.read()) if NAME.match(name) is None]
    if not bad:
        return 0
    print("const names must be UPPER_SNAKE (ONE_UNFINISHED_PER_FORM):", file=sys.stderr)
    for path, name in bad:
        where = f"{path}: " if path else ""
        print(f"{where}{name}", file=sys.stderr)
    return 1


if __name__ == "__main__":
    raise SystemExit(main())
