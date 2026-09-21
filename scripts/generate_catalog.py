#!/usr/bin/env python3
"""Generate the publishable remote catalog under `catalog/`.

Reads today's embedded sources:
- `ref/json/emotes.json`          (id + primary command)
- `Strings/Emotes*.resx`          (per-locale names)
- `README.md` credits block       (Flaticon attribution per emote)
- `Services/CategoriesManager.cs` hard-coded as `DEFAULT_CATEGORIES` below
- `ref/textures/emotes/*.png`     (source textures, case-drift tolerated)

Output:
- `catalog/manifest.json`
- `catalog/emotes.json`
- `catalog/categories.json`
- `catalog/textures/<id>.png`

Run from the repo root:
    python3 scripts/generate_catalog.py
"""

import hashlib
import json
import os
import re
import shutil
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent
OUT = ROOT / "catalog"

EMOTES_JSON = ROOT / "ref" / "json" / "emotes.json"
LOCALES = {
    "en": ROOT / "Strings" / "Emotes.resx",
    "de": ROOT / "Strings" / "Emotes.de.resx",
    "es": ROOT / "Strings" / "Emotes.es.resx",
    "fr": ROOT / "Strings" / "Emotes.fr.resx",
}
README = ROOT / "README.md"
TEXTURE_SRC = ROOT / "ref" / "textures" / "emotes"

# Mirrors the hard-coded default categories in Services/CategoriesManager.cs.
# Favourites is deliberately absent: it is client-side, never catalog data.
DEFAULT_CATEGORIES = {
    "greeting": {
        "names": {"en": "Greeting"},
        "emotes": ["beckon", "bow", "salute", "wave"],
    },
    "reaction": {
        "names": {"en": "Reaction"},
        "emotes": [
            "cower", "cry", "facepalm", "hiss", "masterpiece", "no", "sad",
            "shiver", "shiverplus", "shocked", "shrug", "surprised", "thanks",
            "thumbsdown", "thumbsup", "yes",
        ],
    },
    "fun": {
        "names": {"en": "Fun"},
        "emotes": ["cheer", "laugh", "paper", "rock", "rockout", "scissors"],
    },
    "pose": {
        "names": {"en": "Pose"},
        "emotes": [
            "bless", "blowkiss", "channel", "crossarms", "heroic", "kneel",
            "magicjuggle", "magictrick", "playdead", "point", "poseheart",
            "posepeace", "posesassy", "poseshy", "serve", "sit", "sleep",
            "stretch", "threaten", "transform", "unleash",
        ],
    },
    "dance": {
        "names": {"en": "Dance"},
        "emotes": [
            "boogie", "breakdance", "crabdance", "dance", "geargrind",
            "happydance", "shuffle", "step",
        ],
    },
    "miscellaneous": {
        "names": {"en": "Miscellaneous"},
        "emotes": [
            "barbecue", "drink", "petalthrow", "ponder", "possessed",
            "rank", "readbook", "sipcoffee", "talk",
        ],
    },
}

# Wiki alias commands (https://wiki.guildwars2.com/wiki/Emote, multi-command rows).
# The existing /command stays primary; these are additional triggers.
ALTERNATIVE_COMMANDS = {
    "barbecue": ["/bbq", "/grill"],
    "boogie": ["/bloodstoneboogie"],
    "facepalm": ["/upset"],
    "scissors": ["/scis"],
    "thanks": ["/thank", "/thx", "/ty"],
}

# README credit keys that drift from the emote id.
CREDIT_KEY_ALIASES = {"bloodstone boogie": "boogie"}

DATA_RE = re.compile(
    r'<data\s+name="([^"]+)"[^>]*>\s*(?:<value>(.*?)</value>|<value\s*/>)',
    re.S,
)
CREDIT_RE = re.compile(
    r'^-\s+\*\*(.+?):\*\*\s+<a\s+href="([^"]+)"[^>]*>(.+?)</a>',
    re.MULTILINE,
)


def parse_resx(path: Path) -> dict:
    """Return a dict of non-empty name -> value from a .resx file."""
    if not path.exists():
        return {}
    text = path.read_text(encoding="utf-8")
    result = {}
    for name, value in DATA_RE.findall(text):
        if value is None:
            continue
        value = value.strip()
        if value:
            result[name] = value
    return result


def parse_credits() -> dict:
    """Return {emote_id: {text, url}} from README.md."""
    text = README.read_text(encoding="utf-8")
    credits = {}
    for key, url, credit_text in CREDIT_RE.findall(text):
        key = key.strip().lower()
        key = CREDIT_KEY_ALIASES.get(key, key)
        credits[key] = {"text": credit_text, "url": url}
    return credits


def sha256_file(path: Path) -> str:
    h = hashlib.sha256()
    with path.open("rb") as f:
        for chunk in iter(lambda: f.read(65536), b""):
            h.update(chunk)
    return h.hexdigest()


def build_category_membership() -> dict:
    """Return {emote_id: [category_id, ...]} from DEFAULT_CATEGORIES."""
    membership = {}
    for category_id, data in DEFAULT_CATEGORIES.items():
        for emote_id in data["emotes"]:
            membership.setdefault(emote_id, []).append(category_id)
    return membership


