using System.Text.RegularExpressions;
using LootOfLima.Rules;

namespace LootOfLima.ScenarioImporter;

/// <summary>A single scenario's entry on the Scenario Tokens Chart (page 2).</summary>
internal sealed class ScenarioTokens
{
    /// <summary>Hands for P1..P5, indexed 0..4. Only P1 is strictly needed — P2-P5 are derived
    /// from the question tables and these serve as an independent cross-check.</summary>
    public LocationId[]?[] Hands { get; } = new LocationId[]?[5];

    /// <summary>The terrain in which each player holds the fewest tokens, as printed.</summary>
    public Terrain?[] FewestTerrain { get; } = new Terrain?[5];

    /// <summary>The two publicly revealed locations.</summary>
    public LocationId[]? Reveal { get; set; }

    /// <summary>The treasure as printed in the Solution Chart's "Treasure" column. Independent of
    /// everything else, so it is used to cross-check the treasure we derive as the complement.</summary>
    public LocationId[]? SolutionTreasure { get; set; }
}

/// <summary>
/// Parses page 2, which prints twelve scenario blocks in two rows of six columns.
/// </summary>
/// <remarks>
/// Column slicing has to happen before any regex: a lookup code such as <c>P1</c> or <c>P5</c> is
/// indistinguishable from a player marker, so blocks are located structurally (marker, terrain
/// letter, four codes) and then assigned to a scenario by their character offset.
/// </remarks>
internal static partial class TokensChart
{
    private const int CodesPerHand = 4;

    public static Dictionary<int, ScenarioTokens> Parse(string pageText, LookupChart tokenChart)
    {
        Dictionary<int, ScenarioTokens> result = new();
        string[] lines = pageText.Split('\n');

        // Header columns, e.g. "Scenario1Tokens" at offset 12. Each header group covers six blocks.
        List<(int Scenario, int Column)> headers = new();

        foreach (string line in lines)
        {
            MatchCollection headerMatches = HeaderPattern().Matches(line);
            if (headerMatches.Count > 0)
            {
                headers.Clear();
                foreach (Match match in headerMatches)
                {
                    headers.Add((int.Parse(match.Groups[1].Value), match.Index));
                }

                continue;
            }

            if (headers.Count == 0)
            {
                continue;
            }

            if (line.Contains("Reveal", StringComparison.Ordinal))
            {
                ParseRevealLine(line, headers, tokenChart, result);
                continue;
            }

            if (TryParseSolutionRow(line, tokenChart, result))
            {
                continue;
            }

            ParsePlayerLine(line, headers, tokenChart, result);
        }

        return result;
    }

    private static void ParsePlayerLine(
        string line,
        List<(int Scenario, int Column)> headers,
        LookupChart tokenChart,
        Dictionary<int, ScenarioTokens> result)
    {
        List<CodeToken> tokens;
        try
        {
            tokens = CodeTokenizer.Tokenize(PdfText.RemovePageFooter(line));
        }
        catch (FormatException)
        {
            return;
        }

        // A block is: player marker (P1..P5), a bare terrain letter, then four lookup codes.
        for (int i = 0; i + 1 + CodesPerHand < tokens.Count; i++)
        {
            CodeToken marker = tokens[i];
            if (marker.Letter != 'P' || marker.Number is not (>= 1 and <= 5))
            {
                continue;
            }

            CodeToken terrainToken = tokens[i + 1];
            if (!terrainToken.IsBareLetter || terrainToken.Letter is not ('B' or 'F' or 'M'))
            {
                continue;
            }

            List<CodeToken> codes = tokens.GetRange(i + 2, CodesPerHand);
            if (codes.Exists(c => c.IsBareLetter))
            {
                continue;
            }

            int scenario = NearestScenario(headers, marker.Column);
            ScenarioTokens entry = GetOrAdd(result, scenario);
            int player = marker.Number.Value;

            entry.Hands[player - 1] = [.. codes.Select(c => Decode(c, tokenChart))];
            entry.FewestTerrain[player - 1] = Terrains.FromCode(terrainToken.Letter);

            i += 1 + CodesPerHand;
        }
    }

