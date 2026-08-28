# Known issues

Real defects found in review, understood and deliberately not fixed yet. Each entry says what
breaks, what it takes to trigger it, and why it can wait. None of them affects a normal run of
`dotnet test` or a normal import, which is exactly why they are easy to forget.

Fixed issues are removed from this file, not struck through — git remembers.

Numbers are identifiers, not positions: a removed issue leaves a gap rather than renumbering the
ones after it, so a reference to "known issue 6" means the same defect a year later.

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
room code and the tokens issued for that game. Best done alongside persistence, since the same
question decides what gets written down.

Raised by the review of [PR #4](https://github.com/dvhirtum/loot-of-lima-companion/pull/4).

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
socket and no attachment can recover, so surfacing "disconnected" would be a lie. This belongs with
reconnect UX, but it is a correctness bug underneath rather than presentation: styling the banner
would ship straight past it.

Raised while renaming the locations for the physical game.

## 8. The two halves spell a location differently

The physical game stamps the number first, so the token in a player's hand reads `1B`. The client
now shows that everywhere a player looks, but the flip is presentation only —
`web/.../core/locations.ts`, `locationLabel` — and everything behind it still says `B1`: the engine,
`board-layout.json`, all 239 scenario files, the wire, `data-location`, the server's logs and the
harness's output.

So the two spellings coexist and the seam is where they meet:

- `GameHub.TryReadLocation` refuses an unreadable location with `'1B' is not one of the 24 island
  locations.` — naming the id format at a player who typed exactly what the box says. Nothing in
  the app can currently produce that message (the client only ever sends ids it was given), but it
  is the first thing a hand-written client or a future free-text field would hit.
- `tools/LootOfLima.Harness` prints `B1`, so its per-seat output no longer reads like the screens it
  is demonstrating.
- Anyone reading a server log next to a phone is translating between the two.

**Why it can wait:** nothing is wrong at runtime, and the boundary is drawn where it is cheap to
hold — one function, called from the four presentation surfaces, with the rule "anything that
addresses an element takes the id, anything that reads the screen comes back a label" written down
in `docs/technical.md` and enforced by the e2e suite. The client is where players are, so the
client is where the rename earns most of what it is worth.

**Fix, when it is worth doing:** rename in the engine, so `LocationId.ToString()` and its parse are
the box's spelling and everything downstream inherits it — then delete `locationLabel`. That is a
bigger change than it sounds and is the reason it is not this change: the 239 committed scenario
files and `board-layout.json` are keyed by the id, the oracle checks 244,736 answers against them,
and `LocationId` round-trips through `ToString`/parse on the wire. Doing it properly means the seed
data and the importer move together with the engine, in one commit, with the oracle as the proof.
Accepting both spellings on the way in would leave the app permanently ambiguous about which it
means, so it should be a rename rather than a widening.
