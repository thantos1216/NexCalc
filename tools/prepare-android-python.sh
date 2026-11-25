#!/usr/bin/env bash
set -euo pipefail

repo_root="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")/.." && pwd)"
destination="$repo_root/src/NexCalc.Android/Assets/python/runtime"
archive="$(mktemp)"
trap 'rm -f "$archive"' EXIT

curl --fail --silent --show-error --location --retry 2 \
  https://registry.npmjs.org/pyodide/-/pyodide-0.27.7.tgz -o "$archive"
printf '%s  %s\n' 89d5d952675eb08e11286e52c7238f1248ce71726e896db9703e73bef23dc343 "$archive" | sha256sum -c -
mkdir -p "$destination"
tar -xzf "$archive" -C "$destination" --strip-components=1 \
  package/pyodide.js package/pyodide.asm.js package/pyodide.asm.wasm \
  package/pyodide-lock.json package/python_stdlib.zip
