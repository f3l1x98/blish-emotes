# 02 — Tracer bullet: first fetch renders emotes

**What to build:** On a fresh module start with an empty cache, the module fetches the catalog from raw GitHub (during development, from the feature branch; in production, `master`), writes the local cache, and the emote list and radial menu render from the cache instead of the embedded data. The embedded load path stays intact as a fallback — the expand half of expand–contract, so nothing user-visible regresses. A second launch runs fully offline from the cache.

Naive full pull is fine here — the cheap conditional sync is 03. Grounding decisions: transport and sync semantics per [Transport choice for catalog delivery](../001-transport-choice-for-catalog-delivery.md) and [Draft catalog schema and repo layout](../002-draft-catalog-schema-and-repo-layout.md).

**Blocked by:** 01 — Generate the catalog.

**Status:** implemented — code-complete, unit/integration-tested; checklist items 1–3 need a runtime pass on Windows (Blish HUD cannot run under Linux/mono)

- [ ] Fresh data directory → launch → catalog fetched in the background and all 64 emotes render and work (list, radial, commands)
- [x] Fetch is non-blocking; the module UI is usable while it runs
- [ ] Second launch with network blocked renders identically from the cache (offline = frozen until next successful fetch)
- [x] Fetched data is parsed and sanity-checked before it replaces what the module renders; a parse failure leaves the module on the fallback
- [x] Embedded load path still works unchanged (expand phase)

## Resolution

Tracer bullet landed on `feature/remote-emotes-catalog` (fetch client, cache, live swap, embedded fallback intact).

**Shape:** a Blish-free catalog layer under `Services/Catalog/` (`CatalogModels` — DTOs + `CatalogData.FromJson`; `CatalogValidator` — the schema invariants of `scripts/validate_catalog.py` incl. the schemaVersion gate; `CatalogNames` — locale → `en` → title-cased id; `CatalogPaths` — path safety; `CatalogHash` — sha256; `CatalogClient` — full-pull HTTP fetch, every file hash-verified; `CatalogCache` — staged write → validate staged → atomic swap, single generation) plus a thin Blish-facing `Services/CatalogManager.cs`. `EmotesModule` loads cache → embedded fallback at startup, then kicks a fire-and-forget `RefreshCatalogAsync`; success applies via build-then-swap on `QueueMainThreadRender` (textures + `ResolveEmoteIds` + settings rebuild + `UpdateUI`, plus a fresh API lock pass). `Emote` gained `alternativeCommands`/`names`/`categories` and a locale-aware `UpdateLabel`; `ModuleSettings.InitEmotes*` became safely re-runnable (get-or-define + rebind handlers exactly once) so keybinds/radial settings survive the swap.

**Fetch URL** is branch-qualified raw GitHub per ticket 001, pointed at `feature/remote-emotes-catalog` during development — `TODO(10-ship-2.0.0)` flips it to `master`.

**Testing:** new `BlishEmotes.Tests/` — a package-free net472 console suite (55 tests) run under mono: name fallback chain, path safety, hashes, validator invariants, real-catalog parsing (manifest hashes re-verified against the committed files), cache staged-swap/offline-freeze behavior, and full-pull fetch + failure modes against a local `HttpListener` serving `catalog/`. Not in `BlishEmotes.sln`, so CI is untouched. Build: `msbuild BlishEmotes.Tests/BlishEmotes.Tests.csproj && mono BlishEmotes.Tests/bin/Debug/BlishEmotes.Tests.exe`.

**Known follow-ups (later tickets):** no ETag/hash-diff conditional sync (03, naive full pull here by design); catalog `categories.json` definitions are parsed but not merged into user categories (04); replaced textures from a re-fetch in one session aren't graveyard-disposed until Unload (05); settings views don't live-refresh their emote lists mid-session (05); fetch UX (status line, retry button, toasts) is 06.

**Pending manual verification on Windows:** fresh-data-dir first launch (fetch → 64 emotes render in list/radial, commands/keybinds work), offline second launch (identical render from cache), parse-failure fallback (e.g. temporarily corrupt the cached `emotes.json`).
