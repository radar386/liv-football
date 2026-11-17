#!/bin/sh
# Compiles and tests the pure-C# game logic (packs, catalogs, wages, rating scale) without Unity.
# Needs mono-mcs (apt-get install mono-mcs). mcs misreads digit separators (1_000_000), so they are stripped in a temp copy.
set -e
HERE=$(cd "$(dirname "$0")" && pwd); SRC="$HERE/../../Assets/Scripts"; OUT=$(mktemp -d)
for f in PackEngine PlayerCatalog RatingScale Wages CoachCatalog; do
  perl -pe 's/(?<=\d)_(?=\d)//g' "$SRC/$f.cs" > "$OUT/$f.cs"
done
mcs -out:"$OUT/check.exe" "$HERE/Stubs.cs" "$OUT"/*.cs
mono "$OUT/check.exe"
