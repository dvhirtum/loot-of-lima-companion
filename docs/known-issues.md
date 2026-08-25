# Known issues

Real defects found in review, understood and deliberately not fixed yet. Each entry says what
breaks, what it takes to trigger it, and why it can wait. None of them affects a normal run of
`dotnet test` or a normal import, which is exactly why they are easy to forget.

Fixed issues are removed from this file, not struck through — git remembers.

Raised by the review of [PR #1](https://github.com/dvhirtum/loot-of-lima-companion/pull/1).

## 1. `ParseRevealLine` skips the page-footer strip and the tokenizer guard

`tools/LootOfLima.ScenarioImporter/TokensChart.cs`, `ParseRevealLine`.

`ParsePlayerLine` and `TryParseSolutionRow` both call `PdfText.RemovePageFooter` and both wrap
`CodeTokenizer.Tokenize` in `try/catch (FormatException)`. `ParseRevealLine` does neither, so:

- A `PAGE n` footer landing on the Reveal line — the exact case `RemovePageFooter` exists for —
  tokenizes as a third code. `codes.Count != 2` then `continue`s silently and the scenario dies
  later with the unhelpful "could not read the Reveal row from the Scenario Tokens Chart".
- A 3-or-more-digit run in a Reveal segment throws `FormatException` out of `TokensChart.Parse`,
  which `Program.cs` catches at *book* level — losing all 12 of that book's scenarios over one bad
  line.

**Why it can wait:** the 20 shipped PDFs do not currently trigger either path, and the seed data is
committed, so a normal build never runs the importer. It bites whoever re-runs the import against a
reprint with a shifted layout.

**Fix:** mirror the other two parsers — strip the footer, catch `FormatException` per line.

## 2. A valueless flag falls back to its default instead of erroring

`tools/LootOfLima.ScenarioImporter/Program.cs`, `GetOption`.

```csharp
return index >= 0 && index + 1 < args.Length ? args[index + 1] : null;
```

A flag that is present but last on the command line returns `null`, which is indistinguishable from
absent. So `--pdf-dir "docs/..." --out-dir` (path forgotten, or eaten by shell quoting) silently
targets the default `src/LootOfLima.Scenarios/Data` rather than refusing to run.

**Why it can wait:** the write gate added in `ee72d3f` means a partial or misdirected run can no
longer overwrite the committed seed data, which was the damaging half of this.

**Fix:** treat a present-but-valueless flag as a usage error.

## 3. `sectorOrder` in `board-layout.json` is read by nobody

`src/LootOfLima.Rules/BoardLayout.cs`, `Load`.

`Load` validates `arcSemantics` and `sectors` but never touches the `sectorOrder` array. The real
clockwise ordering is the declaration order of the `Direction` enum, and `BuildArcMasks()` is
computed entirely independently of the loaded file.

The trap is CLAUDE.md invariant 5, "board geometry and scenario data are data, not logic": someone
editing the layout will reorder `sectorOrder` expecting the arcs to follow. Nothing changes, and
nothing complains.

**Why it can wait:** the field is inert, so it cannot produce a wrong answer — only wasted time.

**Fix:** either validate `sectorOrder` against `Directions.All` on load, or drop the field from the
schema so the enum is visibly the single source of truth.

## 4. Unguarded `int.Parse` in the `--debug-page` path

`tools/LootOfLima.ScenarioImporter/Program.cs`.

`int.Parse(debugPage)` (twice) throws an unhandled `FormatException` with a stack trace for
`--debug-page x` — in the diagnostic mode whose whole purpose is to help when parsing is already
going wrong.

**Why it can wait:** cosmetic, and only reachable through a typo.

**Fix:** `int.TryParse` plus a usage message.
