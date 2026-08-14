using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;

namespace HanabePhotoManager.App.Collections;

public sealed class RangeObservableCollection<T> : ObservableCollection<T>
{
    public RangeObservableCollection()
    {
    }

    public RangeObservableCollection(IEnumerable<T> items)
        : base(items)
    {
    }

    public void AddRange(IEnumerable<T> items)
    {
        ArgumentNullException.ThrowIfNull(items);
        var batch = items as IReadOnlyCollection<T> ?? items.ToArray();
        if (batch.Count == 0)
        {
            return;
        }

        CheckReentrancy();
        foreach (var item in batch)
        {
            Items.Add(item);
        }

        RaiseReset();
    }

    public void ReplaceRange(IEnumerable<T> items)
    {
        ArgumentNullException.ThrowIfNull(items);
        var replacement = items as IReadOnlyCollection<T> ?? items.ToArray();

        CheckReentrancy();
        Items.Clear();
        foreach (var item in replacement)
        {
            Items.Add(item);
        }

        RaiseReset();
    }

    private void RaiseReset()
    {
        OnPropertyChanged(new PropertyChangedEventArgs(nameof(Count)));
        OnPropertyChanged(new PropertyChangedEventArgs("Item[]"));
        OnCollectionChanged(new NotifyCollectionChangedEventArgs(
            NotifyCollectionChangedAction.Reset));
    }
}
