#!/usr/bin/env python3
"""Lint the published catalog invariants.

Run from the repo root:
    python3 scripts/validate_catalog.py

Exits with a non-zero status if the catalog is malformed, the manifest hashes
are stale, or any catalog texture filename does not match its emote id.
"""

import json
import sys
from pathlib import Path

from generate_catalog import sha256_file, validate

ROOT = Path(__file__).resolve().parent.parent
CATALOG = ROOT / "catalog"


def error(msg: str):
    print(msg, file=sys.stderr)


def main() -> int:
    failures = 0

    if not CATALOG.exists():
        error(f"Error: {CATALOG} does not exist.")
        return 1

    required = ["manifest.json", "emotes.json", "categories.json"]
    for name in required:
        if not (CATALOG / name).exists():
            error(f"Error: missing {name}")
            failures += 1

    if failures:
        return 1

    manifest = json.loads((CATALOG / "manifest.json").read_text(encoding="utf-8"))
    emotes = json.loads((CATALOG / "emotes.json").read_text(encoding="utf-8"))
    categories = json.loads((CATALOG / "categories.json").read_text(encoding="utf-8"))

    if manifest.get("schemaVersion") is None:
        error("manifest.json: missing schemaVersion")
        failures += 1

    expected_files = {
        "emotes.json": sha256_file(CATALOG / "emotes.json"),
        "categories.json": sha256_file(CATALOG / "categories.json"),
    }
    for emote in emotes:
        texture_path = CATALOG / "textures" / f"{emote['id']}.png"
        if texture_path.exists():
            expected_files[f"textures/{emote['id']}.png"] = sha256_file(texture_path)

    actual_files = manifest.get("files", {})
    for path, expected_hash in expected_files.items():
        actual_hash = actual_files.get(path)
        if actual_hash != expected_hash:
            error(
                f"manifest.json: hash mismatch for {path}\n"
                f"  expected: {expected_hash}\n"
                f"  actual:   {actual_hash}"
            )
            failures += 1

    for emote in emotes:
        texture_path = CATALOG / "textures" / f"{emote['id']}.png"
        if not texture_path.exists():
            error(f"{emote['id']}: missing texture textures/{emote['id']}.png")
            failures += 1

    extra_textures = [
        p.name for p in (CATALOG / "textures").iterdir()
        if p.suffix.lower() == ".png" and p.stem not in {e["id"] for e in emotes}
    ]
    for extra in extra_textures:
        error(f"extra texture not referenced by any emote: textures/{extra}")
        failures += 1

    for problem in validate(emotes, categories):
        error(f"schema: {problem}")
        failures += 1

    if failures:
        print(f"\n{failures} catalog validation failure(s).", file=sys.stderr)
        return 1

    print(f"Catalog valid: {len(emotes)} emotes, {len(categories)} categories.")
    return 0


if __name__ == "__main__":
    sys.exit(main())
