using System.Text.Json;
using System.Text.Json.Serialization;
using LootOfLima.Rules;
using LootOfLima.ScenarioImporter;

var pdfDir = GetOption(args, "--pdf-dir") ?? Path.Combine("docs", "solo and 2-player rules");
var outDir = GetOption(args, "--out-dir") ?? Path.Combine("src", "LootOfLima.Scenarios", "Data");

if (!Directory.Exists(pdfDir))
{
    Console.Error.WriteLine($"PDF directory not found: {pdfDir}");
    return 1;
}

// Diagnostic mode: dump how a single page tokenizes. Invaluable when a book's layout shifts.
if (GetOption(args, "--debug-page") is { } debugPage)
{
    var debugBook = GetOption(args, "--debug-book") ?? "1";
    var debugPath = Path.Combine(pdfDir, $"Loot-of-Lima_book_{debugBook}.pdf");
    var linePrinter = int.Parse(debugPage) == 2;

    foreach (var line in PdfText.ExtractPage(debugPath, int.Parse(debugPage), linePrinter).Split('\n'))
    {
        string summary;
        try
        {
            var tokens = CodeTokenizer.Tokenize(line);
            var codes = tokens.Count(t => !t.IsBareLetter);
            summary = $"{codes,3} codes, {tokens.Count - codes,2} bare";
        }
        catch (FormatException ex)
        {
            summary = $"TOKENIZE FAILED: {ex.Message}";
        }

        var preview = line.Length > 60 ? line[..60] : line;
        Console.WriteLine($"{summary} | {preview}");
    }

    return 0;
}

var layout = BoardLayout.Default;
var scenarios = new List<ScenarioRecord>();
var answerBlob = new List<byte>();
var failures = new List<string>();

for (var book = 1; book <= 20; book++)
{
    var pdfPath = Path.Combine(pdfDir, $"Loot-of-Lima_book_{book}.pdf");
    if (!File.Exists(pdfPath))
    {
        failures.Add($"book {book}: file not found ({pdfPath})");
        continue;
    }

    Console.WriteLine($"Book {book,2}: {Path.GetFileName(pdfPath)}");

    LookupChart tokenChart;
    LookupChart answerChart;
    Dictionary<int, ScenarioTokens> tokensChart;

    try
    {
        tokenChart = LookupChart.Parse(PdfText.ExtractPage(pdfPath, 15), cellWidth: 2);
        answerChart = LookupChart.Parse(PdfText.ExtractPage(pdfPath, 16), cellWidth: 1);
        tokensChart = TokensChart.Parse(PdfText.ExtractPage(pdfPath, 2, linePrinter: true), tokenChart);
    }
    catch (Exception ex) when (ex is FormatException or InvalidOperationException)
    {
        failures.Add($"book {book}: {ex.Message}");
        continue;
    }

    for (var number = 1; number <= 12; number++)
    {
        var id = $"b{book}s{number}";
        try
        {
            var page = number + 2;   // scenario 1 is on page 3
            var table = QuestionTable.Parse(PdfText.ExtractPage(pdfPath, page), answerChart);

            if (!tokensChart.TryGetValue(number, out var printed))
            {
                throw new FormatException("no entry found on the Scenario Tokens Chart.");
            }

            var record = BuildScenario(id, book, number, table, printed, layout);
            scenarios.Add(record);
            answerBlob.AddRange(table.ToCanonicalBytes());
        }
        catch (Exception ex) when (ex is FormatException or InvalidOperationException)
        {
            failures.Add($"{id}: {ex.Message}");
        }
    }
}

Directory.CreateDirectory(outDir);

var document = new ScenarioFile(
    SchemaVersion: 1,
    GeneratedUtc: DateTime.UtcNow.ToString("O"),
    AnswerOrdering: "start x end x terrainFilter(all,beach,forest,mountain) x player(2..5)",
    AnswersPerScenario: 1024,
    Scenarios: scenarios);

var jsonOptions = new JsonSerializerOptions
{
    WriteIndented = true,
    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
};

File.WriteAllText(Path.Combine(outDir, "scenarios.json"), JsonSerializer.Serialize(document, jsonOptions));
File.WriteAllBytes(Path.Combine(outDir, "scenario-answers.bin"), [.. answerBlob]);

Console.WriteLine();
Console.WriteLine($"Verified and emitted {scenarios.Count} of 240 scenarios.");
Console.WriteLine($"  {Path.Combine(outDir, "scenarios.json")}");
Console.WriteLine($"  {Path.Combine(outDir, "scenario-answers.bin")} ({answerBlob.Count} bytes)");

if (failures.Count > 0)
{
    Console.WriteLine();
    Console.Error.WriteLine($"{failures.Count} scenario(s) could NOT be verified and were not emitted:");
    foreach (var failure in failures)
    {
        Console.Error.WriteLine($"  {failure}");
    }
}

return 0;

