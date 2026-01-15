using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace UltimateEnd.Services
{
    public class RangeObservableCollection<T> : ObservableCollection<T>
    {
        private bool _suppressNotification = false;

        public void AddRange(IEnumerable<T> items)
        {
            if (items == null) return;

            _suppressNotification = true;

            foreach (var item in items) Items.Add(item);

            _suppressNotification = false;

            OnCollectionChanged(new System.Collections.Specialized.NotifyCollectionChangedEventArgs(System.Collections.Specialized.NotifyCollectionChangedAction.Reset));
        }

        protected override void OnCollectionChanged(System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            if (!_suppressNotification) base.OnCollectionChanged(e);
        }
    }
}