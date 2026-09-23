# 04 — V2 migration and merge

**What to build:** An upgrading user's state survives and stays theirs. On the first 2.0 run, `categories.json` migrates V1 → V2: each user category whose name matches a catalog category's en name is stamped with that `catalogId` — "if you touched the name, it's yours" — and the tombstone list (`deletedCatalogCategoryIds`) is added. The migration also places catalog emotes that are *new to this user* (their id appears in no non-Favourites user category) into every linked category matching their placement tags; linked categories are auto-created only when a placed emote is tagged with them, so deleted/renamed 1.x defaults stay absent unless new content needs them. From then on, every applied catalog update places new emotes into every linked category matching their placement tags, auto-creates a linked category for untombstoned catalog categories the user doesn't have, and prunes nothing. Favourites is never merged; user categories remain the only rendered state.

Merge semantics per [Catalog merge into user-persisted categories](../003-catalog-merge-into-user-persisted-categories.md); store invariants per [Inventory user-persisted state keyed by emote ids](../005-inventory-user-persisted-state-keyed-by-emote-ids.md).

**Blocked by:** 02 — Tracer bullet: first fetch renders emotes.

**Status:** implemented — merge/migration semantics unit-tested (89 tests incl. 26 new); the Blish-side glue (CategoriesManager/PersistenceManager/EmotesModule) needs one runtime pass on Windows like tickets 02/03

- [x] 1.3.8 user data migrates: name-matching categories stamped `catalogId`; renamed and hand-created categories stay purely user-owned; new-to-user catalog content is placed in the same pass
- [x] New emote lands in every linked category matching its placement tags — no untouched-vs-modified distinction
- [x] Catalog category with no linked user category → linked category auto-created at the end of the user's order, unique-name constraint respected
- [x] Tombstoned catalog category → emotes tagged with it land uncategorized; the category is never resurrected
- [x] Favourites untouched, nothing pruned, re-running the merge is idempotent
- [x] Downgrade-safe: a 1.x loader ignores the V2 wrapper version and unknown fields

## Resolution

**V2 file format (downgrade-safe by layout):** `{ "version": "V2", "data": [categories…], "deletedCatalogCategoryIds": […], "mergedCatalogVersion": N }` — `data` stays a plain category array (1.x deserializes it directly) and every added field is unknown to the 1.x loader, which ignores the version string and unknown members (unit-tested against a 1.x-shaped reader). Per category, `catalogId` (null = purely user-owned, omitted from JSON). `mergedCatalogVersion` doubles as the migration marker: null = never reconciled with a catalog.

**Blish-free seam (unit-tested):** `Services/Persistence/UserCategoriesModels.cs` (persisted DTOs), `UserCategoriesFile.cs` (V1/V2 read, V2 write) — user-persisted state lives under `Services/Persistence/`, catalog-side under `Services/Catalog/`, mirroring the CONTEXT.md catalog/user-state boundary — and `Services/Catalog/CatalogMerge.cs` (all merge decisions, the one place both worlds meet). The Blish side (`PersistenceManager` owns doc-level state, `CategoriesManager.ApplyCatalog` snapshots → core → apply-back → save once) is thin glue.

**Decision tree in `CatalogMerge.Apply`:** unmigrated state with categories → **migrate** (stamp en-name matches, skip Favourites/already-linked, then place catalog emotes whose id is in no non-Favourites user category into every linked category matching their placement tags; linked categories are auto-created only when a placed emote is tagged with them — deleted/renamed 1.x defaults return only when new content needs them); unmigrated with only Favourites (fresh install post-07, corrupted-file recovery) → **seed** (auto-create every untombstoned catalog category, place every emote); migrated + pending update → **update**; anything else (stale/orphaned/absent pending) → noop. During migration "new" is derived from the user's own `categories.json` (emotes absent from every non-Favourites category), so the migration places correctly even if the sync-side `pending-merge.json` is missing, stale, or garbage; the record is cleared on every non-noop apply so it never lingers. For updates "new" is defined sync-side (new since the previous cache): `CatalogSync` records a `pending-merge.json` sidecar in the data root — written after fetch/parse succeed but *before* the cache swap, so a failed swap leaves an orphan that the merge's version check (`pending.CatalogVersion == catalog version && > merged version`) neutralizes and clears. Update auto-creation is bounded to categories new in the update or tagged by a new emote, always tombstone-checked, appended at the end of the user's order, unique names respected (a conflict skips creation, its emotes land uncategorized).

**Wiring (ticket-03 apply semantics preserved):** sync completion runs the migration on the game thread when the state is still unmigrated ("first 2.0 run" for an online upgrader — stamping changes nothing rendered; the record it supersedes is cleared); update merges apply at the next launch's `LoadAsync`, which also retries a missed migration. Deleting a linked category records its catalog id as a tombstone. Ticket 05 (live apply) reuses the same core with the sync's lists in hand.

**Known follow-ups:** live apply (05); the fresh-install seeding path only becomes the default flow when ticket 07 deletes `SetupDefaultCategories` (until then fresh users get the hardcoded defaults, which the migration then stamps on first fetch — no duplicates); pending-manual-verification on Windows: 1.3.8 upgrade end-to-end, delete-linked-category → tombstone → update with that tag, downgrade to 1.x after migration.

## Comments

- The migration semantics above reflect the resolution of ticket **11 — Migration places content new to the user** (`.scratch/remote-emote-catalog/issues/11-migration-places-new-content.md`).
