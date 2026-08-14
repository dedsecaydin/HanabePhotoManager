using System.Collections.Specialized;
using FluentAssertions;
using HanabePhotoManager.App.Collections;
using Xunit;

namespace HanabePhotoManager.App.Tests;

public sealed class RangeObservableCollectionTests
{
    [Fact]
    public void AddRange_RaisesOneResetForTheWholeBatch()
    {
        var collection = new RangeObservableCollection<int>();
        var events = new List<NotifyCollectionChangedEventArgs>();
        collection.CollectionChanged += (_, args) => events.Add(args);

        collection.AddRange([1, 2, 3]);

        collection.Should().Equal(1, 2, 3);
        events.Should().ContainSingle()
            .Which.Action.Should().Be(NotifyCollectionChangedAction.Reset);
    }

    [Fact]
    public void AddRange_WithNoItems_DoesNotRaiseCollectionChanged()
    {
        var collection = new RangeObservableCollection<int>([1]);
        var eventCount = 0;
        collection.CollectionChanged += (_, _) => eventCount++;

        collection.AddRange([]);

        eventCount.Should().Be(0);
        collection.Should().Equal(1);
    }

    [Fact]
    public void ReplaceRange_ReplacesExistingItemsWithOneReset()
    {
        var collection = new RangeObservableCollection<int>([1, 2]);
        var events = new List<NotifyCollectionChangedEventArgs>();
        collection.CollectionChanged += (_, args) => events.Add(args);

        collection.ReplaceRange([3, 4, 5]);

        collection.Should().Equal(3, 4, 5);
        events.Should().ContainSingle()
            .Which.Action.Should().Be(NotifyCollectionChangedAction.Reset);
    }
}
