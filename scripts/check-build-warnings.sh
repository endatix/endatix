#!/usr/bin/env bash
# Check build warnings against the allowed set (NU1901-NU1904).
# Exit 0: only allowed warnings. Exit 1: in-scope warnings found. Exit 2: usage or input error.
#
# Handles every MSBuild warning shape:
#   path/file.cs(10,5): warning CS0168: msg [proj.csproj]
#   path/file.cs(10,5,10,20): warning CA1062: msg [proj.csproj]   (ranged)
#   proj.csproj : warning NU1903: msg                             (no line/column)
#   CSC : warning CS8032: msg [proj.csproj]                       (no file)

set -euo pipefail

# Warning lines are those containing ": warning CODE: ".
parse_warnings() {
    grep -E ': warning [A-Za-z]+[0-9]+: ' || true
    return 0
}

# Strip the runner/workspace prefix so lines are stable across machines.
normalize_path() {
    local line="$1"
    line="${line//"$PWD"\//}"
    sed -E 's|/[^ :(]*/endatix/endatix/||g' <<< "$line"
    return 0
}

# Prints "bucket" for one normalized warning line.
classify_warning() {
    local line="$1"
    local rest="${line#*: warning }"
    local code="${rest%%:*}"

    if [[ "$code" =~ ^NU190[1-4]$ ]]; then
        echo "nu190x"
    else
        echo "in_scope"
    fi
    return 0
}

main() {
    local input="${1:-}"

    if [[ -z "$input" ]]; then
        echo "Usage: $0 <build-log-file>" >&2
        echo "  or: dotnet build 2>&1 | $0 -" >&2
        exit 2
    fi

    local raw
    if [[ "$input" == "-" ]]; then
        raw=$(parse_warnings)
    elif [[ -f "$input" ]]; then
        raw=$(parse_warnings < "$input")
    else
        echo "Build log not found: $input" >&2
        exit 2
    fi

    local in_scope=() nu190x=()
    local line bucket
    while IFS= read -r line; do
        [[ -z "$line" ]] && continue
        line=$(normalize_path "$line")
        bucket=$(classify_warning "$line")
        case "$bucket" in
            in_scope) in_scope+=("$line") ;;
            nu190x) nu190x+=("$line") ;;
            *) echo "Unknown bucket: $bucket" >&2; exit 2 ;;
        esac
    done <<< "$raw"

    # Deduplicate (the same warning repeats across multi-targeting and incremental passes).
    if [[ ${#in_scope[@]} -gt 0 ]]; then
        local unique
        unique=$(printf '%s\n' "${in_scope[@]}" | sort -u)
        in_scope=()
        while IFS= read -r line; do in_scope+=("$line"); done <<< "$unique"
    fi

    echo "Build warning summary:"
    echo "  in_scope: ${#in_scope[@]}"
    echo "  nu190x: ${#nu190x[@]}"

    if [[ ${#in_scope[@]} -gt 0 ]]; then
        echo ""
        echo "❌ Found ${#in_scope[@]} in-scope warning(s) that should not exist:"
        echo ""
        printf '%s\n' "${in_scope[@]}"
        exit 1
    fi

    echo ""
    echo "✓ All warnings are in allowed categories (NU190x)."
}

main "$@"
