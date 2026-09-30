#!/usr/bin/env bash
# Rewrites secrets/<env>.env in the layout of example.env: its banners and notes, and every key
# with the value it has in the env file (empty if the file has none). Keys that example.env does
# not list go last, under "Not in example.env", in their original order. A key that appears twice
# keeps its first value. The old file is kept as secrets/<env>.env.bak, unless nothing changes.
#
#   secrets/tidy.sh <dev|prod>
set -euo pipefail

dir="$(dirname "$0")"
file="$dir/$1.env" template="$dir/example.env"

if [ ! -f "$file" ]; then
    echo "$file is missing. Run: just secrets init $1" >&2
    exit 1
fi

umask 077

# The first pass reads the env file (FNR == NR), the second reads the template. Only the first
# = splits a key from its value, so a value is copied as it is.
awk '
    function is_key(line) { return line ~ /^[A-Za-z_][A-Za-z0-9_]*=/ }
    FNR == NR {
        if (!is_key($0)) next
        i = index($0, "=")
        key = substr($0, 1, i - 1)
        if (key in value) { dup[key] = 1; next }
        value[key] = substr($0, i + 1)
        order[++n] = key
        next
    }
    {
        if (!is_key($0)) { print; next }
        key = substr($0, 1, index($0, "=") - 1)
        if (key in seen) next
        seen[key] = 1
        print key "=" ((key in value) ? value[key] : "")
        if (key in value) kept++
    }
    END {
        extra = 0
        for (j = 1; j <= n; j++) if (!(order[j] in seen)) extra++
        if (extra > 0) {
            print ""
            print "# --- Not in example.env ---"
            for (j = 1; j <= n; j++) if (!(order[j] in seen)) {
                print order[j] "=" value[order[j]]
                printf "moved %s\n", order[j] > "/dev/stderr"
            }
        }
        for (k in dup) printf "duplicate %s\n", k > "/dev/stderr"
        printf "kept %d\n", kept > "/dev/stderr"
    }
' "$file" "$template" > "$file.tmp" 2> "$file.report"
# A file that is already tidy is left alone, and so is its backup.
if cmp -s "$file" "$file.tmp"; then
    rm -f "$file.tmp"
else
    cp -p "$file" "$file.bak"
    chmod 600 "$file.bak"
    mv "$file.tmp" "$file"
fi

moved=$(grep -c '^moved ' "$file.report" || true)
dups=$(grep -c '^duplicate ' "$file.report" || true)
kept=$(sed -n 's/^kept //p' "$file.report")
grep '^duplicate ' "$file.report" | sed 's/^duplicate /warning: duplicate key, kept the first: /' >&2 || true
rm -f "$file.report"
echo "secrets/$1.env: $kept keys kept, $moved moved to \"Not in example.env\", $dups duplicated. Backup: secrets/$1.env.bak"
