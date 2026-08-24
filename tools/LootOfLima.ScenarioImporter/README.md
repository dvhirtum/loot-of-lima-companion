# Scenario Importer

Offline tool. Decodes the 20 official solo/two-player scenario books in
`docs/solo and 2-player rules/` into the seed data embedded in `LootOfLima.Scenarios`.

**Run it manually and rarely** — its output is committed, so neither the server nor `dotnet test`
ever needs the PDFs or `pdftotext`.

```
dotnet run --project tools/LootOfLima.ScenarioImporter -- \
  --pdf-dir "docs/solo and 2-player rules" \
  --out-dir src/LootOfLima.Scenarios/Data
```

Diagnostics, for when a book's layout does not parse:

```
dotnet run --project tools/LootOfLima.ScenarioImporter -- \
  --pdf-dir "docs/solo and 2-player rules" --debug-book 16 --debug-page 2
```

## Requirements

`pdftotext` (poppler) on `PATH`. Developed against **poppler 4.00**.

## Output

| File | Contents |
|---|---|
| `scenarios.json` | Decoded deals: five hands, the public reveal, the treasure, and the printed "fewest terrain" hint |
| `scenario-answers.bin` | Every published answer, one byte each, in canonical `start x end x terrainFilter x player` order (1024 per scenario) |

The answer blob is committed deliberately: it lets the oracle test check the engine against the
**printed book** rather than against our own re-derivation of it.

## How the decode works

Each book has its own obfuscation charts, so nothing can be shared between books.

| Page | Extraction | Contents |
|---|---|---|
| 2 | `-lineprinter -fixed 5` | Scenario Tokens Chart and Solution Chart (`-layout` mangles this six-column page) |
| 3-14 | `-layout` | Question table for scenarios 1-12 |
| 15 | `-layout` | Token lookup chart, 26x50 |
| 16 | `-layout` | Answer lookup chart, 26x50 |

**Hands for P2-P5 are not parsed from page 2 at all.** They are derived from the published answers:
a single-sector query `[k, k+1)` restricted to one terrain returns 0 or 1, and each sector holds
exactly one location per terrain, so those 24 answers name the hand exactly. Page 2 is needed only
for P1 and the reveal, with the 16 derived locations as a checksum.

## Validation

A scenario is emitted only if it passes **all** of these. The importer fails loudly and never
emits an unverified scenario.

1. Each derived hand holds exactly 4 locations.
2. Page 2's printed hands for P2-P5 match the derived hands.
3. **The oracle**: all 1024 published answers are reproduced by `Engine.CountInRange`.
4. P1 has 4 locations, the reveal has 2, and all 22 dealt locations are distinct.
5. **The Solution Chart's treasure column matches the treasure implied by the deal.** This is fully
   independent of everything above, so agreement confirms every hand and the reveal.
6. The printed "fewest terrain" hint really is a minimum of that hand.

## Known gap: 239 of 240

`b16s12` is not emitted. Book 16's Scenario Tokens Chart prints scenario 12's block as a duplicate
of scenario 11's (identical codes `W12 E42 L29 Z38`, `V47 K16 Q25 V40`, reveal `G7 S11`), which
contradicts scenario 12's own question table. **This is an error in the printed book, not in the
importer** — check 2 above catches it. Its P1 and reveal cannot be recovered, so the scenario is
dropped rather than guessed at.

## Parsing gotchas already solved

- Codes are split (`W 26`) and run together (`Y42Y21`) unpredictably, so whitespace is ignored and
  a code is a letter followed by a maximal digit run. Because codes always alternate letter/digits,
  that run is unambiguous.
- The `PAGE n` footer lands on the same line as the last data row. Left in, "PAGE 3" tokenizes as
  three bare letters plus a spurious `E3`, pushing the row to 33 codes so it is silently dropped.
- Row and block labels (`A`, `B`, `F`, `M`, `NE`, ...) are printed on a slightly different baseline
  and appear on the wrong line. They are ignored entirely; data rows are identified by carrying
  exactly 32 codes and are mapped to directions by their order.
- A lookup code can look exactly like a player marker (`P1` and `P5` are valid codes), so page 2 is
  parsed structurally — marker, terrain letter, four codes — and blocks are assigned to a scenario
  by character offset, never by regexing for `P\d`.
