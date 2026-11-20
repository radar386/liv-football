#!/bin/sh
# Tests link-up chemistry, coach price scale and termination fee roll against the Unity stubs (mono-mcs needed).
set -e
HERE=$(cd "$(dirname "$0")" && pwd); SRC="$HERE/../../Assets/Scripts"; OUT=$(mktemp -d)
for f in "$SRC"/*.cs; do perl -pe 's/(?<=\d)_(?=\d)//g' "$f" > "$OUT/$(basename "$f")"; done
mcs -out:"$OUT/t.exe" "$HERE/UnityStubs.cs" "$HERE/LinkTermTest.cs" "$OUT"/*.cs 2>&1 | grep -v warning || true
mono "$OUT/t.exe"
