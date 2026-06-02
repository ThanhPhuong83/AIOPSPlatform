using System.Collections;

namespace HrmAiOps.Application.Abstractions;

/// <summary>
/// Thread-safe list for the in-memory singleton store.
/// Mutations (Add/AddRange/Remove/Clear) are locked.
/// Enumeration returns a snapshot to prevent "collection modified" errors.
/// </summary>
public sealed class SynchronizedList<T> : IEnumerable<T>
{
    private readonly List<T> _inner = [];
    private readonly object _lock = new();

    public int Count { get { lock (_lock) return _inner.Count; } }

    public void Add(T item) { lock (_lock) _inner.Add(item); }

    public void AddRange(IEnumerable<T> items) { lock (_lock) _inner.AddRange(items); }

    public bool Remove(T item) { lock (_lock) return _inner.Remove(item); }

    public void Clear() { lock (_lock) _inner.Clear(); }

    public int RemoveAll(Predicate<T> match) { lock (_lock) return _inner.RemoveAll(match); }

    public IEnumerator<T> GetEnumerator()
    {
        List<T> snapshot;
        lock (_lock) snapshot = [.._inner];
        return snapshot.GetEnumerator();
    }

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
