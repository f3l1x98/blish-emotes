using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Blish_HUD;
using Blish_HUD.Graphics.UI;
using felix.BlishEmotes.Services;
using felix.BlishEmotes.Services.Catalog;
using felix.BlishEmotes.Strings;
using felix.BlishEmotes.UI.Views;
using Microsoft.Xna.Framework;

namespace felix.BlishEmotes.UI.Presenters
{
    class GlobalSettingsPresenter : Presenter<GlobalSettingsView, (CatalogManager, EmotesManager)>
    {
        private static readonly Logger Logger = Logger.GetLogger<GlobalSettingsPresenter>();
        
        private CatalogManager _catalogManager;
        private EmotesManager _emotesManager;

        public GlobalSettingsPresenter(GlobalSettingsView view, (CatalogManager, EmotesManager) model) : base(view, model)
        {
            _catalogManager = model.Item1;
            _emotesManager = model.Item2;
        }

        protected override Task<bool> Load(IProgress<string> progress)
        {
            this._catalogManager.LastSyncResultUpdated += CatalogManager_LastSyncResultUpdated;
            this._catalogManager.ProgressChanged += CatalogManager_ProgressChanged;
            
            this.View.CheckNowClicked += View_CheckNowClicked;

            if (_catalogManager.LastSyncResult != null)
            {
                this.View.UpdateCatalogStatus(BuildCatalogStatus(_catalogManager.LastSyncResult));
            }

            return base.Load(progress);
        }

        // Both handlers run on the game thread (CatalogManager raises its events through the
        // overlay's synchronization context), so the view may be updated directly.
        private void CatalogManager_LastSyncResultUpdated(object sender, CatalogSyncResult e)
        {
            this.View.UpdateCatalogStatus(BuildCatalogStatus(e));
        }

        private void CatalogManager_ProgressChanged(object sender, CatalogSyncProgress progress)
        {
            bool hasCache = this._catalogManager.HasCache();
            this.View.UpdateCatalogStatus(new CatalogStatus()
            {
                State = CatalogSyncState.Downloading,
                StatusLine = Common.settings_catalog_sync_downloading_statusLine,
                Feedback = Common.settings_catalog_sync_downloading_detail,
                FeedbackSubtext = hasCache ? null : Common.settings_catalog_sync_downloading_sub,
                FeedbackColor = Color.Gray,
                DownloadedFiles = progress.DownloadedFiles,
                TotalFiles = progress.TotalFiles,
                ShowProgress = true,
            });
        }

        private async void View_CheckNowClicked(object sender, EventArgs e)
        {
            try
            {
                await _catalogManager.SyncAsync();
            }
            catch (Exception ex)
            {
                Logger.Error("CheckNow failed - Something went horribly wrong");
                Logger.Error(ex.ToString());
            }
        }

        private CatalogStatus BuildCatalogStatus(CatalogSyncResult result)
        {
            bool hasCache = this._catalogManager.HasCache();
            var status = new CatalogStatus
            {
                State = result.State,
                NewEmoteNames = GetNewEmoteNames(result.NewEmoteIds),
                ShowProgress = false,
            };

            switch (result.State)
            {
                case CatalogSyncState.Offline:
                    status.StatusLine = string.Format(Common.settings_catalog_sync_offline_statusLine, result.Version ?? 0);
                    status.Feedback = Common.settings_catalog_sync_offline_detail;
                    status.FeedbackColor = Color.Gray;
                    break;
                case CatalogSyncState.Downloading:
                    status.StatusLine = string.Format(Common.settings_catalog_sync_downloading_statusLine, result.Version ?? 0);
                    status.Feedback = Common.settings_catalog_sync_downloading_detail;
                    status.FeedbackSubtext = hasCache ? null : Common.settings_catalog_sync_downloading_sub;
                    status.FeedbackColor = Color.Gray;
                    status.ShowProgress = true;
                    break;
                case CatalogSyncState.UpToDate:
                    if (hasCache)
                    {
                        status.StatusLine = string.Format(Common.settings_catalog_sync_statusLine, result.Version, FormatLastChecked());
                        status.Feedback = string.Format(Common.settings_catalog_sync_upToDate_detail, result.Version);
                    }
                    else
                    {
                        status.StatusLine = Common.settings_catalog_sync_noCatalog_statusLine;
                        status.Feedback = Common.settings_catalog_sync_noCatalog_detail;
                    }
                    status.FeedbackColor = Color.Gray;
                    break;
                case CatalogSyncState.Updated:
                    status.StatusLine = string.Format(Common.settings_catalog_sync_statusLine, result.Version, FormatLastChecked());
                    status.Feedback = string.Format(Common.settings_catalog_sync_updated_detail, result.PreviousVersion, result.Version, result.NewEmoteIds.Count, status.NewEmoteNames);
                    status.FeedbackColor = Color.Green;
                    break;
                case CatalogSyncState.FirstFetch:
                    status.StatusLine = string.Format(Common.settings_catalog_sync_statusLine, result.Version ?? 0, FormatLastChecked());
                    status.Feedback = string.Format(Common.settings_catalog_sync_firstFetch_detail, result.NewEmoteIds.Count);
                    status.FeedbackColor = Color.Green;
                    break;
                case CatalogSyncState.Failed:
                    if (hasCache)
                    {
                        status.StatusLine = string.Format(Common.settings_catalog_sync_failed_statusLine, result.Version ?? 0);
                        status.Feedback = string.Format(Common.settings_catalog_sync_failed_detail, result.Version);
                    }
                    else
                    {
                        status.StatusLine = Common.settings_catalog_sync_noCatalog_statusLine;
                        status.Feedback = Common.settings_catalog_sync_firstFail_detail;
                        status.FeedbackSubtext = Common.settings_catalog_sync_firstFail_sub;
                    }
                    status.FeedbackColor = Color.Red;
                    break;
                case CatalogSyncState.SchemaTooNew:
                    if (hasCache)
                    {
                        status.StatusLine = string.Format(Common.settings_catalog_sync_schemaTooNew_statusLine, result.Version);
                        status.Feedback = Common.settings_catalog_sync_schemaTooNew_detail;
                        status.FeedbackSubtext = string.Format(Common.settings_catalog_sync_schemaTooNew_sub, result.Version);
                    }
                    else
                    {
                        status.StatusLine = Common.settings_catalog_sync_noCatalog_statusLine;
                        status.Feedback = Common.settings_catalog_sync_schemaTooNew_detail;
                        status.FeedbackSubtext = Common.settings_catalog_sync_schemaTooNew_sub_noCache;
                    }
                    status.FeedbackColor = Color.Orange;
                    break;
            }

            return status;
        }

        private string GetNewEmoteNames(List<string> ids)
        {
            return string.Join(", ", _emotesManager.GetEmoteNamesByIds(ids));
        }

        private string FormatLastChecked()
        {
            if (this._catalogManager.LastCatalogCheck == DateTime.MinValue)
            {
                return "never";
            }
            var elapsed = DateTime.UtcNow - this._catalogManager.LastCatalogCheck;
            if (elapsed.TotalSeconds < 60)
            {
                return "just now";
            }
            if (elapsed.TotalMinutes < 60)
            {
                return $"{(int)elapsed.TotalMinutes}m ago";
            }
            return $"{(int)elapsed.TotalHours}h ago";
        }

        protected override void Unload()
        {
            this._catalogManager.LastSyncResultUpdated -= CatalogManager_LastSyncResultUpdated;
            this._catalogManager.ProgressChanged -= CatalogManager_ProgressChanged;
            this.View.CheckNowClicked -= View_CheckNowClicked;
        }
    }
}