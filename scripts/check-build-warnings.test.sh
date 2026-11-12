#!/usr/bin/env bash
# Fixture test for check-build-warnings.sh. Run: ./scripts/check-build-warnings.test.sh
set -uo pipefail
cd "$(dirname "$0")"
tmp=$(mktemp); trap 'rm -f "$tmp"' EXIT
fail=0
expect() { # name expected_exit
    local name="$1" expected="$2" got
    ./check-build-warnings.sh "$tmp" >/dev/null 2>&1; got=$?
    if [[ $got -ne $expected ]]; then echo "FAIL $name: expected $expected got $got"; fail=1; else echo "ok   $name"; fi
}

printf 'Build succeeded.\n' > "$tmp"; expect "no warnings" 0
printf "src/A.cs(1,2): warning CS0618: 'EndatixConfig' is obsolete: 'x' [a.csproj]\n" > "$tmp"; expect "obsolete EndatixConfig" 1
printf 'a.csproj : warning NU1903: Package x has a vulnerability\n' > "$tmp"; expect "NU1903 no location" 0
printf 'src/A.cs(1,2): warning CS0168: unused [a.csproj]\n' > "$tmp"; expect "plain warning" 1
printf 'src/A.cs(1,2,1,9): warning CA1062: validate [a.csproj]\n' > "$tmp"; expect "ranged warning" 1
printf 'CSC : warning CS8032: analyzer failed [a.csproj]\n' > "$tmp"; expect "no-file warning" 1
printf 'a.csproj : warning NETSDK1138: eol [a.csproj]\n' > "$tmp"; expect "location-less non-NU" 1
printf 'src/My Dir/A.cs(1,2): warning CS0168: unused [a.csproj]\n' > "$tmp"; expect "path with space" 1
printf 'src/Endatix.Core/Configuration/EndatixConfig.cs(11,49): warning CS1574: cref [a.csproj]\n' > "$tmp"; expect "CS1574 in EndatixConfig.cs" 1
printf 'src/Endatix.Core/Configuration/EndatixConfig.cs(16,6): warning S1133: deprecated [a.csproj]\n' > "$tmp"; expect "S1133 in EndatixConfig.cs" 1
printf 'src/Other.cs(1,2): warning S1133: deprecated [a.csproj]\n' > "$tmp"; expect "S1133 elsewhere" 1
printf 'src/B.cs(1,2): warning CS0168: msg mentions warning CS0618: x [a.csproj]\n' > "$tmp"; expect "message containing warning text" 1
./check-build-warnings.sh /nonexistent >/dev/null 2>&1; [[ $? -eq 2 ]] && echo "ok   missing file" || { echo "FAIL missing file"; fail=1; }
exit $fail
