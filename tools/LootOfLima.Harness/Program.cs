using LootOfLima.Harness;
using LootOfLima.Rules;
using LootOfLima.Scenarios;

// The M2 demo. Plays complete games through the headless engine and prints what each seat is
// allowed to see, so the hidden-information design can be read off the terminal rather than
// inferred from the tests.
int seed = ParseSeed(args) ?? 20260825;

Replay.Scenario(ScenarioLibrary.Get(ScenarioId(args)), seed);
Console.WriteLine();
Console.WriteLine();
Replay.Generated(playerCount: 4, humans: 2, seed: seed);

return 0;

static string ScenarioId(string[] args) => GetOption(args, "--scenario") ?? "b1s1";

static int? ParseSeed(string[] args) =>
    int.TryParse(GetOption(args, "--seed"), out int seed) ? seed : null;

static string? GetOption(string[] args, string name)
{
    int index = Array.IndexOf(args, name);
    return index >= 0 && index + 1 < args.Length ? args[index + 1] : null;
}
