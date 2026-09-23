---
title: "Emote catalog remote delivery"
labels: [wayfinder:map]
status: closed
assignee: f3l1x
---

## Destination

Emote content — commands, textures, localized names, category defaults — lives as pure data in a `catalog/` directory of this repo. The module fetches it over free GitHub-hosted infrastructure, caches it locally, and runs fully offline from that cache (it just cannot discover *new* emotes while offline). Adding a new emote becomes a data commit; a module release is only ever needed for code changes.

## Notes

- Domain: Blish HUD module (C#, .NET Framework, MonoGame). Today emotes are compiled in via `ref/json/emotes.json` (embedded), `Strings/Emotes.resx` (names), `Content/` PNGs, and `SetupDefaultCategories` in `Services/CategoriesManager.cs`. User categories persist per-user in the module data directory as `categories.json` (version-wrapped via `PersistenceManager`).
- Settled at charting (standing constraints, not tickets):
  - **Fully remote catalog** — no embedded baseline and no seed snapshot shipped with builds; the module serves an online game, so network at module start is a given. Offline behavior: run from local cache, frozen until the next successful fetch.
  - **Same repo, dedicated `catalog/` directory** — not a separate data repo. Repo restructuring is welcome where it helps.
  - **The catalog owns everything**: id, command, texture, per-locale names, default categories, attribution. The `.resx` emote strings and `SetupDefaultCategories` disappear.
  - **Fetch strategy**: cheap versioned-manifest check at module start (background) + a "check for emote updates" button in settings; only new/changed data is re-pulled.
  - **No self-hosted server** — GitHub-native free hosting only.
- Skills: HITL tickets use `/grilling` + `/domain-modeling`; the schema ticket uses `/prototype`; research tickets use `/research`.
- Existing CI context: `.github/workflows/build.yml` (msbuild → `.bhm` artifact), `.github/workflows/check-emotes.yml` + `scripts/check_emotes.py` (scrapes for new wiki emotes, opens issues). Default branch is `dev` (build workflow targets `master` — worth verifying during the release ticket).
- This map is **planning-only** by default: tickets resolve decisions; the refactor coding happens once the way is clear. Execution may be pulled into the map later only by an explicit Notes override from the maintainer.

## Decisions so far

<!-- one line per closed ticket: enough to judge relevance, then zoom the link for the detail -->

- [Transport choice for catalog delivery](001-transport-choice-for-catalog-delivery.md) — raw.githubusercontent.com branch URLs + ETag/`If-None-Match` startup check; retry-later fallback, no second transport; jsDelivr and Pages rejected (staleness/ceremony/ToS).
- [Draft catalog schema and repo layout](002-draft-catalog-schema-and-repo-layout.md) — `catalog/` = manifest (`schemaVersion` + `version` + sha256 `files` map) + `emotes.json` (self-contained entries incl. `alternativeCommands`, `en`-required names with fallback, category tags, `credit`) + `categories.json` (definitions) + `textures/<id>.png`; sync = ETag/304 check → schemaVersion gate → hash-diff selective re-pull; resx names, embedded JSON, emote PNGs and `SetupDefaultCategories` die.
- [Refactor release and migration strategy](004-refactor-release-and-migration-strategy.md) — 2.0.0 (manifest format & `bh.blishhud >=1.0.0` unchanged); catalog served from `master` with build `paths-ignore: catalog/**` so data pushes never start the pipeline; first run = non-blocking fetch state with settings retry; release commit carries code + initial catalog + README rewrite (content per contribution-flow ticket); schemaVersion bumps follow the module release that needs them.
- [Inventory user-persisted state keyed by emote ids](005-inventory-user-persisted-state-keyed-by-emote-ids.md) — three stores keyed by emote id: `categories.json` `emoteIds` (incl. Favourites; unmatched ids silently retained forever), per-emote keybinds (`EmotesShortcutsKeybindsMap_<id>`) and radial toggles (`EmotesRadialEnabledMap_<id>`) in Blish HUD's settings store (rebuild-on-launch; rename = silent data loss in all three); category unique-name constraint + category-name-keyed textures are merge-relevant invariants.
- [Catalog merge into user-persisted categories](003-catalog-merge-into-user-persisted-categories.md) — user categories stay the only rendered state; one-time V2 migration stamps `catalogId` by name-match ("if you touched the name, it's yours"); new emotes land in every linked category per placement tags; new catalog categories auto-create linked ones unless tombstoned (`deletedCatalogCategoryIds`); removal is a non-event (catalog never drops an emote) and ids are immutable, so no settings cleanup ever; merge = stamp → place → auto-create → prune nothing.
- [Apply fetched catalog updates live or on next restart](006-apply-fetched-catalog-updates-live-or-on-next-restart.md) — live apply via build-then-swap (parse/validate staged → game-thread build → merge → swap → fire `CategoriesUpdated`, existing UpdateUI chain redraws); replaced textures go to a graveyard disposed at Unload; per-emote settings maps rebuilt (no duplicate handler subscription) + immediate API lock pass; catalog validated on staged files *before* the disk cache swap (single generation, old ETag kept on failure so retries retry); category settings view subscribes to `CategoriesUpdated`.
- [Texture URL strategy for mainland China reachability](007-texture-url-strategy-for-mainland-china-reachability.md) — raw-only, no fallback transport and no manifest base-URL field (module has no Chinese localization → negligible mainland audience; a manifest field can't rescue a blocked manifest anyway; jsDelivr's own China reachability unverified); live-probed jsDelivr cache facts recorded in the ticket; the split-off China-audience research follow-up was dropped by the maintainer (see Out of scope).
- [Emote contribution flow and catalog automation](008-emote-contribution-flow-and-catalog-automation.md) — checker stays issue-based (schedule re-enabled; supported-set widened to id + command + `alternativeCommands`, killing the false positives that disabled it); schema lint + manifest-freshness (and generated-README-credits freshness) in one `catalog-lint.yml` on PR and push-to-`master` touching `catalog/**`; manifest regen manual via `regen_manifest.py` (auto-bumps `version`) in the same commit as the data change — verify, never auto-fix; thin PR template + canonical README "Add new Emote" flow; README credits section becomes generated from per-emote `credit`.
- [Fetch UX: update control, feedback, first launch](009-fetch-ux-update-control-feedback-first-launch.md) — Variant A: "Emote catalog" group atop the Global tab (status line, Check now, Check on startup); inline colored feedback per outcome + green `ScreenNotification` toast on update; offline/schema also flagged via `[ CATALOG … ]` corner-menu status line; non-blocking first launch (visible-but-empty, placeholders in list/radial, Retry now in settings); progress bar attempted at implementation (spinner + file-count is the settled fallback); asset on branch `prototype/fetch-ux`.
- [Cheap sync: conditional GET, hash-diff, staged swap](issues/03-cheap-sync-etag-hash-diff.md) — conditional manifest GET (`If-None-Match`) → schemaVersion gate → hash-diff re-pull of changed files only → staged parse/invariant validation → swap + ETag store; any failure keeps the old cache *and* the old ETag so a retry genuinely retries; **no pre-cache PNG decode-test** — sha256 verification covers transit corruption, and the load-time decode in `EmotesManager.LoadCatalogTexture` (missing-texture fallback) is the true decode-test a structural CRC check never was.
- [Migration places content new to the user](issues/11-migration-places-new-content.md) — migration derives "new" from the user's own `categories.json` (emotes absent from every non-Favourites category), places them in the same pass, auto-creates linked categories only when a placed emote is tagged with them, and ignores the sync-side pending-merge record; the dead second merge call is removed and the pending record is cleared on every non-noop apply.

## Not yet specified

<!-- see "Fog of war": in-scope fog you can't ticket yet; graduates as the frontier advances -->

## Out of scope

- Any self-hosted server — the premise of this effort.
- Shipping a seed snapshot of the catalog inside module builds — rejected at charting (module serves an online game; network at startup is guaranteed).
- A separate data repository for the catalog — rejected at charting (solo maintainer; same-repo `catalog/` keeps one place to manage).
- Exposing the catalog as a public API for third-party tools — nothing asks for it; revisit only if the destination is redrawn.
- Mainland China reachability and audience reality check — the revisit-trigger research ticket split off from [Texture URL strategy for mainland China reachability](007-texture-url-strategy-for-mainland-china-reachability.md) (does a real mainland Blish HUD audience exist, and is raw blocked there while some alternate host works). Dropped by the maintainer: the raw-only decision stands on its own rationale (no Chinese localization → negligible audience; a manifest field can't rescue a blocked manifest), and any revisit should come from real user reports, not speculative research. The ticket file was never created, so there is nothing to close.
- Pipeline hardening of `build.yml` beyond the catalog `paths-ignore` (e.g. a tag-exists guard against re-pushing an unbumped version) — raised and rejected during [Refactor release and migration strategy](004-refactor-release-and-migration-strategy.md) as unrelated to catalog delivery; do anytime, outside this map.

## Resolution

**Map complete** — all nine decision tickets resolved, fog empty, nothing left to decide before building. The dropped China-audience research ticket is recorded in Out of scope above.

Execution continues in the refactor tickets created from this map's decisions (children of this issue, labelled `refactor`, wired with `blocked-by`): generating the initial `catalog/`, the fetch/cache client, the V2 migration and merge, live apply, the compiled-in content cutover, fetch UX, catalog CI, the README/PR-template rewrite, and shipping 2.0.0.
