# 05 — Check now applies live

**What to build:** A working Check-now control triggers a fetch, and a changed catalog takes effect immediately — no restart. Apply is transactional build-then-swap: staged data is parsed and validated on a background thread, then on the game thread the new emote list is assembled, the merge (04) runs, the emote collection is swapped in place, and the existing `CategoriesUpdated` chain redraws the menu and radial. Replaced textures go to a graveyard disposed only at module unload. Per-emote keybind and radial-toggle settings re-bind to the new list, and one API pass runs so new emotes get correct lock state within seconds.

Apply semantics per [Apply fetched catalog updates live or on next restart](../006-apply-fetched-catalog-updates-live-or-on-next-restart.md) (including the category-settings-view refresh — a manual check while the user sits in settings is the main collision window). The control's final home and look is 06; here it just needs to exist and work.

**Blocked by:** 03 — Cheap sync; 04 — V2 migration and merge.

**Status:** closed

- [x] Check now with a new emote on the branch → it appears in list and radial without restart, in the right categories
- [x] Any failure mid-apply leaves the previous state fully intact — half-updated is impossible by construction
- [x] Replaced textures disposed only at unload — no disposed-texture crashes with settings views or the radial open
- [x] Keybinds and radial toggles re-bind; no duplicate handlers stacked across repeated applies
- [x] New emotes show correct lock state within seconds of applying

## Resolution

Implemented live catalog apply for the manual Check now control.

- Added a Check-now button to `GlobalSettingsView` (`UI/Views/GlobalSettingsView.cs`) with status feedback strings in `Strings/Common.resx`.
- `EmotesModule.CheckNowAsync()` runs the cheap conditional sync in the background, then schedules the apply on the game thread via `GameService.Graphics.QueueMainThreadRender`.
- `EmotesModule.ApplyCatalogLive()` builds the new emote set, runs the catalog merge, then swaps the emote collection in place and fires `CategoriesUpdated` so the existing `UpdateUI` chain rebuilds the list menu and radial.
- `CategoriesManager.ApplyCatalog(...)` now builds and swaps a new category dictionary atomically; failures before the swap leave the previous `categories` untouched.
- `EmotesManager.GraveyardAndUpdateAll(...)` moves replaced emote textures to a graveyard disposed only at module unload, avoiding crashes while settings views or the radial still hold the old `Texture2D` references.
- `ModuleSettings.InitEmotesSettings(...)` rebuilds the per-emote keybind and radial-toggle maps (with existing handler de-duplication) so repeated applies never stack `Activated` handlers; `OnEmotesLoaded` refreshes the hotkey and radial settings views.
- `CategorySettingsPresenter` subscribes to `CategoriesUpdated` so the category settings view refreshes when a live apply mutates categories underneath it.
- After swapping, one fire-and-forget `UpdateEmotesFromApi()` pass updates lock states for new emotes within seconds.
- First-run migration intentionally stamps categories but keeps the pending update, allowing the subsequent live apply (or next launch) to place the new emotes in one go.
- `Strings/Common.resx` / `Common.Designer.cs` gained `settings_global_checkNow`, `settings_global_sync_checking`, `settings_global_sync_upToDate`, `settings_global_sync_updated`, `settings_global_sync_failed`, and `settings_global_sync_schemaTooNew`.