    private static void ParseRevealLine(
        string line,
        List<(int Scenario, int Column)> headers,
        LookupChart tokenChart,
        Dictionary<int, ScenarioTokens> result)
    {
        foreach (Match match in RevealPattern().Matches(line))
        {
            // Everything up to the next "Reveal" (or end of line) belongs to this block.
            int start = match.Index + match.Length;
            Match nextMatch = match.NextMatch();
            int end = nextMatch.Success ? nextMatch.Index : line.Length;

            List<CodeToken> codes = CodeTokenizer.Tokenize(line[start..end]).Where(c => !c.IsBareLetter).ToList();
            if (codes.Count != 2)
            {
                continue;
            }

            int scenario = NearestScenario(headers, match.Index);
            GetOrAdd(result, scenario).Reveal = [.. codes.Select(c => Decode(c, tokenChart))];
        }
    }

    /// <summary>
    /// Reads a Solution Chart row: <c>&lt;scenario&gt; &lt;sum&gt; &lt;code&gt; &lt;code&gt; &lt;24 numbers&gt; &lt;constant&gt;</c>.
    /// The two codes decode straight to the treasure. Rows are recognised by starting with the
    /// scenario number and carrying exactly two lookup codes, which the column header (24 codes)
    /// and the player rows (which start with "P") never do.
    /// </summary>
    private static bool TryParseSolutionRow(
        string line,
        LookupChart tokenChart,
        Dictionary<int, ScenarioTokens> result)
    {
        Match match = SolutionRowPattern().Match(line);
        if (!match.Success)
        {
            return false;
        }

        List<CodeToken> codes;
        try
        {
            codes = CodeTokenizer.Tokenize(PdfText.RemovePageFooter(line))
                .Where(t => !t.IsBareLetter)
                .ToList();
        }
        catch (FormatException)
        {
            return false;
        }

        if (codes.Count != 2)
        {
            return false;
        }

        int scenario = int.Parse(match.Groups[1].Value);
        if (scenario is < 1 or > 12)
        {
            return false;
        }

        GetOrAdd(result, scenario).SolutionTreasure = [.. codes.Select(c => Decode(c, tokenChart))];
        return true;
    }

    private static LocationId Decode(CodeToken code, LookupChart tokenChart) =>
        LocationId.Parse(tokenChart[code.Letter, code.Number!.Value]);

    /// <summary>Assigns a block to the scenario whose header it sits under. Blocks start slightly
    /// left of their header, so the nearest header wins rather than the nearest preceding one.</summary>
    private static int NearestScenario(List<(int Scenario, int Column)> headers, int column)
    {
        (int Scenario, int Column) best = headers[0];
        int bestDistance = int.MaxValue;

        foreach ((int Scenario, int Column) header in headers)
        {
            int distance = Math.Abs(header.Column - column);
            if (distance < bestDistance)
            {
                bestDistance = distance;
                best = header;
            }
        }

        return best.Scenario;
    }

    private static ScenarioTokens GetOrAdd(Dictionary<int, ScenarioTokens> result, int scenario)
    {
        if (!result.TryGetValue(scenario, out ScenarioTokens? entry))
        {
            entry = new ScenarioTokens();
            result[scenario] = entry;
        }

        return entry;
    }

    [GeneratedRegex(@"Scenario\s*(\d+)\s*Tokens", RegexOptions.IgnoreCase)]
    private static partial Regex HeaderPattern();

    [GeneratedRegex("Reveal", RegexOptions.IgnoreCase)]
    private static partial Regex RevealPattern();

    [GeneratedRegex(@"^\s*(\d{1,2})\s+\d")]
    private static partial Regex SolutionRowPattern();
}
