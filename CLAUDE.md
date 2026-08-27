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
| M1 — pure rules engine + scenario oracle | **Done.** 1246 tests green |
| M2 — complete headless engine: dealer, passing, turns, abilities, accusation, `Project()` | **Done.** 1486 tests green |
| M3 — minimal API + SignalR server | **Done.** 1629 tests green, leak tests included |
| M4 — Angular skeleton, first playable loop | **Done.** 1629 .NET tests + 41 web tests green |
| M5 — SVG island map, abilities, accusation, passing UI | Next |
| M6 — polish | Not started |

**The full plan lives at `C:\Users\Dick\.claude\plans\nested-hatching-petal.md`** (outside the
repo). Read it before starting a milestone.

Known defects that are understood but deliberately unfixed live in `docs/known-issues.md`. Check it
before touching the importer or `BoardLayout`.

## Build and test

```
dotnet build      # warnings are errors
dotnet test       # expect 1629 passing

# The M2 demo: two scripted games, printed once per seat so the redaction is visible.
dotnet run --project tools/LootOfLima.Harness

# The server. Swagger-less by design; the integration tests are the API contract.
dotnet run --project src/LootOfLima.Server
```

```
cd web/loot-of-lima-web
npm test          # Vitest; expect 41 passing
npm start         # dev server on :4200, proxying /api and /hub to Kestrel on :5000
npm run start:lan # the same, reachable from a phone on the table's wifi
```

Both halves have to be running: `npm start` serves the app and proxies to `dotnet run`. The proxy
sets `changeOrigin: false` on purpose — the server builds the join URL the QR code encodes from the
request's `Host` header, so rewriting it would put `localhost` in the code a phone scans.

## Running it without dev servers

```
docker compose up --build     # the whole thing at http://localhost:4200
```

Either half swaps for a dev server by stopping only that container:

```
docker compose stop web   →   cd web/loot-of-lima-web && npm start
docker compose stop api   →   dotnet run --project src/LootOfLima.Server
```

**Neither container talks to the other over the compose network, and that is the whole trick.**
They meet on the host at port **5000**: `api` publishes it, and nginx reaches back to it through
`host.docker.internal`. So "the API" means whatever is listening on host port 5000 — the container,
or `dotnet run` once you have stopped it — and neither swap needs any reconfiguration. The web
container listens on **4200**, the port `npm start` already uses, so the address never changes and
a QR code somebody has already scanned keeps working.

Consequences worth knowing before editing any of it:

- **`nginx-proxy.conf` is a separate file on purpose.** nginx does not merge `proxy_set_header`: a
  location that sets one of its own discards every inherited one. `/hub` must set `Upgrade` and
  `Connection`, so putting the shared headers at server level would silently drop `Host`.
- **`Host` is forwarded as `$http_host`, not `$host`.** `$host` drops the port, and the join URL is
  built from this header — same reason the dev proxy sets `changeOrigin: false`.
- **`/api` and `/hub` are `location ^~`.** A regex location outranks a plain prefix one, so the
  static-asset rule otherwise wins `/api/games/ABCD/qr.png` and 404s the QR code.
- There is deliberately **no `depends_on`**: nginx answers 502 until something is on 5000, which is
  what you want while the api container is stopped on purpose.

`global.json` pins SDK **10.0.203**. This machine's default `dotnet` is a .NET 11 preview, so
without the pin everything silently targets the wrong SDK. Do not remove it.

## Layout

```
src/LootOfLima.Rules/       Pure engine. Zero dependencies.
src/LootOfLima.Contracts/   Client DTOs. Must NOT reference Rules.
src/LootOfLima.Projection/  Project(). The ONLY place Rules and Contracts meet.
src/LootOfLima.Scenarios/   Embedded seed data for the 239 official scenarios.
src/LootOfLima.Server/      Minimal API + SignalR hub.
tools/LootOfLima.Harness/            Headless demo. Replays scripted games.
tools/LootOfLima.ScenarioImporter/   Offline PDF -> seed data. See its README.
tests/                      Rules.Tests, Projection.Tests, Scenarios.Tests, Server.Tests.
compose.yaml                Both halves in Docker. Either one stops for a dev server.
web/loot-of-lima-web/       Angular 22 workspace. Standalone, zoneless, signals, Vitest.
  src/app/core/             contracts.ts, GameStore, GameConnection, ApiClient, identity.
  src/app/pages/            One folder per route, all lazy.
  src/app/play/             The pieces the game page is made of.
```

`Projection` exists because the projection has nowhere else to live: `Rules` references nothing and
`Contracts` must not reference `Rules`. Giving it an assembly of its own means the whole
hidden-information boundary is one function in one project.

`web/.../core/contracts.ts` mirrors `LootOfLima.Contracts` by hand and is the only place the client
is allowed to describe the wire. There is no generator: the DTOs are small, and a mismatch shows up
as a `strictTemplates` error rather than as a field that silently reads `undefined`.

Phases: `Lobby → Dealing → [Passing] → Announcing → InProgress → Finished`. `Passing` is skipped
when the deal settles it (three players, or a scenario); `Announcing` always happens but resolves
itself when the deal prescribes the answers. There is no `SeatClaim` phase — nothing could move a
game into it, so it would be a state no game is ever observed in. Seats are claimed during `Lobby`.

## Invariants — do not break these

1. **`LootOfLima.Rules` has zero `PackageReference`s** and references nothing else in the solution.
   Randomness enters only through an injected `IShuffleSource`. `AssemblyPurityTests` enforces it by
   reading the csproj — reflection cannot, because Roslyn omits references whose types go unused.
