namespace LootOfLima.Rules;

/// <summary>
/// The only way randomness enters the rules engine. Everything else in
/// <c>LootOfLima.Rules</c> is a pure function of its arguments.
/// </summary>
public interface IShuffleSource
{
    /// <summary>A uniformly distributed integer in <c>[0, exclusiveUpperBound)</c>.</summary>
    int Next(int exclusiveUpperBound);
}

public static class ShuffleSources
{
    /// <summary>Fisher-Yates, in place.</summary>
    public static void Shuffle<T>(this IShuffleSource source, IList<T> items)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(items);

        for (int i = items.Count - 1; i > 0; i--)
        {
            int j = source.Next(i + 1);
            (items[i], items[j]) = (items[j], items[i]);
        }
    }

    /// <summary>Removes and returns one uniformly chosen element.</summary>
    public static T Take<T>(this IShuffleSource source, IList<T> items)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(items);

        if (items.Count == 0)
        {
            throw new ArgumentException("Cannot take from an empty collection.", nameof(items));
        }

        int index = source.Next(items.Count);
        T item = items[index];
        items.RemoveAt(index);
        return item;
    }
}
