using Avalonia.Controls;
using Avalonia.VisualTree;
using UltimateEnd.Views.Overlays;

namespace UltimateEnd.Utils
{
    public static class OverlayHelper
    {
        public static bool IsAnyOverlayVisible(Control root)
        {
            if (root == null)
                return false;

            foreach (var child in root.GetVisualDescendants())
            {
                if (child is BaseOverlay overlay && overlay.Visible)
                    return true;
            }

            return false;
        }

        public static int GetVisibleOverlayCount(Control root)
        {
            if (root == null)
                return 0;

            int count = 0;
            foreach (var child in root.GetVisualDescendants())
            {
                if (child is BaseOverlay overlay && overlay.Visible)
                    count++;
            }
            return count;
        }
    }
}