2. **`LootOfLima.Contracts` must never reference `Rules`**, so `GameState`, `Deal`, `Seat` and
   `SecretQuestionEntry` stay unreachable from any hub signature. This is the first layer of the
   hidden-information design, and `AssemblyPurityTests` enforces it the same way.
3. **The server never serializes `GameState`** — only `PlayerView`, produced by the single pure
   function `Projector.Project(state, viewerSeat)`. Every push goes through one `BroadcastAsync`
   that sends per connection; `Clients.All`/`Clients.Group` are never used for state.
4. **Private history is redacted by type, not by nulling fields.** A non-participant receives a
   different DTO with no field for the secret at all. Three entries work this way: a secret question
   (no range, no answer), a token pass (no location), and a *failed* accusation (no named pair — the
   rulebook says it reveals nothing, and a ruled-out pair is a real deduction).
5. **Board geometry and scenario data are data, not logic.** Editing
   `src/LootOfLima.Rules/Data/board-layout.json` incorrectly is caught immediately by the oracle.
6. **The client holds no rules logic.** `GameStore` is a holder, not a reducer: it keeps the newest
   `PlayerView` and slices it. Nothing on the client computes an answer, a legal move or a hand.
   This is the fifth layer of the hidden-information design — a client that derives nothing cannot
   derive what it was not told. The one thing it reads out of a string is the terrain letter of a
   location id, and that only decides what colour a token is drawn.
7. **`GameStore.apply` drops any frame older than the highest `Version` it has seen.** Commands are
   ordered by the server's store; the sends that follow them are not, so a frame can overtake its
   successor. The comparison is strict — a repeated version is a rejoin projecting the same
   revision to a seat instead of to a spectator, and dropping it would leave a player who has just
   sat down looking at somebody else's view.

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

- **Setup announcement, after passing and before play:** in turn order, each player announces the
  terrain they hold fewest of. Holding *none* of a terrain makes it the fewest. **On a tie the
  player chooses, and must not reveal that there was a tie** — so the engine only ever checks that
  an announcement is truthful, and nothing downstream records how many terrains were eligible. This
  is a general rule, not a solo/2-player one, and it applies at every table size.
- Abilities, once each per game: **Barrel** (re-roll dice), **Shovel** (treat a die as another
  terrain), **Pistol** (secret question, free range, *never* terrain-restricted).
- Accusation is legal at any time, even off-turn. Wrong accusation reveals **nothing** and
  eliminates the accuser, who must still answer questions.

## Settled product decisions

- **Client DTOs name directions, terrains and locations as strings**, and the server parses
  them. `Contracts` cannot reference `Rules`, so `Direction`, `Terrain` and `LocationId` are
  unreachable from any hub signature by design. If typed fields are ever wanted on the wire,
  **duplicate the enums into `Contracts`** - never reference `Rules` to get them.
- **The app does not model the roll.** So Barrel and Shovel have no in-app mechanics — they are
  only marked as spent. The die faces are recorded in `docs/dice.md` for reference; no code reads
  them.
- **The app always owns the deal.** Nobody types a hand in.
- **The app auto-answers for humans too**, from the hand it dealt.
- **Bots never take turns**, they only answer — faithful to the scenario books, which are pure
  puzzles where only the human asks.
- **No auto-solving.** Show history, own hand, pass knowledge and ability status; the player deduces.
- Official scenarios are five-player only, so 3- and 4-player games always use generated deals.
- **A scenario deal never passes tokens**, even at five players. Their published answers describe
  the hands exactly as dealt, so passing would contradict the book. `Passing.IsRequired` keys off
  `DealSourceKind`, not just seat count.
- **A scenario's printed "fewest terrain" letters *are* the setup announcements**, carried through
  as `Deal.PrescribedFewestTerrain` so a scenario game announces exactly what the booklet prints.
  They cannot be recomputed: **656 of the books' 1195 hands hold two terrains tied at the minimum**,
  and on a tie the choice is the player's, so the book has already made it. Recomputing would
  silently disagree with the booklet in the player's hands.

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
- **Code style lives in `.editorconfig` and is enforced, not suggested.**
- **Test layout and naming:** one folder per type under test, one test class per method
  (`Engine/CountInRangeTests.cs`), mirroring the production layout, with namespaces following the
  folders so the test explorer mirrors the app. Name tests `Should_<expected>_when_<condition>` in
  snake_case, and write the `// Arrange`, `// Act` and `// Assert` comments out explicitly.
- **Each folder namespace shadows the production type it is named after** —
  `LootOfLima.Rules.Tests.Engine` hides the `Engine` class, so bare `Engine.CountInRange` is
  CS0118. Test files alias it back in at the top: `using Sut = LootOfLima.Rules.Engine;` for the
  type under test, plus a short distinct alias per collaborator (`Board`, `Location`, `Library`).
  **The alias name must not match any test folder name**, or the folder namespace wins the lookup
  and the alias is unreachable.
- **The web tests follow the same layout and naming.** `core/game-store/apply.spec.ts` is a folder
  named for the type under test, one file per method, `describe('GameStore.apply')`, tests named
  `Should_<expected>_when_<condition>`, with the AAA comments written out. Spec files sit beside
  the source rather than in a separate tree, which is the one thing that differs from the .NET
  suites, because `tsconfig.spec.json` collects them by glob.
- **A new guard is not finished until it has been seen to fail.** Break the thing it guards, watch
  the test go red, then put it back. Two of the M3 leak tests passed with the bug still in place.
