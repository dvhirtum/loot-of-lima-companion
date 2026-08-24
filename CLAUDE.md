# Loot of Lima companion app

A web companion for the board game *Loot of Lima* (Allplay, 3-5 players). Players scan a QR code,
claim a seat, and see their own information on the island map. The app owns the deal, routes
questions, answers on behalf of bot **and** human players, tracks the special abilities, and keeps
secret questions secret.

**It is a companion, not a replacement.** The physical board, screens, tokens and dice stay on the
table. The app replaces the printed scenario booklet and the bookkeeping.

Stack: Angular 22 + .NET 10. Local-only for now — no CI/CD.

## Status

| Milestone | State |
|---|---|
| M1 — pure rules engine + scenario oracle | **Done.** 996 tests green |
| M2 — complete headless engine: dealer, passing, turns, abilities, accusation, `Project()` | Next |
| M3 — minimal API + SignalR server | Not started |
| M4-M6 — Angular client, then polish | Not started |

**The full plan lives at `C:\Users\Dick\.claude\plans\nested-hatching-petal.md`** (outside the
repo, on Dick's machine). Read it before starting a milestone — it holds the settled product
decisions and the rules research, and saves re-deriving any of it.

## Build and test

```
dotnet build      # warnings are errors
dotnet test       # expect 996 passing
```

`global.json` pins SDK **10.0.203**. This machine's default `dotnet` is a .NET 11 preview, so
without the pin everything silently targets the wrong SDK. Do not remove it.

## Layout

```
src/LootOfLima.Rules/       Pure engine. Zero dependencies.
src/LootOfLima.Contracts/   Client DTOs. Must NOT reference Rules.
src/LootOfLima.Scenarios/   Embedded seed data for the 239 official scenarios.
src/LootOfLima.Server/      (M3) minimal API + SignalR.
tools/LootOfLima.ScenarioImporter/   Offline PDF -> seed data. See its README.
tests/                      Rules.Tests, Scenarios.Tests, (M3) Server.Tests.
web/loot-of-lima-web/       (M4) Angular workspace.
```

## Invariants — do not break these

1. **`LootOfLima.Rules` has zero `PackageReference`s** and references nothing else in the solution.
   Randomness enters only through an injected `IShuffleSource`. `AssemblyPurityTests` enforces it.
2. **`LootOfLima.Contracts` must never reference `Rules`**, so `GameState`, `Deal`, `Seat` and
   `SecretQuestion` stay unreachable from any hub signature. This is the first layer of the
   hidden-information design.
3. **The server never serializes `GameState`** — only `PlayerView`, produced by the single pure
   function `Project(state, viewerSeat)`. Every push goes through one `BroadcastAsync` that sends
   per connection; `Clients.All`/`Clients.Group` are never used for state.
4. **Secret questions are redacted by type, not by nulling fields.** Non-participants receive a
   different DTO that has no field for the range or the answer.
5. **Board geometry and scenario data are data, not logic.** Editing
   `src/LootOfLima.Rules/Data/board-layout.json` incorrectly is caught immediately by the oracle.

## Domain facts worth not re-deriving

- 24 locations: 8 Beach `B1`-`B8`, 8 Forest `F1`-`F8`, 8 Mountain `M1`-`M8`.
- **Sector *k* holds `{Bk, Fk, Mk}`** — B1/F1/M1 in N, through B8/F8/M8 in NW.
- **The query arc is clockwise from start, inclusive of start, EXCLUSIVE of end.** Coincident
  arrows sweep the whole circle. This is the subtle part: the dial's arrows point at the eight
  compass *lines*, not at sectors, and tokens sit in the gaps between lines.
- Deal: 2 locations are set aside as the treasure; the other 22 are dealt evenly and the remainder
  revealed publicly.

  | Players | Hand | Revealed | Passing |
  |---|---|---|---|
  | 3 | 7 | 1 | none |
  | 4 | 5 | 2 | pass one recorded token across the table (`+2` seats) |
  | 5 | 4 | 2 | pass one right, then one left |

- Abilities, once each per game: **Barrel** (re-roll dice), **Shovel** (treat a die as another
  terrain), **Pistol** (secret question, free range, *never* terrain-restricted).
- Accusation is legal at any time, even off-turn. Wrong accusation reveals **nothing** and
  eliminates the accuser, who must still answer questions.

## Settled product decisions

- **The app does not model the roll.** The dice stay physical; the app never asks what was rolled
  and lets a player construct any question, trusting their roll allows it. So Barrel and Shovel have
  no in-app mechanics — they are only marked as spent. The die faces are recorded in `docs/dice.md`
  for reference; no code reads them.
- **The app always owns the deal.** It shuffles or loads a scenario, privately tells each human
  which tokens to pull, and simulates token passing. Nobody types a hand in.
- **The app auto-answers for humans too**, from the hand it dealt.
- **Bots never take turns**, they only answer — faithful to the scenario books, which are pure
  puzzles where only the human asks.
- **No auto-solving.** Show history, own hand, pass knowledge and ability status; the player deduces.
- Official scenarios are five-player only, so 3- and 4-player games always use generated deals.

## The scenario data

239 of the 240 printed scenarios are decoded into `src/LootOfLima.Scenarios/Data`. The oracle test
checks the engine against **244,736 answers exactly as printed**, which validates the board layout,
the arc semantics, `CountInRange` and the seed data at once. If it fails, suspect your change, not
the books.

`b16s12` is deliberately absent: book 16 prints scenario 12's tokens as a duplicate of scenario
11's, contradicting its own question table. That is an error in the printed book.

**The source PDFs are gitignored** (`docs/**/*.pdf`) and kept locally only. Nothing but the importer
reads them, and its output is committed, so build and test never need them.

## Conventions

- Branch off `main` and open a PR; never commit to `main` directly.
- Unit-test each piece as it is built. The rules engine is pure, so there is no excuse not to.