static ScenarioRecord BuildScenario(
    string id,
    int book,
    int number,
    QuestionTable table,
    ScenarioTokens printed,
    BoardLayout layout)
{
    // P2-P5 come straight out of the published answers; no page-2 parsing involved.
    var hands = new List<LocationId>[5];
    for (var player = QuestionTable.FirstPlayer; player <= QuestionTable.LastPlayer; player++)
    {
        var hand = table.DeriveHand(player, layout);
        if (hand.Count != 4)
        {
            throw new FormatException($"derived hand for P{player} has {hand.Count} tokens, expected 4.");
        }

        hands[player - 1] = hand;

        // Independent cross-check against the printed chart, when we managed to read it.
        var fromChart = printed.Hands[player - 1];
        if (fromChart is not null && !fromChart.Order().SequenceEqual(hand.Order()))
        {
            throw new FormatException(
                $"P{player} printed as [{string.Join(" ", fromChart)}] but the answer table implies "
                + $"[{string.Join(" ", hand.Order())}].");
        }
    }

    // THE ORACLE: every published answer must be reproduced by the engine. This is what proves the
    // board layout, the arc semantics and the decode all agree with the book.
    VerifyAgainstBook(table, hands, layout);

    var p1 = printed.Hands[0]
        ?? throw new FormatException("could not read P1 from the Scenario Tokens Chart.");
    if (p1.Length != 4)
    {
        throw new FormatException($"P1 has {p1.Length} tokens, expected 4.");
    }

    hands[0] = [.. p1];

    var reveal = printed.Reveal
        ?? throw new FormatException("could not read the Reveal row from the Scenario Tokens Chart.");
    if (reveal.Length != 2)
    {
        throw new FormatException($"Reveal has {reveal.Length} tokens, expected 2.");
    }

    var dealt = hands.SelectMany(h => h).Concat(reveal).ToList();
    if (dealt.Distinct().Count() != dealt.Count)
    {
        throw new FormatException("the same location is dealt twice.");
    }

    if (dealt.Count != 22)
    {
        throw new FormatException($"{dealt.Count} locations dealt, expected 22.");
    }

    var treasure = LocationId.All.Except(dealt).ToArray();
    if (treasure.Length != 2)
    {
        throw new FormatException($"{treasure.Length} locations left for the treasure, expected 2.");
    }

    // Independent confirmation: the Solution Chart prints the treasure in its own obfuscated
    // column, derived from nothing we have used so far. If it agrees, every hand and the reveal
    // must be right, because the treasure is exactly what they leave over.
    if (printed.SolutionTreasure is { } solutionTreasure)
    {
        if (!solutionTreasure.Order().SequenceEqual(treasure.Order()))
        {
            throw new FormatException(
                $"Solution Chart gives the treasure as [{string.Join(" ", solutionTreasure.Order())}] "
                + $"but the deal leaves [{string.Join(" ", treasure.Order())}].");
        }
    }
    else
    {
        throw new FormatException("could not read the treasure from the Solution Chart.");
    }

    // The printed "fewest terrain" hint must really be a minimum of that hand.
    for (var seat = 0; seat < 5; seat++)
    {
        if (printed.FewestTerrain[seat] is not { } fewest)
        {
            continue;
        }

        var counts = Terrains.All.ToDictionary(t => t, t => hands[seat].Count(l => l.Terrain == t));
        if (counts[fewest] != counts.Values.Min())
        {
            throw new FormatException(
                $"P{seat + 1} is marked fewest-{fewest} but holds "
                + string.Join(", ", counts.Select(kv => $"{kv.Value} {kv.Key}")) + ".");
        }
    }

    return new ScenarioRecord(
        Id: id,
        Book: book,
        Number: number,
        PlayerCount: 5,
        Hands: [.. hands.Select(h => string.Join(" ", h.Order()))],
        PublicReveal: string.Join(" ", reveal.Order()),
        Treasure: string.Join(" ", treasure.Order()),
        FewestTerrain: string.Join(" ", printed.FewestTerrain.Select(t => t?.ToString()[..1] ?? "?")));
}

static void VerifyAgainstBook(QuestionTable table, List<LocationId>[] hands, BoardLayout layout)
{
    foreach (var start in Directions.All)
    {
        foreach (var end in Directions.All)
        {
            for (var terrainRow = 0; terrainRow < QuestionTable.TerrainRows.Count; terrainRow++)
            {
                var range = new QueryRange(start, end, QuestionTable.TerrainRows[terrainRow]);

                for (var player = QuestionTable.FirstPlayer; player <= QuestionTable.LastPlayer; player++)
                {
                    var expected = table[start, end, terrainRow, player];
                    var actual = Engine.CountInRange(hands[player - 1], range, layout);

                    if (expected != actual)
                    {
                        throw new FormatException(
                            $"oracle mismatch for P{player} {range}: book says {expected}, engine says {actual}.");
                    }
                }
            }
        }
    }
}

static string? GetOption(string[] args, string name)
{
    var index = Array.IndexOf(args, name);
    return index >= 0 && index + 1 < args.Length ? args[index + 1] : null;
}

internal sealed record ScenarioFile(
    int SchemaVersion,
    string GeneratedUtc,
    string AnswerOrdering,
    int AnswersPerScenario,
    IReadOnlyList<ScenarioRecord> Scenarios);

internal sealed record ScenarioRecord(
    string Id,
    int Book,
    int Number,
    int PlayerCount,
    IReadOnlyList<string> Hands,
    string PublicReveal,
    string Treasure,
    string FewestTerrain);
