using System.Text.RegularExpressions;

namespace LootOfLima.ScenarioImporter;

/// <summary>
/// One of the books' 26x50 obfuscation grids: the token lookup chart (page 15, cells like
/// <c>3M</c>) or the answer lookup chart (page 16, single digits). Every book has its own.
/// </summary>
internal sealed partial class LookupChart
{
    public const int Rows = 26;
    public const int Columns = 50;

    private readonly string[] _cells;

    private LookupChart(string[] cells) => _cells = cells;

    public string this[char row, int column]
    {
        get
        {
            if (!char.IsAsciiLetterUpper(row))
            {
                throw new ArgumentOutOfRangeException(nameof(row), row, "Row must be A-Z.");
            }

            if (column is < 1 or > Columns)
            {
                throw new ArgumentOutOfRangeException(nameof(column), column, "Column must be 1-50.");
            }

            return _cells[((row - 'A') * Columns) + (column - 1)];
        }
    }

    /// <param name="text">Raw page text.</param>
    /// <param name="cellWidth">2 for the token chart, 1 for the answer chart.</param>
    public static LookupChart Parse(string text, int cellWidth)
    {
        var expectedLength = Columns * cellWidth;
        var cells = new string[Rows * Columns];
        var seen = new bool[Rows];

        foreach (var line in text.Split('\n'))
        {
            var match = RowPattern().Match(line);
            if (!match.Success)
            {
                continue;
            }

            // Whitespace inside a row is arbitrary: some rows are grouped in fives, some are one
            // continuous run, and rows 'M' and ' I' have irregular leading spacing.
            var payload = WhitespacePattern().Replace(match.Groups[2].Value, string.Empty);
            if (payload.Length != expectedLength)
            {
                // Not a data row (e.g. the "PAGE 15" footer, which also starts with a capital).
                continue;
            }

            var rowIndex = match.Groups[1].Value[0] - 'A';
            if (seen[rowIndex])
            {
                throw new FormatException($"Lookup chart row '{match.Groups[1].Value}' appears twice.");
            }

            seen[rowIndex] = true;
            for (var column = 0; column < Columns; column++)
            {
                cells[(rowIndex * Columns) + column] = payload.Substring(column * cellWidth, cellWidth);
            }
        }

        var missing = Enumerable.Range(0, Rows).Where(i => !seen[i]).Select(i => (char)('A' + i)).ToArray();
        if (missing.Length > 0)
        {
            throw new FormatException($"Lookup chart is missing rows: {string.Join(", ", missing)}.");
        }

        return new LookupChart(cells);
    }

    [GeneratedRegex(@"^\s*([A-Z])\s*(.*)$")]
    private static partial Regex RowPattern();

    [GeneratedRegex(@"\s+")]
    private static partial Regex WhitespacePattern();
}
