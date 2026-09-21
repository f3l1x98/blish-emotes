# 03 — Cheap sync: conditional GET, hash-diff, staged validation

**What to build:** The startup check becomes cheap and safe. One conditional GET on the catalog manifest with the stored ETag: `304` → done, nothing pulled. `200` → schemaVersion gate (too-new → abort the sync, run from cache) → diff the manifest's sha256 file map against the cache index → re-pull only changed files into staging → validate (parse, invariants) → swap into the cache and record the new ETag. Any failure deletes staging and keeps the old cache *and* the old ETag, so a retry genuinely retries instead of 304-ing past a bad version — indistinguishable from a network failure. Boot always loads data that once validated. Applied updates take effect at next launch here; live apply is 05.

**Blocked by:** 02 — Tracer bullet: first fetch renders emotes.

**Status:** closed

- [x] Unchanged catalog → single conditional GET returns 304, zero files pulled
- [x] Changed catalog → only changed files re-pulled (hash-diff), applied on next launch
- [x] Too-new schemaVersion → sync aborted, module runs from cache, outcome surfaced for the UX states (06)
- [x] Corrupted or malformed catalog → staging deleted, old cache and old ETag kept; the next check re-fetches rather than skipping
- [x] Outcome states exposed for the UI: first-fetch / downloading / up-to-date / updated / failed / schema-too-new, with catalog version and new-emote info

## Resolution

Implemented in commit `a3621ec` on `feature/remote-emotes-catalog`.

- Conditional manifest GET with `If-None-Match` is handled by `CatalogClient.GetManifestAsync`.
- Cheap sync orchestration lives in `Services/Catalog/CatalogSync.cs`: schemaVersion gate, hash-diff
  against cached files, re-pull of changed files only, staged validation and swap via
  `CatalogCache.Store`, and persistence of the new ETag in `catalog/etag.txt`.
- `CatalogSyncResult`/`CatalogSyncState` expose the UX outcomes and include version + new emote ids.
- `CatalogManager.SyncAsync` surfaces `LastSyncResult` and no longer applies updates live; they take
  effect on the next launch (live apply remains ticket 05).
- Tests cover 304/zero-pull, hash-diff partial pull, schema-too-new, failed-sync retry safety, and
  new-emote reporting (`BlishEmotes.Tests/CatalogSyncTests.cs`).

**Follow-up — pre-cache PNG decode-test dropped.** `CatalogTextureDecoder` and its wiring were
removed again after review: every fetched file is sha256-verified against the manifest, so transit
corruption is already covered, and the only remaining case — a published-but-broken PNG — is handled
strictly better at load time. `EmotesManager.LoadCatalogTexture` decodes each cached texture inside a
try/catch and falls back to the missing-texture placeholder — a *true* decode-test, where the
structural CRC check could never catch a CRC-consistent but undecodable PNG anyway. Worst case
without the pre-cache gate is one placeholder emote icon that self-heals on the next catalog fix.
Sync tests pin both sides: a hash-mismatched file fails the sync (old cache and ETag kept, retry
re-fetches), and a hash-consistent broken texture lands in the cache for the load-time fallback.
