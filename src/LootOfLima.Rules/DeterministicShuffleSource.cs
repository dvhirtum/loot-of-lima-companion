namespace LootOfLima.Rules;

/// <summary>
/// A seeded, self-contained SplitMix64 generator. It is deliberately not <c>System.Random</c>:
/// the same seed must produce the same deal on every machine and every runtime version forever,
/// because a game's seed is the only thing stored that can reproduce its deal.
/// </summary>
public sealed class DeterministicShuffleSource : IShuffleSource
{
    private const ulong Gamma = 0x9E3779B97F4A7C15UL;

    private ulong _state;

    public DeterministicShuffleSource(int seed) => _state = (ulong)(uint)seed;

    public int Next(int exclusiveUpperBound)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(exclusiveUpperBound);

        // Rejection sampling: taking the raw value modulo the bound would favour the low end
        // whenever the bound does not divide 2^32.
        uint bound = (uint)exclusiveUpperBound;
        uint threshold = (uint)((0x1_0000_0000UL - bound) % bound);

        uint value;
        do
        {
            value = (uint)(NextUInt64() >> 32);
        }
        while (value < threshold);

        return (int)(value % bound);
    }

    private ulong NextUInt64()
    {
        _state += Gamma;
        ulong z = _state;
        z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
        z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
        return z ^ (z >> 31);
    }
}
