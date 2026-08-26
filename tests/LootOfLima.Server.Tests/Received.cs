namespace LootOfLima.Server.Tests;

/// <summary>
/// Everything of one kind a client has been sent, in arrival order.
/// </summary>
/// <remarks>
/// <para>
/// This exists as a type of its own because it is the one place in the test harness where two
/// threads meet: SignalR delivers on a dispatch thread while the test thread counts, snapshots and
/// asserts. A bare list cannot be added to while somebody else is reading it — growing one puts a
/// new array behind a count that has already been read — and the failures that causes are torn
/// results and exceptions from inside the assertion, neither of which has anything to say about
/// the server under test.
/// </para>
/// <para>
/// Readers are handed a copy rather than the list itself, so an assertion that enumerates cannot
/// be overtaken halfway through by the next frame arriving.
/// </para>
/// </remarks>
internal sealed class Received<T>
{
    private readonly Lock _gate = new();
    private readonly List<T> _items = [];

    public int Count
    {
        get
        {
            lock (_gate)
            {
                return _items.Count;
            }
        }
    }

    public void Add(T item)
    {
        lock (_gate)
        {
            _items.Add(item);
        }
    }

    /// <summary>A copy of everything so far, safe to enumerate at leisure.</summary>
    public IReadOnlyList<T> Snapshot()
    {
        lock (_gate)
        {
            return [.. _items];
        }
    }

    /// <summary>
    /// The most recent item, or <c>default</c> when nothing has arrived.
    /// </summary>
    /// <remarks>
    /// One read rather than a count followed by an index, which would be two looks at a list that
    /// may have grown in between.
    /// </remarks>
    public T? Last()
    {
        lock (_gate)
        {
            return _items.Count > 0 ? _items[^1] : default;
        }
    }
}
