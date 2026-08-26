using Sut = LootOfLima.Server.Games.RoomCodes;

namespace LootOfLima.Server.Tests.Games.RoomCodes;

public class NextTests
{
    [Fact]
    public void Should_use_only_the_unambiguous_alphabet_when_a_code_is_made()
    {
        // Arrange
        // I and O are absent on purpose: the code is read aloud across a table, where they become
        // 1 and 0. A code containing one would be typed wrong by somebody, some evening.
        string[] codes = [.. Enumerable.Range(0, 500).Select(_ => Sut.Next())];

        // Act
        char[] used = [.. codes.SelectMany(code => code).Distinct()];

        // Assert
        Assert.All(codes, code => Assert.Equal(Sut.Length, code.Length));
        Assert.All(used, character => Assert.True(Sut.Alphabet.Contains(character), $"{character} is not in the alphabet."));
        Assert.False(Sut.Alphabet.Contains('I'), "I reads as 1 across a table.");
        Assert.False(Sut.Alphabet.Contains('O'), "O reads as 0 across a table.");
    }

    [Fact]
    public void Should_not_hand_out_the_same_code_twice_in_a_row_when_many_are_drawn()
    {
        // Arrange
        // Not a uniqueness guarantee — the store owns that. This only catches a generator that has
        // stopped varying at all, which is the failure that would make collisions constant.
        int draws = 500;

        // Act
        int distinct = Enumerable.Range(0, draws).Select(_ => Sut.Next()).Distinct(StringComparer.Ordinal).Count();

        // Assert
        Assert.True(distinct > draws * 9 / 10, $"Only {distinct} distinct codes in {draws} draws.");
    }
}
