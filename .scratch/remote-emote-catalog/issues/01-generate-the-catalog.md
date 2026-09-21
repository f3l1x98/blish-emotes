# 01 — Generate the catalog

**What to build:** The repo carries the real catalog as pure data — complete, valid, publishable — generated from today's compiled-in emote sources. Nothing fetches it yet; it's the artifact the module will fetch (02) and the lint will guard (08). The prototype's generator already proves the migration is mechanical for all 64 emotes.

Catalog shape (from the schema prototype, branch `prototype/catalog-schema`):

```
manifest.json    { schemaVersion, version, files: { path: sha256 } }
emotes.json      [ { id, command, alternativeCommands?, names{en required}, categories[], credit? } ]
categories.json  [ { id, names } ]   array order = default display order; membership lives in the emotes
textures/<id>.png                   one PNG per emote, filename == id
```

**Blocked by:** None — can start immediately.

**Status:** closed

- [x] `catalog/` contains the manifest, emotes, category definitions, and one texture per emote (filename == id)
- [x] All 64 emotes from today's embedded data present: command, alternative commands, per-locale names (`en` required), placement tags, credit where Flaticon-sourced
- [x] Passes the schema invariants: unique ids, `en` name required, commands start with `/`, category refs valid, filename == id
- [x] Known drift fixed: the thumbs textures' filename case, boogie's credit filed under "bloodstone boogie"
- [x] Generator's long-term home decided (one-off script vs. kept under `scripts/` beside the regen script)

## Resolution

Catalog generated under `catalog/` from today's embedded sources via `scripts/generate_catalog.py`. The generator is kept under `scripts/` (beside the existing `check_emotes.py`) so it can be reused/adapted for the regen script in the contribution-flow/CI work. Validation lives in `scripts/validate_catalog.py` and passes; known drift (thumbs texture case, boogie credit alias) is handled in the generator.
