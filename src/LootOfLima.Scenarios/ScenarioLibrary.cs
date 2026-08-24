using System.Collections.Frozen;
using System.Reflection;
using System.Text.Json;
using LootOfLima.Rules;

namespace LootOfLima.Scenarios;

/// <summary>
/// The decoded official scenarios plus, for each, the answers exactly as published in the book.
/// </summary>
/// <remarks>
/// The published answers are shipped verbatim so the oracle test checks the engine against the
/// printed book rather than against our own re-derivation of it.
/// </remarks>
public static class ScenarioLibrary
{
    /// <summary>Answers per scenario: 8 starts x 8 ends x 4 terrain filters x 4 players (P2-P5).</summary>
    public const int AnswersPerScenario = 1024;

    public const int FirstAnsweringPlayer = 2;
    public const int LastAnsweringPlayer = 5;

    private static readonly Lazy<LibraryData> Data = new(Load, isThreadSafe: true);

    /// <summary>All scenario ids, in book then scenario order.</summary>
    public static IReadOnlyList<string> Ids => Data.Value.Ids;

    public static IReadOnlyList<Scenario> All => Data.Value.Ordered;

    public static Scenario Get(string id) =>
        Data.Value.ById.TryGetValue(id, out var scenario)
            ? scenario
            : throw new KeyNotFoundException($"No scenario with id '{id}'.");

    /// <summary>
    /// The scenario's published answers, in the canonical ordering described by
    /// <see cref="AnswerIndex"/>.
    /// </summary>
    public static ReadOnlyMemory<byte> PublishedAnswers(string id)
    {
        var position = Data.Value.Position[id];
        return Data.Value.Answers.AsMemory(position * AnswersPerScenario, AnswersPerScenario);
    }

    /// <summary>Index into the published-answer block for one question.</summary>
    /// <param name="terrainRow">0 = all terrain, then Beach, Forest, Mountain.</param>
    /// <param name="player">The player asked, 2-5.</param>
    public static int AnswerIndex(Direction start, Direction end, int terrainRow, int player)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(terrainRow);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(terrainRow, 3);
        ArgumentOutOfRangeException.ThrowIfLessThan(player, FirstAnsweringPlayer);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(player, LastAnsweringPlayer);

        const int players = LastAnsweringPlayer - FirstAnsweringPlayer + 1;
        return ((((((int)start * Directions.Count) + (int)end) * 4) + terrainRow) * players)
            + (player - FirstAnsweringPlayer);
    }

    /// <summary>The terrain filter for each terrain row, matching the books' printed row order.</summary>
    public static IReadOnlyList<Terrain?> TerrainRows { get; } =
        [null, Terrain.Beach, Terrain.Forest, Terrain.Mountain];

    private sealed record LibraryData(
        IReadOnlyList<string> Ids,
        IReadOnlyList<Scenario> Ordered,
        FrozenDictionary<string, Scenario> ById,
        FrozenDictionary<string, int> Position,
        byte[] Answers);

    private static LibraryData Load()
    {
        var assembly = typeof(ScenarioLibrary).GetTypeInfo().Assembly;

        using var jsonStream = assembly.GetManifestResourceStream("LootOfLima.Scenarios.Data.scenarios.json")
            ?? throw new InvalidOperationException("Embedded scenarios.json is missing.");
        using var answerStream = assembly.GetManifestResourceStream("LootOfLima.Scenarios.Data.scenario-answers.bin")
            ?? throw new InvalidOperationException("Embedded scenario-answers.bin is missing.");

        using var buffer = new MemoryStream();
        answerStream.CopyTo(buffer);
        var answers = buffer.ToArray();

        using var document = JsonDocument.Parse(jsonStream);
        var root = document.RootElement;

        var ordered = new List<Scenario>();
        foreach (var element in root.GetProperty("scenarios").EnumerateArray())
        {
            ordered.Add(ReadScenario(element));
        }

        var expected = ordered.Count * AnswersPerScenario;
        if (answers.Length != expected)
        {
            throw new InvalidOperationException(
                $"scenario-answers.bin holds {answers.Length} bytes but {ordered.Count} scenarios "
                + $"need {expected}. The seed files are out of step; re-run the ScenarioImporter.");
        }

        return new LibraryData(
            Ids: [.. ordered.Select(s => s.Id)],
            Ordered: ordered,
            ById: ordered.ToFrozenDictionary(s => s.Id),
            Position: ordered.Select((s, i) => (s.Id, i)).ToFrozenDictionary(x => x.Id, x => x.i),
            Answers: answers);
    }

    private static Scenario ReadScenario(JsonElement element)
    {
        var hands = new List<IReadOnlyList<LocationId>>();
        foreach (var hand in element.GetProperty("hands").EnumerateArray())
        {
            hands.Add(ParseLocations(hand.GetString()));
        }

        var fewest = (element.GetProperty("fewestTerrain").GetString() ?? string.Empty)
            .Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Select(code => code == "?" ? (Terrain?)null : Terrains.FromCode(code[0]))
            .ToArray();

        return new Scenario(
            Id: element.GetProperty("id").GetString() ?? throw new InvalidOperationException("Scenario has no id."),
            Book: element.GetProperty("book").GetInt32(),
            Number: element.GetProperty("number").GetInt32(),
            Hands: hands,
            PublicReveal: ParseLocations(element.GetProperty("publicReveal").GetString()),
            Treasure: ParseLocations(element.GetProperty("treasure").GetString()),
            FewestTerrain: fewest);
    }

    private static LocationId[] ParseLocations(string? text) =>
        [.. (text ?? string.Empty)
            .Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Select(LocationId.Parse)];
}
