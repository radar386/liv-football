#!/bin/sh
# Compiles EVERY script in Assets/Scripts against a small stand-in for UnityEngine (UnityStubs.cs).
# Catches typos, missing types and wrong method names, but is NOT a real Unity build: open the project in Unity too.
set -e
HERE=$(cd "$(dirname "$0")" && pwd); SRC="$HERE/../../Assets/Scripts"; OUT=$(mktemp -d)
for f in "$SRC"/*.cs; do perl -pe 's/(?<=\d)_(?=\d)//g' "$f" > "$OUT/$(basename "$f")"; done
mcs -out:"$OUT/all.dll" -target:library "$HERE/UnityStubs.cs" "$OUT"/*.cs && echo "All scripts compile against the stubs."
