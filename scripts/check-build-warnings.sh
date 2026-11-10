#!/usr/bin/env bash
# Check build warnings against allowed set (EndatixConfig + NU190x).
# Exit 0: only allowed warnings. Exit 1: in-scope warnings found. Exit 2: error.

set -euo pipefail

# Parse MSBuild warning lines from stdin (dotnet build 2>&1 output).
# Expected format: path/file.cs(line,col): warning CODE: message [project.csproj]
# Normalize: remove runner path prefixes, trailing [*.csproj], and deduplicate.

parse_warnings() {
    grep -E '^[^ ]+\([0-9]+,[0-9]+\): warning [A-Za-z0-9]+:' || true
}

normalize_path() {
    # Strip common CI runner prefixes like /home/runner/work/endatix/endatix/
    sed 's|^/[^:]*endatix/endatix/||' | sed 's|^/[^:]*endatix/||'
}

normalize_message() {
    # Strip trailing [ [*.csproj] ] from messages
    sed 's/ \[[^]]*\.csproj\]$//'
}

deduplicate() {
    sort -u
}

classify_warning() {
    local line="$1"
    
    # Extract components: file(line,col): warning CODE: message
    local file=$(echo "$line" | sed -n 's/^\([^(]*\)(.*/\1/p')
    local code=$(echo "$line" | sed -n 's/.*warning \([A-Za-z0-9]*\):.*/\1/p')
    local message=$(echo "$line" | sed -n 's/.*warning [A-Za-z0-9]*: \(.*\)/\1/p')
    
    # NU190x bucket
    if [[ "$code" =~ ^NU190[1-4]$ ]]; then
        echo "nu190x"
        return
    fi
    
    # excluded_endatixconfig bucket
    # Rule: (CS0618 AND message starts with 'EndatixConfig' is obsolete:)
    #    OR file matches (^|/)(EndatixConfig|IEndatixConfig)\.cs$
    if [[ "$code" == "CS0618" ]] && [[ "$message" =~ ^\'EndatixConfig\'\ is\ obsolete: ]]; then
        echo "excluded_endatixconfig"
        return
    fi
    
    if [[ "$file" =~ (^|/)(EndatixConfig|IEndatixConfig)\.cs$ ]]; then
        echo "excluded_endatixconfig"
        return
    fi
    
    # Everything else is in_scope
    echo "in_scope"
}

main() {
    local input="${1:-}"
    
    if [[ -z "$input" ]]; then
        echo "Usage: $0 <build-log-file>" >&2
        echo "  or: dotnet build 2>&1 | $0 -" >&2
        exit 2
    fi
    
    local warnings
    if [[ "$input" == "-" ]]; then
        warnings=$(parse_warnings | normalize_path | normalize_message | deduplicate)
    else
        warnings=$(parse_warnings < "$input" | normalize_path | normalize_message | deduplicate)
    fi
    
    if [[ -z "$warnings" ]]; then
        echo "✓ No warnings found."
        exit 0
    fi
    
    # Classify all warnings
    local in_scope=()
    local excluded=()
    local nu190x=()
    
    while IFS= read -r line; do
        local bucket=$(classify_warning "$line")
        case "$bucket" in
            in_scope)
                in_scope+=("$line")
                ;;
            excluded_endatixconfig)
                excluded+=("$line")
                ;;
            nu190x)
                nu190x+=("$line")
                ;;
        esac
    done <<< "$warnings"
    
    local in_scope_count=${#in_scope[@]}
    local excluded_count=${#excluded[@]}
    local nu190x_count=${#nu190x[@]}
    
    echo "Build warning summary:"
    echo "  in_scope: $in_scope_count"
    echo "  excluded_endatixconfig: $excluded_count"
    echo "  nu190x: $nu190x_count"
    
    if [[ $in_scope_count -gt 0 ]]; then
        echo ""
        echo "❌ Found $in_scope_count in-scope warning(s) that should not exist:"
        echo ""
        printf '%s\n' "${in_scope[@]}"
        exit 1
    fi
    
    echo ""
    echo "✓ All warnings are in allowed categories (EndatixConfig or NU190x)."
    exit 0
}

main "$@"
