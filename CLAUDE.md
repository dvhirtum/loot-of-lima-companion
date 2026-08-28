# Loot of Lima companion app

A web companion for the board game *Loot of Lima* (Allplay, 1-5 players). Players scan a QR code,
claim a seat, and see their own information on the island map. The app owns the deal, routes
questions, answers on behalf of bot **and** human players, tracks the special abilities, and keeps
secret questions secret.

**It is a companion, not a replacement.** The physical board, screens, tokens and dice stay on the
table. The app replaces the printed scenario booklet and the bookkeeping.

**`docs/rules.md` is the rules, and it is the authority.** See [The rulebook](#the-rulebook) before
settling any question about how the game works.

Stack: Angular 22 + .NET 10. Local-only for now — no CI/CD.

## Status

| Milestone | State |
|---|---|
| M1 — pure rules engine + scenario oracle | **Done.** 1246 tests green |
| M2 — complete headless engine: dealer, passing, turns, abilities, accusation, `Project()` | **Done.** 1486 tests green |
| M3 — minimal API + SignalR server | **Done.** 1629 tests green, leak tests included |
| M4 — Angular skeleton, first playable loop | **Done.** 1629 .NET tests + 48 web tests green |
| M4.5 — Playwright e2e harness, first loop pinned | **Done.** 17 e2e specs, green in both modes |
| M5 — SVG island map, abilities, accusation, scenario picker | **Done.** 1633 .NET + 84 web + 37 e2e green |
| M6 — polish | **In progress.** 1659 .NET + 104 web + 48 e2e green |

**Every milestone is finished on the same terms**, and a milestone meeting all but one of them is
not done:

1. `dotnet test` and `npm test` green, and from M4.5 onwards `npm run e2e` too.
2. **`docker compose up --build` still stands the whole thing up, and both images still build.**
   Adding a project, moving a file the server Dockerfile copies, changing a port or adding a route
   nginx has to know about all change the composition whether or not anybody noticed. The failure
   mode is quiet — everything works under `dotnet run` and `npm start` while the containers are
   broken for weeks.
3. Both swap modes still work: `compose stop web` with `npm start`, `compose stop api` with
   `dotnet run`.
4. This file reflects what is now true — status, test counts, and any new invariant.

**The full plan lives at `C:\Users\Dick\.claude\plans\nested-hatching-petal.md`** (outside the
repo). Read it before starting a milestone.

Known defects that are understood but deliberately unfixed live in `docs/known-issues.md`. Check it
before touching the importer, `BoardLayout`, or how a location is spelled.

## Build and test

```
dotnet build      # warnings are errors
dotnet test       # expect 1659 passing

# The M2 demo: two scripted games, printed once per seat so the redaction is visible.
dotnet run --project tools/LootOfLima.Harness

# The server. Swagger-less by design; the integration tests are the API contract.
dotnet run --project src/LootOfLima.Server
```

```
cd web/loot-of-lima-web
npm test          # Vitest; expect 104 passing
npm start         # dev server on :4200, proxying /api and /hub to Kestrel on :5000
npm run start:lan # the same, reachable from a phone on the table's wifi

npm run e2e        # Playwright; expect 48 passing. Boots both halves itself — nothing else needed.
npm run e2e:docker # the same specs against `docker compose up -d`, so nginx and the built bundle
```

Both e2e scripts run `e2e:prepare` first, which downloads Chromium if it is missing and type-checks
`e2e/` — **`npm ci` does not install browsers**, and Playwright transpiles the specs without ever
checking them, so without that step "nothing else needed" would be false and a type error in a spec
would fail no command at all.

Both halves have to be running: `npm start` serves the app and proxies to `dotnet run`. The proxy
sets `changeOrigin: false` on purpose — the server builds the join URL the QR code encodes from the
request's `Host` header, so rewriting it would put `localhost` in the code a phone scans.

**`npm run e2e` is the exception — it starts its own `dotnet run` and `ng serve`**, so it needs
nothing already running, though it will reuse a dev server that is.

**Stop the compose stack before running `npm run e2e`.** The web container listens on 4200 — the
same port `ng serve` uses, which is the whole point of the swap — and `reuseExistingServer` cannot
tell the two apart. With the containers up, the default mode silently becomes a second run of the
docker mode: it serves the last *built* bundle, so a source change is not in it. The symptom is a
suite that passes in a second and a half against code you have just broken, which is far worse than
a failure.

The docker mode covers what the default mode cannot: the production bundle rather than the dev
server's, and nginx rather than the Angular proxy. The `^~` location bug that 404'd the QR
endpoint was invisible to every other suite in this repository, and is the kind of thing only that
mode sees.

**And the same collision runs the other way, silently, because the two bind different addresses.**
`ng serve` listens on `::1` only; Docker publishes on `::` and `0.0.0.0`. So a leftover dev server
does *not* stop `docker compose up` — the container starts, `docker compose ps` shows
`0.0.0.0:4200->4200`, and everything looks right — but `localhost` resolves to `::1` first, so
`npm run e2e:docker` reaches the dev server and never touches nginx at all. It passes, and it has
tested nothing the default mode had not already tested.

Killing the dev server is not always enough to be sure: stopping the shell that ran `npm start`
leaves `ng serve`'s own node process alive and still holding `::1`. **Check the port rather than
trusting the stop**, and check what answered rather than that something did:

```
Get-NetTCPConnection -LocalPort 4200 -State Listen | ForEach-Object {
  "$($_.LocalAddress) pid=$($_.OwningProcess) $((Get-Process -Id $_.OwningProcess).ProcessName)" }
(Invoke-WebRequest http://localhost:4200 -UseBasicParsing).Headers.Server   # nginx/1.x, or nothing
```

nginx sends `Server: nginx/…` with an `ETag`; the dev server sends neither, and answers with
`Vary: Origin` and `Cache-Control: no-cache`. One header is the whole difference between having run
the docker mode and having believed you did.

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
- **`launchSettings.json` binds `dotnet run` to `http://*:5000`, not localhost.** nginx reaches the
  host through `host.docker.internal`, which is not a loopback address, so the framework default of
  `localhost:5000` refuses that connection anywhere the traffic is not quietly forwarded through a
  VM — that is, everywhere except Docker Desktop. It fails as a bare 502, indistinguishable from
  "the api is not up yet".
- **`*` and not `0.0.0.0`, which is IPv4 only.** The default this replaced bound both `127.0.0.1`
  and `[::1]`, and `proxy.conf.json` and `playwright.config.ts` both say `localhost` — which glibc
  resolves to `::1` first on exactly the Linux runner the bind was widened for. `0.0.0.0` refuses
  that connection and survives only on Node's happy-eyeballs retry. `*` binds dual-mode.
- A **malformed `launchSettings.json` does not stop `dotnet run`**: it prints one line and carries
  on with the defaults, so a JSON comment in there costs you the port *and* the environment
  silently.

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
  src/app/core/             contracts.ts, GameStore, GameConnection, ApiClient, BoardStore, identity.
  src/app/pages/            One folder per route, all lazy.
  src/app/play/             The pieces the game page is made of, plus map-geometry.ts and TableUi.
  e2e/                      Playwright. The only suite that drives a browser.
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
   location id, and that only decides what colour a token is drawn. The island map adds one more
   reading of the same kind: where each location sits comes from `GET /api/board`, the JSON
   `BoardLayout.Load()` reads, so `map-geometry.ts` turns a *sector index the server gave it* into
   a place to draw and never works out which sector anything is in. The second exception is the
   setup announcement: `SetupPanel` takes the minimum of three counts of the viewer's *own* hand to
   decide which terrain buttons are pressable. Both exceptions have the same shape and the same
   bound — derived from what this viewer was already told in full, never authoritative, and with the
   engine still refusing whatever arrives.
7. **`GameStore.apply` drops any frame older than the highest `Version` it has seen.** Commands are
   ordered by the server's store; the sends that follow them are not, so a frame can overtake its
   successor. The comparison is strict — a repeated version is a rejoin projecting the same
   revision to a seat instead of to a spectator, and dropping it would leave a player who has just
   sat down looking at somebody else's view.
8. **`IslandMap` must not inject `GameStore`.** The map draws all 24 locations, labelled and
   coloured by terrain, and draws them identically for everybody at the table — where a location is
   on the island and what it is made of are both geography, and the printed board says both. What a
   viewer may not know is *whose* a location is, and the map now says nothing about that at all: it
   takes the public board layout from `BoardStore` and the range and taps from `TableUi`, so no
   hand, reveal, pass or treasure is reachable from the component. That is the sixth layer of the
   hidden-information design and the only one enforced by what is *not* imported, so adding the
   injection back is the whole of the regression. The caption and the hover `<title>` are part of
   this: a caption reading the viewer's hand out would be the map telling a screen-reader user what
   it tells nobody else. The e2e claim is correspondingly `mapTokens`, which asserts two players'
   islands are identical, and `visibleTokens`, which asks about everything *but* the map — extending
   `visibleTokens` to the map would make it match all 24 on every page and quietly stop testing
   anything.
9. **A projected bool must never lie, even helpfully.** `SeatView.PistolArmed` is projected
   truthfully for *every* seat rather than only the viewer's, because redaction here is by type: a
   field that reads false for a seat that is armed is a falsehood, not a redaction. It leaks
   nothing either way — spending an ability writes a public `abilityUsed` entry carrying the same
   turn number.

## The rulebook

**`docs/rules.md` is the rulebook, transcribed by hand from the physical copy, and it is the
authority on how this game works.** It supersedes the source PDFs, anything remembered, and
anything in this file that disagrees with it. `docs/components.jpg`, `setup.jpg`, `gameplay.jpg`
and `notes.jpg` are the plates it refers to and are committed with it — unlike the scenario-book
PDFs, which stay gitignored because nothing but the importer reads them. They arrived as PNGs of
publisher renders and were re-encoded at 1400px and quality 82, which is 8.8 MB down to 752 KB with
the setup callouts and the notes-sheet grid still readable. Anything added here later deserves the
same treatment: a PNG of a photograph or a render is several megabytes of a repository forever.

Two things it is not:

- **It is the main rulebook, so it covers 3, 4 and 5 players.** The box says 1-5, and solo and
  two-player are separate rules that are not in this transcription. Everything this app does at
  those table sizes is a decision recorded under [Settled product decisions](#settled-product-decisions),
  not something to look for here.
- **It does not settle the arc's inclusive/exclusive ends.** It voices a question as *between* two
  directions and says to name the clockwise arrow first, which is as precise as the prose gets.
  What makes it exact is the oracle: 244,736 published answers, which only one reading survives.

One thing it describes as a physical act rather than as an entitlement, and the difference matters:
**a wrong accuser is not shown the treasure.** At a table the accuser looks at the two set-aside
tokens themselves, because somebody has to check, and if they are wrong they have seen the answer
and are out. Here the app does the checking, so nobody needs to look, and `Projector` fills in
`Treasure` only in `Finished` — an eliminated player still at the table and still answering is one
tell away from ending everybody else's game. Taking the checking over is the app's whole job; being
handed the answer was never the reward for guessing wrong.

Where the app deliberately departs from it, the departure is written down as a settled decision
with its reason. Every one of them follows from the app owning the deal, from the app not modelling
the dice, or from a table size this book does not cover — none is a house rule about how the game
plays.

## Domain facts worth not re-deriving

- 24 locations: 8 Beach `B1`-`B8`, 8 Forest `F1`-`F8`, 8 Mountain `M1`-`M8`.
- **The id is `B1`; the physical token is stamped `1B`, and that is what a player is shown.** The
  flip is presentation and happens only in the client, in `core/locations.ts`'s `locationLabel`.
  Nothing else is renamed: the engine, `board-layout.json`, all 239 scenario files, the wire and
  the accusation a client sends back all still say `B1`, and so does the map's `data-location`.
  So in the web code and the e2e suite, **anything that addresses an element takes the id and
  anything that reads the screen comes back a label** — `mapToken` and `accuse` take `B1`,
  `ownHand` and `visibleTokens` return `1B`. Mixing them is how a leak assertion passes by never
  matching anything. Making the two halves agree is wanted eventually and is known issue 8 —
  a rename through the engine, the seed data and the importer at once, not a widening.
- **Sector *k* holds `{Bk, Fk, Mk}`** — B1/F1/M1 in N, through B8/F8/M8 in NW.
- **The query arc is clockwise from start, inclusive of start, EXCLUSIVE of end.** Coincident
  arrows sweep the whole circle. This is the subtle part: the dial's arrows point at the eight
  compass *lines*, not at sectors, and tokens sit in the gaps between lines. The rulebook confirms
  both halves without stating either outright: it says to name the clockwise arrow first and that
  "between N and W" is a different question from "between W and N", and it says the two arrows may
  point the same way but that doing so is only useful when the question names a terrain — which is
  true of a full circle and of nothing else, since all three terrains across the whole island is
  just that player's hand size, and everybody already knows it. The inclusive/exclusive ends
  themselves come from the oracle, not from the prose.
- **The app phrases a question more precisely than the book voices it, on purpose.** The book says
  "between" two directions; the question builder reads "clockwise from N up to, but not including,
  SE". That is the same question said unambiguously, and it is not a divergence to tidy away — the
  arc's ends are exactly what a player is most likely to get wrong at the table.
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
  terrain), **Pistol** (secret question, free range).
- **Abilities are spent on your own turn, while constructing your question.** `SpendAbility` starts
  with `RejectTurnHolder` for exactly this reason, and that is the rulebook's rule rather than a
  convenience.
- **The roll decides only which terrains a question may name.** Three dice are rolled, two are
  chosen: two of the same terrain means the question must be about *that* terrain, two different
  terrains means it is about all of them, and blue faces are wild. So a question is about one
  terrain or about all three — never two — which is why `QueryRange.TerrainFilter` is a single
  nullable terrain and why the shape the rulebook calls out as illegal is inexpressible here.
- **A Pistol question may name a terrain, and `docs/rules.md` says so.** This used to be recorded
  the other way round — that the printed text forbade it and the publisher had corrected itself —
  and the transcription from the physical book shows there was never anything to correct. The
  Pistol lifts the *roll*: you may ask anything a legal roll could theoretically have given you,
  and the one example it names as still illegal is asking about two terrains at once. A single
  terrain in an arc is an ordinary shape. So there is nothing extra for the engine to enforce and
  no `AskSecretQuestion` refusal about terrain — the same reason ordinary question legality goes
  unchecked, that the app does not model the roll, applies unchanged. `SecretQuestionView` carries
  the terrain to the two participants; `SecretQuestionOccurredView` has no property for it, as it
  has none for the range.
- Accusation is legal at any time, even off-turn. Wrong accusation reveals **nothing** and
  eliminates the accuser, who must still answer questions.
- **A wrong accusation at a table of two humans ends the game**, because one survivor is
  last-player-standing. Anything that has to observe play *continuing* after an elimination — the
  eliminated seat still answering, the turn passing on — needs a third human at the table. Bots do
  not count: they are never eliminated and never take turns.
- **A wrong accusation at a table of *one* human eliminates nobody.** `GameState.IsSolo` is the
  test, and `Accuse` returns before the elimination. The seat stays in, the phase stays
  `InProgress`, the turn advances as it would after any other action taken on your own turn, and
  the pair is ruled out — which is a real deduction and the accuser's own. See the settled decision
  below for why this is not a house rule.
- **`GameEndReason.AllPlayersEliminated` is now unreachable from `Accuse`.** A solo table returns
  before the elimination, and at two or more humans the game ends the first time the active count
  falls to one, so it can never fall to zero. It survives only as the fall-through of `AdvanceTurn`,
  which has to return something. Do not write a test that claims to reach it.
- **The compass letters belong on the eight lines, not in the middle of the sectors.** The dial's
  arrows point at the lines, the tokens sit in the gaps, and sector *k* is the gap *clockwise* from
  line *k* — so B1/F1/M1 are drawn between the N letter and the NE one. Putting a letter in the
  middle of a slice invites a player to read "from NE" as the middle of the north-east gap, and the
  wedge then appears to start somewhere the question did not say.

## Settled product decisions

- **Client DTOs name directions, terrains and locations as strings**, and the server parses
  them. `Contracts` cannot reference `Rules`, so `Direction`, `Terrain` and `LocationId` are
  unreachable from any hub signature by design. If typed fields are ever wanted on the wire,
  **duplicate the enums into `Contracts`** - never reference `Rules` to get them.
- **The app does not model the roll.** So Barrel and Shovel have no in-app mechanics — they are
  only marked as spent. The die faces are recorded in `docs/dice.md` for reference; no code reads
  them.
- **The app always owns the deal.** Nobody types a hand in.
- **The lowest-numbered human seat starts**, which is `ActiveSeat = state.ActiveHumanSeats.First()`
  in `BeginPlay`. The rulebook's tiebreak is that whoever most recently dug something up
  goes first, and no app can know that; asking would be a question at setup for a decision nobody
  cares about, and the seats are claimed in the order people sat down anyway.
- **The physical tokens are counters, and every player takes any handful face down.** This replaces
  setup step 8, where each player slots their own dealt tokens into their screen's map, and it is
  the one place the app changes something a player physically does. They do *not* correspond to the
  hand the app dealt, and the app tells players so at setup. Having each player pick up the tokens
  the app named would lose the game before the first question: 22 of the 24 are dealt into hands or
  turned face up and exactly 2 are the treasure, so once every hand has left the table the 2
  remaining **are** the treasure — at three, four and five players alike, since the arithmetic is
  24 − 22 and has nothing to do with the table size. Watching who picks up what leaks the hands on
  the way there as well. **This is not a disagreement with the rulebook.** At a real table the 2
  treasure tokens are set aside before the deal and are physically gone, so nothing is left over;
  here the app holds them, so they would be the two nobody picked up. The deviation is forced by
  the app owning the deal and disappears the moment it does not. Nothing physical is read by the
  app, so the counters cost nothing and keep the boards looking right.
- **Setup passing moves no physical token either.** Every seat gives exactly as many as it receives
  — one each way at four players, two each way at five, since there a token goes to *each* neighbour
  — so the counts never change, and there is no named token in front of anybody to hand over. The
  panel reads as a statement of what the app moved rather than as an instruction.
- **The app auto-answers for humans too**, from the hand it dealt.
- **Bots never take turns**, they only answer — faithful to the scenario books, which are pure
  puzzles where only the human asks.
- **No auto-solving.** Show history, own hand, pass knowledge and ability status; the player deduces.
- Official scenarios are five-player only, so 3- and 4-player games always use generated deals.
- **Ephemeral UI state lives in `TableUi`, provided by the game page and not at the root.** A range
  being built and an accusation half-named are wanted in two components each — the question builder
  chooses a range and the map draws it; the map is tapped and the accusation panel fills up — but
  they are not game state, and they must not survive the page. Nothing in it knows what an answer is.
- **A forced setup announcement is still announced by a human.** When the hand leaves exactly one
  truthful terrain the panel says so and opens one button, but it never sends by itself. Announcing
  automatically would resolve every untied seat the instant the phase reached it, leaving the tied
  seats as the only ones anybody waits for — so the pause would tell the whole table which players
  had a tie, which is the one thing the rulebook says a tie must not reveal. The click is also the
  beat where the player says it aloud, which is the physical act this app is a companion to.
- **Untruthful terrains are disabled, not hidden.** The count beside each is the viewer's own hand,
  so showing all three is what lets a player check the offer instead of taking the app's word for
  it. Offering a button whose only outcome is a refusal is the same defect as the seat a spectator
  could not take — the refusal was correct, and it named an internal seat index at somebody who had
  done nothing but press what they were shown.
- **The game page's phase slot belongs to a seat, and one `@if (!store.isSpectator())` says so for
  every phase.** `StartGame`, `ConfirmTokensTaken`, `ConfirmPassDone`, `AnnounceFewestTerrain` and
  both ask methods all reach the hub through `WithSeatAsync`, so a viewer without a seat is refused
  every one of them; the lobby panel, all three faces of `SetupPanel` and the question builder are
  therefore inside that one condition rather than each remembering separately. **A new phase panel
  goes inside it too.** Shown to a spectator these did not merely clutter — `SetupPanel` reads its
  count off `mySeat()`, so it asked somebody with no seat to take *any 0 tokens*. What a spectator
  keeps is everything that is true of them: the island, the log, the seat strip, and the tokens
  panel saying they are watching without a seat. In `Lobby` the seat strip still offers them the way
  in, because there the way in is real — `ClaimSeat` is the one command that is refused by *phase*
  rather than by seat, so it is the one offer that has to disappear on the deal instead.
- **There is one game-page layout, ordered by the loop, and the width decides only how many columns
  it is poured into.** The panels are the grid's own children in the order a player uses them —
  island → what this phase wants from you → history → abilities → accusation → hand — with no column
  wrappers and **no `order` declaration anywhere**, so DOM order, reading order, tab order and
  painted order agree by construction. On a laptop the island takes `grid-row: span 2`, which is the
  entire difference between the two sizes: the question builder and the log land beside it instead
  of below it, and the hand, the abilities and the accusation flow on into the cells after them.
  There used to be two layouts — a laptop's two columns, and a phone stack reordered on top of them
  with `order` and `display: contents` — and both buried the question builder, which is how the same
  complaint arrived twice. The phone put the ability bar between the island and the builder; the
  laptop put the map's full height *and* the ability bar above it in one column, so asking meant
  scrolling past the island to choose a range and back up to check the arc against it. Abilities are
  spent at most three times a game and the ask loop happens every turn, so the loop wins the top of
  the page at every size. **`MyHandPanel` is last because it is the only panel nothing is ever done
  to**, and because `SeatStrip` already prints this seat's own `knownHand` at the top of the page —
  what is only in that panel is the face-up row, and at the end the treasure. Abilities and
  accusation outrank it because they are things a player can act on. `e2e/page-layout.spec.ts`
  asserts both sizes by measured position — vertical order on a phone, and beside-not-below on a
  laptop — because a DOM-order check says nothing about which column the grid actually put a panel
  in.
- **The map is about the terrain, and says nothing about who holds what.** Three coloured bands and
  24 terrain-coloured tokens, the same picture on every screen at the table. The four ownership
  states it used to draw — mine, revealed, passed, treasure — are gone along with `data-state`, and
  the reason is what that buys: with nothing on the map derived from a hand, `IslandMap` stops
  injecting `GameStore` altogether, and the leak it was the likeliest component in this app to
  spring becomes unwritable rather than merely untested. The cost is real and was accepted: the
  treasure no longer lights up at the end (it is still spelled out in the hand panel, which is where
  a player reads their tokens anyway), the face-up locations no longer read as public on the island,
  and what setup passing taught you is left in the history where it was said. What the map is *for*
  is the question being built — which band, which slice — and that got better rather than worse: the
  highlight is now per terrain, so "how many forest locations from N up to SE" lights the forest band
  and the three forest tokens in it instead of a wedge across all three rings that said "somewhere
  in here" about a question that named one terrain.
- **The island is drawn on a three-step scale of one colour per terrain**, and a highlight that is
  not made of terrain colour is a bug. The band is the terrain mixed down into the sea, the token is
  the terrain plain, and the segment a question covers is `--beach-bright` / `--forest-bright` /
  `--mountain-bright` — paired with the plain ones in `styles.css` exactly as `--brass-bright` is
  with `--brass`, so no component picks its own greens. A single brass wash for all three came
  first and was wrong: against the beach band it barely showed and against the forest one it was
  worse, so it said "something is selected" without saying which band — the one thing the highlight
  exists to say. Asking about Any lights all three at once and they must stay told apart. The lit
  tokens' ring is `--ink` for the same reason the wash failed: a bright ring on newly-brightened
  ground disappears. The Pistol changes the segment's outline to `--coral` and leaves the fill
  alone, because which terrain is being asked about is worth more than a second way of saying
  "secret".
- **The accusation is a panel, not a modal.** The two locations are named by tapping the island, and
  a modal would cover the thing being tapped. It stays folded behind one button instead, because an
  accusation cannot be taken back.
- **A solo game does not end on a wrong accusation, and that follows from the projection rather
  than from taste.** Eliminating the only player finishes the game, and `Finished` is the single
  phase in which `Projector` fills in `Treasure` — so the penalty for guessing wrong would be being
  handed the answer, and any "keep playing" offer made afterwards would sit next to it. The books
  are puzzles, and finding out a pair is wrong is how a puzzle is solved. So the game carries on
  and `GameEngine.GiveUp` becomes the only other way out. This does not contradict `docs/rules.md`:
  that is the main rulebook and it describes 3 to 5 players, where "eliminated, but still answering"
  and "last player standing wins" are exactly what the engine does. A table of one is outside it.
- **Giving up is refused until that seat has accused and been wrong at least once.** `GiveUp` is
  the one command in this application whose entire purpose is to reveal the treasure, so it is not
  available for the asking: a button sitting there from the first turn is one misclick from
  spoiling a puzzle somebody was halfway through. Requiring a wrong accusation first means giving
  up costs what giving up costs — you had to commit to a pair and find out it was not the pair.
  The gate is in the engine, not in the panel; the panel not drawing the button is the same
  courtesy as the disabled terrain buttons, never the enforcement.
- **The way off the game page is a link on the header's own line**, not a nav row. A row of its own
  would cost a phone the top of its screen for something pressed once a session, and the island has
  just been given that space. Leaving needs no confirming and releases no seat: the seat is a token
  in that browser's storage, so the same code sits back down. The finished banner is the one place
  a fresh game is offered unprompted, because it is the one moment somebody is certainly looking
  for one.
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
- **The e2e suite asserts what a player sees, and nothing a cheaper suite already asserts.** No arc
  arithmetic, no oracle, no frame-level redaction — `Server.Tests` checks the raw bytes of every
  frame, which is stricter than the DOM can be. What only a browser can say is whether the page in
  front of a player shows them somebody else's hand, and **two `browser.newContext()`s are the only
  way to sit two humans at one table**: the seat lives in `localStorage`, so two pages of one
  context are the same person twice and make every two-player assertion vacuous.
- **Value assertions in e2e belong to five-player scenario games only.** A generated deal is seeded
  from `Random.Shared.Next()` and `CreateGameRequest` has no seed field, so nothing about a shuffled
  table repeats. 3- and 4-player specs assert structure — hand sizes, whether passing happens, that
  every seat announces. Adding a test-only seed would put a hook in the wire contract to buy
  assertions `b1s1` already makes.
- **Three Playwright traps this app walks straight into.** Every `<select>` sits *inside* its
  `<label>`, so its accessible name is the caption plus every option — `getByLabel('Ask')` never
  matches exactly, and matches for the wrong reason otherwise. Use the `field()` helper. And
  `hasText` tests raw `textContent` while `allInnerTexts` returns it collapsed, so a string read out
  of an element will not match an anchored pattern put back into it; anchor as `^\s*`.
- **Text matching is substring by default, and panels share the screen.** `getByRole`'s `name` and
  CSS `:has-text()` both match case-insensitive substrings. The case that taught this was "Your
  tokens" also matching the Dealing panel, then headed "Take your tokens", with both panels up at
  once; that heading has since been renamed and the collision with it, but the reads stay `exact:
  true` and `:text-is()` because the next overlap will not announce itself. It only showed up
  against Docker, where the phase changes fast enough to still be showing when the read happens: a
  locator ambiguity is a flake whose probability depends on how quick the server is.
- **`count()`, `allInnerTexts()` and friends are one-shots with no auto-waiting.** Reading one
  before the first frame has rendered is a race that passes locally and fails under load. Do the
  waiting with a locator assertion first, then read.
- **Wait for the thing you are about to assert, not for a proxy of it.** `HubConnection.State`
  flips to `Disconnected` before the `Closed` handler has run, so `LastResortTests` waited on the
  state and then asserted on the handler's list — which under enough parallel load came back empty
  and reported "the collection was empty" about a guard that had worked perfectly. It surfaced as
  one test occasionally reddening in the full suite and always passing alone, which is the least
  useful shape a failure has. Adding tests to `Server.Tests` adds load, so it can resurface
  anywhere the same shortcut is taken.
