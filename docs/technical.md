# How it is built

Angular 22 + .NET 10. Local-only — no CI/CD. `global.json` pins SDK **10.0.203**; do not remove it.

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

`web/.../core/contracts.ts` mirrors `LootOfLima.Contracts` by hand and is the only place the client
describes the wire. There is no generator; a mismatch is a `strictTemplates` error.

## Invariants — do not break these

1. **`LootOfLima.Rules` has zero `PackageReference`s** and references nothing else in the solution.
   Randomness enters only through an injected `IShuffleSource`. `AssemblyPurityTests` enforces this
   by reading the csproj; reflection cannot.
2. **`LootOfLima.Contracts` never references `Rules`**, so `GameState`, `Deal`, `Seat` and
   `SecretQuestionEntry` are unreachable from any hub signature. Same enforcement.
3. **The server serializes only `PlayerView`**, from the pure `Projector.Project(state, viewerSeat)`.
   Every push goes through `BroadcastAsync`, which sends per connection. `Clients.All` and
   `Clients.Group` are never used for state.
4. **Private history is redacted by type, not by nulling fields.** A secret question, a token pass
   and a *failed* accusation each project to a separate DTO with no field for the secret.
5. **Board geometry and scenario data are data, not logic.** The oracle catches a bad edit to
   `src/LootOfLima.Rules/Data/board-layout.json`.
6. **The client holds no rules logic.** `GameStore` holds the newest `PlayerView` and slices it.
   Three derivations are allowed, all from the viewer's own data and none authoritative:
   `locationLabel` reads a location id's terrain letter to pick a token colour; `map-geometry.ts`
   turns a server-supplied sector index into coordinates; `SetupPanel` takes the minimum of the
   viewer's own three hand counts to decide which terrain buttons are enabled.
7. **`GameStore.apply` drops any frame whose `Version` is not strictly higher** than the highest
   seen. A repeated version is a rejoin projecting the same revision to a seat.
