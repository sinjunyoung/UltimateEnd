using Avalonia.Controls;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UltimateEnd.Models;
using UltimateEnd.Scraper;
using UltimateEnd.Views;
using UltimateEnd.Views.Overlays;
using UltimateEnd.Views.Helpers;

namespace UltimateEnd.Utils
{
    public static class BatchScrapOverlayHelper
    {
        private static LoadingOverlay? FindLoadingOverlay(Control control)
        {
            var mainContentView = control.FindAncestorOfType<MainContentView>();
            return mainContentView?.LoadingOverlay;
        }

        public static void ShowLoading(this Control control, string message = "로딩 중...", CancellationTokenSource? cts = null)
        {
            var overlay = FindLoadingOverlay(control);
            overlay?.Show(message, cts);
        }

        public static void HideLoading(this Control control)
        {
            var overlay = FindLoadingOverlay(control);
            overlay?.Hide(Enums.HiddenState.Silent);
        }

        private static BatchScrapOverlay? FindBatchScrapOverlay(Control control)
        {
            var mainContentView = control.FindAncestorOfType<MainContentView>();
            return mainContentView?.BatchScrapOverlay;
        }

        public static async Task<bool> StartBatchScrapAsync(this Control control, ScreenScraperService service, List<GameMetadata> game)
        {
            var overlay = FindBatchScrapOverlay(control);
            if (overlay == null)
                return false;
            return await overlay.StartBatchScrapAsync(service, game);
        }

        public static bool IsBatchScrapVisible(this Control control)
        {
            var overlay = FindBatchScrapOverlay(control);
            return overlay?.Visible ?? false;
        }

        public static bool IsBatchScrapInProgress(this Control control)
        {
            var overlay = FindBatchScrapOverlay(control);
            return overlay?.IsScrapInProgress ?? false;
        }
    }
}