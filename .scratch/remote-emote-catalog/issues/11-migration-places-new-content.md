# 11 — Migration places content new to the user

**What to build:** A 1.x upgrader's first 2.x apply no longer swallows the catalog update. Today migration stamps name-matched categories and places nothing, and the "recover" attempt in the live apply (a second merge call) is provably a no-op — the migration's own version baseline makes the pending update permanently stale, so everything new lands uncategorized until the *next* catalog update. Instead, migration derives "new to *this user*" from the only baseline that exists for an upgrader — their own `categories.json` — and places it in the same pass: catalog emotes that appear in no user category land in every linked category matching their placement tags.

Decisions (agreed with the maintainer, superseding the "migrate: place/create nothing" line in 04's decision tree):

- **Emote filter:** new = catalog emote id in no user category's emote list. **Favourites is excluded from the check** — a favourites-only emote still lands in its tagged category (favouriting never hides an emote from its home category; this also keeps seeding byte-for-byte identical, since a seed-state user has no other categories).
- **Category filter:** a linked category is auto-created only when a filtered-new emote is tagged with it — *not* for every catalog category the user lacks. Renamed/deleted 1.x defaults therefore stay absent unless new content needs them, which is exactly how the post-migration update path already behaves (a later update with a new dance-tagged emote resurrects a renamed "Dance" today). This avoids mass-resurrecting deleted defaults as empty categories.
- **The pending-merge record is not an input to migration.** Migration builds its own synthetic pending (as seeding does) from document vs. catalog; the sync-side record is meaningless on this path anyway — an upgrader's first sync is a first fetch, so it marks the *entire catalog* as new. Migration now places correctly even if the record is absent or garbage.
- **Accepted cost, noted with a small comment in the code:** "in no category" cannot distinguish *new to this user* from *deliberately removed from all categories* — removed emotes get re-placed into their tagged categories. The emote was in the flat list regardless; removing it again is one drag.
- **Untouched invariant:** an emote the user re-categorized stays exactly where the user put it — it counts as "known" (present in a category) and placement is additive-only anyway; nothing is ever moved or pruned.

**Blocked by:** None — can start immediately (04 and 05 are closed; this amends their semantics).

**Status:** closed
**Assignee:** coding-agent

- [x] Upgrader's first apply: catalog emotes in no user category land in every linked category matching their tags; emotes already in any category (incl. moved to a custom one) are untouched
- [x] Renamed default + new emote carrying that tag → linked category auto-created carrying only the new emotes; deleted default with no new tagged content → stays absent (no empty-category resurrection)
- [x] Favourites-only emote lands in its tagged category (Favourites never blocks placement)
- [x] Seeding behavior unchanged (fresh install / corrupted-file recovery place everything, as today)
- [x] Migration places correctly with a missing, stale, or garbage pending-merge record
- [x] Live apply has a single merge call (the dead second call and its TODO removed); the pending-merge record is cleared on every non-noop outcome, so no orphaned record lingers on disk after a migration
- [x] Re-applying is idempotent (noop), and the merge's outcome reporting for migration carries the created-category names and placed-emote count
- [x] Merge unit tests cover the defining scenarios: renamed default with new tagged emote, deleted default without, removed-emote re-placement, favourites-only emote, moved emote, missing pending record, idempotency
- [x] 04's decision tree (and 05's resolution note about keeping the pending update for a second apply) amended to record the new migration semantics

## Resolution

Implemented in `Services/Catalog/CatalogMerge.cs`, `EmotesModule.cs`, and `BlishEmotes.Tests/CatalogMergeTests.cs`:

- `CatalogMerge.Migrate` now derives "new to this user" from the user's own `categories.json`: an emote is new when its id is absent from every non-Favourites category. Favourites is intentionally excluded from the check.
- It builds a synthetic pending update (new emote ids only; empty `NewCategoryIds`) and runs the same placement/auto-creation logic as updates. Linked categories are auto-created only when a placed emote is tagged with them, so deleted/renamed 1.x defaults stay absent unless new content needs them.
- The sync-side `pending-merge.json` is not read by migration; placement is correct whether the record is missing, stale, or garbage.
- A small code comment records the accepted cost: an emote deliberately removed from all non-Favourites categories is indistinguishable from a genuinely new emote and will be re-placed.
- `EmotesModule.LoadAsync` and `ApplyCatalogLive` clear the pending-merge record on every non-noop apply (migration, seed, or update), and the dead second merge call in `ApplyCatalogLive` was removed.
- Added/updated merge unit tests covering: real-catalog upgrader migration, renamed default with new tagged emote, deleted default without new content, favourites-only emote, removed-emote re-placement, moved emote, ignored pending record, and idempotency.

Also fixed a pre-existing test-project compilation drift in `BlishEmotes.Tests/CatalogSyncTests.cs` (it was still passing the old `previousCache` argument to `CatalogSync.RunAsync`). Full test suite: 95 passed.
