using System;
using System.Collections.Generic;
using System.Threading.Tasks;
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

        private void CatalogManager_LastSyncResultUpdated(object sender, CatalogSyncResult e)
        {
            this.View.UpdateCatalogStatus(BuildCatalogStatus(e));
        }

        private void CatalogManager_ProgressChanged(object sender, CatalogSyncProgress e)
        {
            this.View.UpdateCatalogProgress(e);
        }

        private async void View_CheckNowClicked(object sender, EventArgs e)
        {
            await _catalogManager.SyncAsync();
        }

        private CatalogStatus BuildCatalogStatus(CatalogSyncResult result)
        {
            bool hasCache = this._catalogManager.HasCache();
            bool isDownloading = result.State == CatalogSyncState.Downloading;
            var status = new CatalogStatus
            {
                State = result.State,
                NewEmoteNames = GetNewEmoteNames(result.NewEmoteIds),
                ShowProgress = false,
            };

            if (isDownloading)
            {
                status.StatusLine = string.Format(Common.settings_global_sync_downloading, "-", "-");
                status.Feedback = Common.settings_global_sync_downloading;
                status.FeedbackSubtext = hasCache ? null : Common.settings_global_sync_downloading_sub;
                status.FeedbackColor = Color.Gray;
                status.ShowProgress = true;
                return status;
            }

            switch (result.State)
            {
                case CatalogSyncState.UpToDate:
                    if (hasCache)
                    {
                        status.StatusLine = string.Format(Common.settings_global_sync_statusLine, result.Version, FormatLastChecked());
                        status.Feedback = string.Format(Common.settings_global_sync_upToDate_detail, result.Version);
                        status.FeedbackColor = Color.Gray;
                    }
                    else
                    {
                        status.StatusLine = "no catalog yet";
                        status.Feedback = Common.settings_global_sync_noCatalog_detail;
                        status.FeedbackColor = Color.Gray;
                    }
                    break;
                case CatalogSyncState.Updated:
                    status.StatusLine = string.Format(Common.settings_global_sync_statusLine, result.Version, FormatLastChecked());
                    status.Feedback = string.Format(Common.settings_global_sync_updated_detail, result.PreviousVersion, result.Version, result.NewEmoteIds.Count, status.NewEmoteNames);
                    status.FeedbackColor = Color.Green;
                    break;
                case CatalogSyncState.FirstFetch:
                    status.StatusLine = string.Format(Common.settings_global_sync_statusLine, result.Version, FormatLastChecked());
                    status.Feedback = string.Format(Common.settings_global_sync_firstFetch_detail, result.NewEmoteIds.Count);
                    status.FeedbackColor = Color.Green;
                    break;
                case CatalogSyncState.Failed:
                    if (hasCache)
                    {
                        status.StatusLine = $"v{result.Version} · cached · last check failed";
                        status.Feedback = string.Format(Common.settings_global_sync_failed_detail, result.Version);
                        status.FeedbackColor = Color.Red;
                    }
                    else
                    {
                        status.StatusLine = "no catalog yet";
                        status.Feedback = Common.settings_global_sync_firstFail_detail;
                        status.FeedbackSubtext = Common.settings_global_sync_firstFail_sub;
                        status.FeedbackColor = Color.Red;
                    }
                    break;
                case CatalogSyncState.SchemaTooNew:
                    if (hasCache)
                    {
                        status.StatusLine = $"v{result.Version} · cached · catalog too new";
                        status.Feedback = Common.settings_global_sync_schemaTooNew_detail;
                        status.FeedbackSubtext = string.Format(Common.settings_global_sync_schemaTooNew_sub, result.Version);
                        status.FeedbackColor = Color.Orange;
                    }
                    else
                    {
                        status.StatusLine = "no catalog yet";
                        status.Feedback = Common.settings_global_sync_schemaTooNew_detail;
                        status.FeedbackSubtext = Common.settings_global_sync_schemaTooNew_noCache_sub;
                        status.FeedbackColor = Color.Orange;
                    }
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