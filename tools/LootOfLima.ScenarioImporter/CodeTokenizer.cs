namespace LootOfLima.ScenarioImporter;

/// <summary>A lookup code such as <c>N31</c> (row N, column 31), or a bare letter such as the
/// terrain marker <c>F</c>.</summary>
/// <param name="Letter">The row letter.</param>
/// <param name="Number">The column number, or null for a bare letter.</param>
/// <param name="Column">Character offset in the source line, used to map blocks to table columns.</param>
internal readonly record struct CodeToken(char Letter, int? Number, int Column)
{
    public bool IsBareLetter => Number is null;

    public override string ToString() => Number is { } n ? $"{Letter}{n}" : Letter.ToString();
}

internal static class CodeTokenizer
{
    /// <summary>
    /// Tokenizes a line into codes. Whitespace is insignificant because the PDFs regularly split
    /// codes (<c>W 26</c>) and run them together (<c>Y42Y21</c>). A code is an uppercase letter
    /// followed by a maximal run of digits; because codes always alternate letter/digits, that run
    /// is unambiguous and must be 1-2 digits long.
    /// </summary>
    public static List<CodeToken> Tokenize(string line)
    {
        List<CodeToken> tokens = new();

        for (int i = 0; i < line.Length; i++)
        {
            if (!char.IsAsciiLetterUpper(line[i]))
            {
                continue;
            }

            int column = i;
            char letter = line[i];

            // Skip whitespace between the letter and its digits.
            int j = i + 1;
            while (j < line.Length && line[j] == ' ')
            {
                j++;
            }

            int digitStart = j;
            while (j < line.Length && char.IsAsciiDigit(line[j]))
            {
                j++;
            }

            int digitCount = j - digitStart;
            if (digitCount == 0)
            {
                tokens.Add(new CodeToken(letter, null, column));
                continue;
            }

            if (digitCount > 2)
            {
                throw new FormatException(
                    $"Code '{letter}{line[digitStart..j]}' has {digitCount} digits; expected 1 or 2.");
            }

            tokens.Add(new CodeToken(letter, int.Parse(line.AsSpan(digitStart, digitCount)), column));
            i = j - 1;
        }

        return tokens;
    }
}