8. **`IslandMap` must not inject `GameStore`.** It takes the layout from `BoardStore` and the range
   and taps from `TableUi`; caption and hover `<title>` included. The e2e claims are `mapTokens`
   (two players' islands are identical) and `visibleTokens` (everything *but* the map).
9. **`SeatView.PistolArmed` is projected truthfully for every seat**, not only the viewer's.

## Before editing

- Client DTOs name directions, terrains and locations as **strings**; the server parses them. For
  typed fields on the wire, duplicate the enums into `Contracts` — never reference `Rules`.
- **`TableUi`** holds ephemeral UI state (a range being built, an accusation half-named), is provided
  by the game page rather than the root, and knows nothing about answers.
- **`PlayerIdentityStore` holds its identities in a signal.** A plain `Map` notifies nobody, and the
  reconnect effect reads them through a `computed`.
- **`GameConnection.connect` and `disconnect` are queued against each other**, or two callers get
  past the early return and a socket is built, started and never stopped.
- **`Seat.PistolArmedTurn` is an absolute turn**, so any turn advance disarms it. `EndAccusersTurn`
  carries it across the solo accusation branch.
- **`GameEndReason.AllPlayersEliminated` is unreachable from `Accuse`** and survives only as the
  fall-through of `AdvanceTurn`. Do not write a test claiming to reach it.
- **`ClaimSeat`'s two refusal texts are pinned in `ClaimSeatTests`.** Neither names a `seatIndex`
  (zero-based) or a player id.
- **`InMemoryGameStore.MutateAsync` holds a per-game `SemaphoreSlim`.** Without it two concurrent
  claims both succeed.

## Running it

```
dotnet build      # warnings are errors
dotnet test
dotnet run --project src/LootOfLima.Server     # Swagger-less; the tests are the contract
dotnet run --project tools/LootOfLima.Harness  # two scripted games, printed once per seat
```

```
cd web/loot-of-lima-web
npm test           # Vitest
npm start          # dev server on :4200, proxying /api and /hub to Kestrel on :5000
npm run start:lan  # the same, reachable from a phone on the table's wifi
npm run e2e        # Playwright. Boots both halves itself — nothing else needed
npm run e2e:docker # the same specs against the containers. Dick runs this one, never you
```

Both e2e scripts run `e2e:prepare` first: it downloads Chromium if missing and type-checks `e2e/`.
`npm ci` installs no browsers, and Playwright transpiles specs without checking them.

The dev proxy sets `changeOrigin: false`: the server builds the QR code's table URL from the
request's `Host` header.

### Docker

```
docker compose up --build     # the whole thing at http://localhost:4200
docker compose stop web   →   cd web/loot-of-lima-web && npm start
docker compose stop api   →   dotnet run --project src/LootOfLima.Server
```

The containers do not talk over the compose network. They meet on **host port 5000**: `api`
publishes it, nginx reaches back through `host.docker.internal`. "The API" is whatever listens on
5000, so neither swap needs reconfiguration. Web listens on **4200**, the port `npm start` uses, so
a scanned QR code keeps working.

- **`nginx-proxy.conf` is a separate file.** nginx does not merge `proxy_set_header`; a location
  setting one of its own discards every inherited one, and `/hub` must set `Upgrade` and
  `Connection`.
- **`Host` is forwarded as `$http_host`.** `$host` drops the port.
- **`/api` and `/hub` are `location ^~`**, or the regex static-asset rule wins
  `/api/games/ABCD/qr.png`.
- **No `depends_on`**: nginx answers 502 until something is on 5000.
- **`launchSettings.json` binds `dotnet run` to `http://*:5000`.** `localhost` refuses
  `host.docker.internal`; `0.0.0.0` is IPv4 only while `localhost` resolves to `::1` first on Linux.
  A malformed `launchSettings.json` does not fail — it prints one line and falls back to the
  defaults, costing the port and the environment.

### The e2e procedure

```
docker compose ps               # anything listed means ASK before going further
npm run e2e                     # against the working tree
```

The compose stack's default state is down, so the ordinary case needs nothing doing first.

- **Never run `docker compose down` or `up` unprompted.** Anything listed, Dick put there.
- **Never run `npm run e2e:docker`.** It is his, and "green in both modes" is his to report.
- **`down`/`up`, never `stop`/`start`.** A stop/start cycle can leave Docker Desktop's port proxy
  bound but reachable only from this machine — every check from the laptop passes while a phone on
  the same wifi gets nothing. `--build` whenever it goes back up.
- **Both halves want port 4200.** With the containers up, Playwright's `reuseExistingServer` reuses
  nginx and the default mode silently tests the last *built* bundle.
- **The collision runs both ways.** `ng serve` holds `::1` only, Docker publishes `::` and
  `0.0.0.0`, and `localhost` resolves `::1` first — so a leftover dev server lets
  `docker compose up` succeed while `e2e:docker` never reaches nginx. Stopping the shell that ran
  `npm start` leaves its node process alive. Check the port and check what answered:

  ```
  Get-NetTCPConnection -LocalPort 4200 -State Listen | ForEach-Object {
    "$($_.LocalAddress) pid=$($_.OwningProcess) $((Get-Process -Id $_.OwningProcess).ProcessName)" }
  (Invoke-WebRequest http://localhost:4200 -UseBasicParsing).Headers.Server   # nginx/1.x, or nothing
  ```

  nginx sends `Server: nginx/…` and an `ETag`; the dev server sends neither, and answers with
  `Vary: Origin` and `Cache-Control: no-cache`.

`npm run start:lan` is the fallback while the containers are down: it binds node to `0.0.0.0`
directly.

## The scenario data

239 of the 240 printed scenarios are decoded into `src/LootOfLima.Scenarios/Data`. The oracle test
checks the engine against **244,736 answers exactly as printed** — the board layout, the arc
semantics, `CountInRange` and the seed data at once. If it fails, suspect your change.

The source PDFs are gitignored (`docs/**/*.pdf`) and kept locally; only the importer reads them and
its output is committed. The rulebook plates (`docs/components.jpg`, `setup.jpg`, `gameplay.jpg`,
`notes.jpg`) *are* committed, re-encoded at 1400px and quality 82. Do the same with any added later.

## Conventions

- Branch off `main` and open a PR; never commit to `main` directly.
- Unit-test each piece as it is built.
- **Code style lives in `.editorconfig` and is enforced, not suggested.**
- **Test layout:** one folder per type under test, one class per method
  (`Engine/CountInRangeTests.cs`), mirroring the production layout, namespaces following the folders.
  Name tests `Should_<expected>_when_<condition>` in snake_case; write the `// Arrange`, `// Act` and
  `// Assert` comments out.
- **A test folder namespace shadows the type it is named after** — `LootOfLima.Rules.Tests.Engine`
  makes bare `Engine.CountInRange` a CS0118. Alias at the top of the file
  (`using Sut = LootOfLima.Rules.Engine;`) plus one short alias per collaborator. **No alias may
  match a test folder name.**
- **Web tests follow the same layout and naming** — `core/game-store/apply.spec.ts`,
  `describe('GameStore.apply')`, AAA comments written out. Specs sit beside the source;
  `tsconfig.spec.json` collects them by glob.
- **A new guard is not finished until it has been seen to fail.** Break the thing it guards, watch it
  go red, put it back.
- **The e2e suite asserts what a player sees, and nothing a cheaper suite already asserts** — no arc
  arithmetic, no oracle, no frame-level redaction. **Two `browser.newContext()`s are the only way to
  sit two humans at one table**; the seat lives in `localStorage`.
- **Value assertions in e2e belong to five-player scenario games only.** Generated deals are seeded
  from `Random.Shared.Next()` and there is no seed field on the wire, so 3- and 4-player specs assert
  structure: hand sizes, whether passing happens, that every seat announces.
- **In web and e2e code, anything that addresses an element takes the id and anything that reads the
  screen comes back a label** — `mapToken` and `accuse` take `B1`; `ownHand` and `visibleTokens`
  return `1B`.

### Traps

- **No backtick inside an inline component template, comments included.** The template is a template
  literal; one backtick ends the string and the parse error is reported at `@Component({`.
- **A `<select>` whose options come from `@for` must mark the chosen `<option>`** with
  `[selected]="option === chosen()"`, never bind `[value]` on the select — that write lands on an
  empty select and is never re-applied.
- **A `resource` in the error state re-throws out of `value()`**, and `defaultValue` does not save
  you. Ask `error()` first. `isLoading()` and `error()` are safe at any time.
- **Every `<select>` sits inside its `<label>`**, so its accessible name is the caption plus every
  option. Use the `field()` helper, not `getByLabel`.
- **`hasText` tests raw `textContent`; `allInnerTexts` returns it collapsed.** Anchor as `^\s*`.
- **`getByRole`'s `name` and `:has-text()` match case-insensitive substrings**, and panels share the
  screen. Use `exact: true` and `:text-is()` — an over-matching assertion inside a fixture surfaces
  as a failure somewhere else entirely.
- **`count()`, `allInnerTexts()` and friends do not auto-wait.** Assert with a locator first, then
  read.
- **Wait for the thing you are about to assert, not a proxy of it.** `HubConnection.State` flips to
  `Disconnected` before the `Closed` handler runs.