def find_texture_source(emote_id: str) -> Path:
    """Return the source PNG path for an emote, normalising filename case."""
    exact = TEXTURE_SRC / f"{emote_id}.png"
    if exact.exists():
        return exact
    lower = TEXTURE_SRC / f"{emote_id.lower()}.png"
    if lower.exists():
        return lower
    for candidate in TEXTURE_SRC.iterdir():
        if candidate.is_file() and candidate.stem.lower() == emote_id.lower():
            return candidate
    return exact


def write_json(path: Path, data):
    path.write_text(
        json.dumps(data, indent=2, ensure_ascii=False, sort_keys=True) + "\n",
        encoding="utf-8",
    )


def validate(emotes: list, categories: list) -> list:
    """Return a list of invariant violations in the generated catalog."""
    problems = []
    cat_ids = {c["id"] for c in categories}
    seen_ids = set()
    for emote in emotes:
        eid = emote["id"]
        if eid in seen_ids:
            problems.append(f"duplicate id: {eid}")
        seen_ids.add(eid)

        if not emote["command"].startswith("/"):
            problems.append(f"{eid}: command {emote['command']!r} does not start with '/'")
        for cmd in emote.get("alternativeCommands", []):
            if not cmd.startswith("/"):
                problems.append(f"{eid}: alternative command {cmd!r} does not start with '/'")
            if cmd == emote["command"]:
                problems.append(f"{eid}: alternative command {cmd!r} duplicates primary")

        if "en" not in emote["names"]:
            problems.append(f"{eid}: no 'en' name")

        for category_id in emote["categories"]:
            if category_id not in cat_ids:
                problems.append(f"{eid}: references unknown category {category_id!r}")
        if not emote["categories"]:
            problems.append(f"{eid}: in no category")

    for category in categories:
        cid = category["id"]
        if "en" not in category["names"]:
            problems.append(f"category {cid}: no 'en' name")
        if not any(cid in e["categories"] for e in emotes):
            problems.append(f"category {cid}: empty")

    return problems


def main() -> int:
    if not EMOTES_JSON.exists():
        print(f"Error: {EMOTES_JSON} not found.", file=sys.stderr)
        return 1

    emotes_source = json.loads(EMOTES_JSON.read_text(encoding="utf-8"))
    if not isinstance(emotes_source, list):
        print("Error: emotes.json must be a list.", file=sys.stderr)
        return 1

    names = {loc: parse_resx(path) for loc, path in LOCALES.items()}
    credits = parse_credits()
    membership = build_category_membership()

    OUT.mkdir(parents=True, exist_ok=True)
    (OUT / "textures").mkdir(exist_ok=True)

    entries = []
    missing_credit = []
    missing_texture = []

    for emote in emotes_source:
        eid = emote["id"]
        entry_names = {}
        for loc in LOCALES:
            if eid in names[loc]:
                entry_names[loc] = names[loc][eid]

        entry = {
            "id": eid,
            "command": emote["command"],
            "names": entry_names,
            "categories": membership.get(eid, []),
        }

        if eid in ALTERNATIVE_COMMANDS:
            entry["alternativeCommands"] = ALTERNATIVE_COMMANDS[eid]

        if eid in credits:
            entry["credit"] = credits[eid]
        else:
            missing_credit.append(eid)

        src = find_texture_source(eid)
        dest = OUT / "textures" / f"{eid}.png"
        if src.exists():
            shutil.copy2(src, dest)
        else:
            missing_texture.append(eid)

        entries.append(entry)

    categories = [
        {"id": cid, "names": data["names"]}
        for cid, data in DEFAULT_CATEGORIES.items()
    ]

    write_json(OUT / "emotes.json", entries)
    write_json(OUT / "categories.json", categories)

    files = {
        "emotes.json": sha256_file(OUT / "emotes.json"),
        "categories.json": sha256_file(OUT / "categories.json"),
    }
    for entry in entries:
        texture_path = OUT / "textures" / f"{entry['id']}.png"
        if texture_path.exists():
            files[f"textures/{entry['id']}.png"] = sha256_file(texture_path)

    manifest = {
        "schemaVersion": 1,
        "version": 1,
        "files": files,
    }
    write_json(OUT / "manifest.json", manifest)

    problems = validate(entries, categories)

    print(
        f"Wrote {len(entries)} emotes, {len(categories)} categories, "
        f"{len(files) - 2} textures to {OUT}"
    )

    for loc in LOCALES:
        if loc == "en":
            continue
        untranslated = [e["id"] for e in entries if loc not in e["names"]]
        print(f"untranslated ({loc}): {', '.join(untranslated) or 'none'}")

    print(f"no credit parsed: {', '.join(missing_credit) or 'none'}")
    print(f"no texture found: {', '.join(missing_texture) or 'none'}")

    if missing_credit or missing_texture or problems:
        for problem in problems:
            print(f"validation: {problem}", file=sys.stderr)
        return 1

    return 0


if __name__ == "__main__":
    sys.exit(main())
