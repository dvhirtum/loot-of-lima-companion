using LootOfLima.Rules;

namespace LootOfLima.ScenarioImporter;

/// <summary>
/// One scenario's published question table (one page, pages 3-14 of a book).
/// Columns are start direction x player asked (2-5); rows are end direction x terrain filter.
/// </summary>
internal sealed class QuestionTable
{
    /// <summary>Terrain sub-rows within each end-direction block, in printed order:
    /// <c>A</c> (all terrain), then Beach, Forest, Mountain.</summary>
    public static readonly IReadOnlyList<Terrain?> TerrainRows =
        [null, Terrain.Beach, Terrain.Forest, Terrain.Mountain];

    public const int FirstPlayer = 2;
    public const int LastPlayer = 5;
    public const int PlayerCount = LastPlayer - FirstPlayer + 1;
    public const int CodesPerLine = Directions.Count * PlayerCount;   // 32

    /// <summary>Indexed [start, end, terrainRow, playerOffset].</summary>
    private readonly byte[] _answers;

    private QuestionTable(byte[] answers) => _answers = answers;

    public byte this[Direction start, Direction end, int terrainRow, int player] =>
        _answers[Offset(start, end, terrainRow, player)];

    public static int Offset(Direction start, Direction end, int terrainRow, int player) =>
        ((((((int)start * Directions.Count) + (int)end) * TerrainRows.Count) + terrainRow) * PlayerCount)
        + (player - FirstPlayer);

    public byte[] ToCanonicalBytes() => (byte[])_answers.Clone();

    /// <param name="pageText">Raw text of the scenario's page.</param>
    /// <param name="answerChart">The book's answer lookup chart (page 16).</param>
    public static QuestionTable Parse(string pageText, LookupChart answerChart)
    {
        var rows = new List<List<CodeToken>>();

        foreach (var line in pageText.Split('\n'))
        {
            List<CodeToken> tokens;
            try
            {
                tokens = CodeTokenizer.Tokenize(PdfText.RemovePageFooter(line));
            }
            catch (FormatException)
            {
                // A line we cannot tokenize is not a data row.
                continue;
            }

            // A data row carries exactly 32 lookup codes. Row and block labels ("A", "B", "F",
            // "M", "NE", ...) tokenize as bare letters and are dropped; header and footer lines
            // never reach 32 codes.
            var codes = tokens.Where(t => !t.IsBareLetter).ToList();
            if (codes.Count == CodesPerLine)
            {
                rows.Add(codes);
            }
        }

        var expectedRows = Directions.Count * TerrainRows.Count;   // 32
        if (rows.Count != expectedRows)
        {
            throw new FormatException($"Expected {expectedRows} question rows, found {rows.Count}.");
        }

        var answers = new byte[Directions.Count * Directions.Count * TerrainRows.Count * PlayerCount];

        for (var row = 0; row < expectedRows; row++)
        {
            var end = (Direction)(row / TerrainRows.Count);
            var terrainRow = row % TerrainRows.Count;

            for (var column = 0; column < CodesPerLine; column++)
            {
                var start = (Direction)(column / PlayerCount);
                var player = FirstPlayer + (column % PlayerCount);

                var code = rows[row][column];
                var decoded = answerChart[code.Letter, code.Number!.Value];
                if (!byte.TryParse(decoded, out var answer))
                {
                    throw new FormatException($"Answer code {code} decoded to '{decoded}', not a digit.");
                }

                answers[Offset(start, end, terrainRow, player)] = answer;
            }
        }

        return new QuestionTable(answers);
    }

    /// <summary>
    /// Reconstructs a player's hand directly from the published answers. A single-sector query
    /// <c>[k, k+1)</c> restricted to one terrain returns 0 or 1, and each sector holds exactly one
    /// location per terrain, so those 24 answers name the hand exactly. This is why P2-P5 need no
    /// parsing of the Scenario Tokens Chart at all.
    /// </summary>
    public List<LocationId> DeriveHand(int player, BoardLayout layout)
    {
        ArgumentNullException.ThrowIfNull(layout);
        var hand = new List<LocationId>();

        for (var terrainRow = 1; terrainRow < TerrainRows.Count; terrainRow++)
        {
            var terrain = TerrainRows[terrainRow]!.Value;

            foreach (var sector in Directions.All)
            {
                var count = this[sector, sector.Next(), terrainRow, player];
                switch (count)
                {
                    case 0:
                        break;
                    case 1:
                        hand.Add(layout.LocationsIn(sector).Single(l => l.Terrain == terrain));
                        break;
                    default:
                        throw new FormatException(
                            $"Player {player} reports {count} {terrain} tokens in the single sector "
                            + $"{sector}, but a sector holds only one location per terrain.");
                }
            }
        }

        return hand;
    }
}
