using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Reactive;
using System.Reactive.Linq;

// ReSharper disable EventUnsubscriptionViaAnonymousDelegate

namespace Minark.Client.Helpers;

/// <summary>
///     Extension methods for ObservableCollection reactive helpers.
/// </summary>
public static class ObservableCollectionExtensions
{
    /// <summary>
    ///     Returns an observable that fires on every CollectionChanged event (and immediately on subscribe).
    /// </summary>
    public static IObservable<Unit> WhenCollectionChanged<T>(this ObservableCollection<T> collection)
    {
        return Observable
            .FromEventPattern<NotifyCollectionChangedEventArgs>(
                h => collection.CollectionChanged += (_, e) => h(collection, e),
                h => collection.CollectionChanged -= (_, e) => h(collection, e))
            .Select(_ => Unit.Default)
            .StartWith(Unit.Default);
    }
}