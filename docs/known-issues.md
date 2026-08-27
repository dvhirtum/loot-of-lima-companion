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

## 3. Unguarded `int.Parse` in the `--debug-page` path

`tools/LootOfLima.ScenarioImporter/Program.cs`.

`int.Parse(debugPage)` (twice) throws an unhandled `FormatException` with a stack trace for
`--debug-page x` — in the diagnostic mode whose whole purpose is to help when parsing is already
going wrong.

**Why it can wait:** cosmetic, and only reachable through a typo.

**Fix:** `int.TryParse` plus a usage message.

Raised by the review of [PR #3](https://github.com/dvhirtum/loot-of-lima-companion/pull/3).

## 4. Nothing is ever evicted, so room codes are never reclaimed

`src/LootOfLima.Server/Games/InMemoryGameStore.cs` and `InMemoryPlayerTokenStore.cs`.

`IGameStore` has no delete. A finished game keeps its entry, its room code stays claimed in
`_gameIdByRoomCode`, and every token ever issued stays in `_byToken`. Both grow for the life of the
process and nothing shrinks them.

The visible failure is `POST /api/games` starting to answer `NoRoomCodeAvailable` on a server that
looks idle: `CreateGameAsync` tries `RoomCodeAttempts` (8) random codes, and once enough of the
24^4 = 331,776 codes are held, eight draws can all collide.

**Why it can wait:** the arithmetic is not close. Collisions only become likely in the tens of
thousands of live games, and this is a companion for a table of three to five people that is
expected to be restarted between sessions — everything is in memory and dies with the process
anyway. Fixing it properly means deciding when a game is over for good, which is a question about
the product rather than about this code: a table sitting in `Finished` may still be being read by
players arguing about the last accusation.

**Fix:** an eviction policy — likely "idle for N hours" — plus `IGameStore.Remove` releasing the
room code and the tokens issued for that game. Best done alongside the persistence M6 already
anticipates, since the same question decides what gets written down.

Raised by the review of [PR #4](https://github.com/dvhirtum/loot-of-lima-companion/pull/4).

## 5. The `compose stop api` swap only works on Docker Desktop

`compose.yaml`, and by extension the second swap mode `CLAUDE.md` lists as a milestone exit
criterion.

Stopping the `api` container and running `dotnet run --project src/LootOfLima.Server` in its place
relies on nginx reaching the host through `host.docker.internal`. The `extra_hosts: host-gateway`
entry makes that name resolve on Linux too, but resolving is not the problem. There is no
`launchSettings.json` and nothing sets `ASPNETCORE_URLS`, so `dotnet run` takes the framework
default of `http://localhost:5000` — loopback only — and a connection arriving from the bridge
gateway address is refused. On Docker Desktop the traffic reaches the loopback listener anyway,
through the VM's port forwarding, which is why the swap works on this machine and would not work on
a Linux host.

The failure is a plain 502 from nginx — indistinguishable from "the api is not running yet", which
is a state the composition deliberately treats as ordinary. Nothing says the listener is the
problem.

**Why it can wait:** the only machine this runs on is Docker Desktop, and nothing else in either
swap depends on the platform.

**Fix:** `ASPNETCORE_URLS=http://0.0.0.0:5000 dotnet run --project src/LootOfLima.Server`, or add a
`launchSettings.json` with that `applicationUrl` so the plain command in `CLAUDE.md` works
everywhere. Worth doing when CI arrives, since a Linux runner is exactly where this bites.

## 6. A spectator is offered a seat they cannot take

`web/loot-of-lima-web/src/app/play/seat-strip.ts`.

The "Sit here" button is shown for any seat still held by a bot, with no check on whether this
browser holds a player token:

```html
@if (seat.isViewer) { ... } @else if (seat.kind === 'Bot') {
  <button ... (click)="claim.emit(seat.index)">Sit here</button>
}
```

A spectator reaches the game page through the host screen's own "Watch the table" link, so this is
a normal route rather than a contrived one. Clicking it sends `ClaimSeat`, which
`GameHub.WithPlayerAsync` refuses with `NotAPlayer` — "Join the game before taking a seat." — a
step the page never offered, because joining happens at `/join/ABCD` behind the QR code.

`LobbyPanel` gets the same case right on the same screen: it disables "Deal and begin" for a viewer
with no seat and says why. So the two panels disagree about what a spectator may be shown, which is
the part that will rot.

**Why it can wait:** the refusal is correct and addressed only to the clicker — no state changes and
nothing leaks. It is a dead end with a confusing message, not a broken game.

**Fix:** gate the button on `store.isSpectator()` as well as `seat.kind`, and put a link to
`/join/<code>` in its place. Belongs with M5's seat and passing UI, where this component is being
worked on anyway; an e2e spec for it costs a file by then, since M4.5 will have built the harness.

## 7. A rejected `Rejoin` after a reconnect leaves the client attached to nothing

`web/loot-of-lima-web/src/app/core/game-connection.ts`, `onreconnected`.

```ts
connection.onreconnected(() => {
  if (isCurrent()) {
    void this.rejoin();
  }
});
```

`void` discards the promise, so a rejected `Rejoin` is swallowed. Two things then fail together.
`rejoin` never reaches `setStatus('connected')`, so the banner stays on "reconnecting" — which is at
least visible. The real damage is on the server: a reconnect is a new connection id, the old one was
already dropped by `OnDisconnectedAsync` calling `connections.Detach`, and `Attach` only ever runs
inside `Rejoin`. `BroadcastAsync` sends per connection out of `InGame(gameId)`, so a connection that
never attached is never sent anything.

The result is the worst shape a bug can have here: the websocket is open, the transport is healthy,
commands appear to send, and the board simply stops changing. Nothing in the UI says the client has
been orphaned.

**Why it can wait:** it needs `Rejoin` to fail on a connection that has just succeeded in
reconnecting, which in practice means the server restarting inside the reconnect window. Not
observed in play.

**Fix:** catch the rejection, and retry rather than merely report it — a client that has an open
socket and no attachment can recover, so surfacing "disconnected" would be a lie. M6 lists
"Reconnect UX", which is where this belongs, but note that this is a correctness bug underneath it
rather than presentation: styling the banner would ship straight past it.
