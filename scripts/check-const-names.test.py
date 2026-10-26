#!/usr/bin/env python3
"""The const-name check fails on the shape this repo added, and passes on UPPER_SNAKE."""

from __future__ import annotations

import subprocess
import sys
from pathlib import Path

SCRIPT = Path(__file__).with_name("check-const-names.py")

BAD = """\
diff --git a/src/AudienceImport.cs b/src/AudienceImport.cs
--- a/src/AudienceImport.cs
+++ b/src/AudienceImport.cs
@@ -22,0 +22,1 @@
+        public const string OneUnfinishedPerForm = "IX_Imports_Unfinished";
"""

GOOD = """\
diff --git a/src/AudienceImport.cs b/src/AudienceImport.cs
--- a/src/AudienceImport.cs
+++ b/src/AudienceImport.cs
@@ -22,0 +22,1 @@
+        public const string ONE_UNFINISHED_PER_FORM = "IX_Imports_Unfinished";
"""

SKIPPED = """\
diff --git a/src/Model.Designer.cs b/src/Model.Designer.cs
--- a/src/Model.Designer.cs
+++ b/src/Model.Designer.cs
@@ -1,0 +1,1 @@
+        public const string OneUnfinishedPerForm = "IX_Imports_Unfinished";
"""


def run(diff: str) -> int:
    completed = subprocess.run(
        [sys.executable, str(SCRIPT)],
        input=diff,
        text=True,
        capture_output=True,
        check=False,
    )
    return completed.returncode


def main() -> int:
    if run(BAD) == 0:
        print("expected the PascalCase const to fail", file=sys.stderr)
        return 1
    if run(GOOD) != 0:
        print("expected UPPER_SNAKE to pass", file=sys.stderr)
        return 1
    if run(SKIPPED) != 0:
        print("expected a designer file to be skipped", file=sys.stderr)
        return 1
    if run("") != 0:
        print("expected an empty diff to pass", file=sys.stderr)
        return 1
    print("check-const-names: pass")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
