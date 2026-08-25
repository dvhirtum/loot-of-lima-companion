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
        List<List<CodeToken>> rows = new();

        foreach (string line in pageText.Split('\n'))
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
            List<CodeToken> codes = tokens.Where(t => !t.IsBareLetter).ToList();
            if (codes.Count == CodesPerLine)
            {
                rows.Add(codes);
            }
        }

        int expectedRows = Directions.Count * TerrainRows.Count;   // 32
        if (rows.Count != expectedRows)
        {
            throw new FormatException($"Expected {expectedRows} question rows, found {rows.Count}.");
        }

        byte[] answers = new byte[Directions.Count * Directions.Count * TerrainRows.Count * PlayerCount];

        for (int row = 0; row < expectedRows; row++)
        {
            Direction end = (Direction)(row / TerrainRows.Count);
            int terrainRow = row % TerrainRows.Count;

            for (int column = 0; column < CodesPerLine; column++)
            {
                Direction start = (Direction)(column / PlayerCount);
                int player = FirstPlayer + (column % PlayerCount);

                CodeToken code = rows[row][column];
                string decoded = answerChart[code.Letter, code.Number!.Value];
                if (!byte.TryParse(decoded, out byte answer))
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
        List<LocationId> hand = new();

        for (int terrainRow = 1; terrainRow < TerrainRows.Count; terrainRow++)
        {
            Terrain terrain = TerrainRows[terrainRow]!.Value;

            foreach (Direction sector in Directions.All)
            {
                byte count = this[sector, sector.Next(), terrainRow, player];
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
