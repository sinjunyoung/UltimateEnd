using Avalonia;
using Avalonia.Controls;
using Avalonia.VisualTree;
using System;

namespace UltimateEnd.Views.Helpers
{
    internal static class VisualTreeExtensions
    {
        public static T? FindDescendantByCondition<T>(this Visual visual, Func<T, bool>? predicate = null)
            where T : Visual
        {
            foreach (var child in visual.GetVisualChildren())
            {
                if (child is T typedChild && (predicate == null || predicate(typedChild)))
                    return typedChild;

                var result = FindDescendantByCondition(child, predicate);

                if (result != null)
                    return result;
            }

            return null;
        }

        public static T? FindAncestorOfType<T>(this Control control) where T : class
        {
            var parent = control.Parent;

            while (parent != null)
            {
                if (parent is T target)
                    return target;

                parent = (parent as Control)?.Parent;
            }

            return null;
        }
    }
}