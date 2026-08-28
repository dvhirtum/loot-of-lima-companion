# Loot of Lima — server

Minimal API + SignalR hub. Holds every game in memory, decides who is allowed to do what, and sends
each connection a view built for the seat behind it. There is no Swagger; the integration tests in
`tests/LootOfLima.Server.Tests` are the contract.

```
dotnet run --project src/LootOfLima.Server     # http://*:5000
```

`launchSettings.json` binds `http://*:5000` rather than `localhost`, so nginx can reach it through
`host.docker.internal`. The container listens on 8080 and compose publishes it as host port 5000.

## REST

Everything before the socket opens: making a table, getting into one, and the two pieces of static
reference data the client draws with.

| | |
|---|---|
| `POST /api/games` | Create a game. Returns the game id, the room code and the table URL |
| `GET /api/games/{roomCode}` | Lobby summary — what is written on the outside of the box |
| `POST /api/games/{roomCode}/join` | Issue a player token for this browser |
| `GET /api/games/{roomCode}/qr.png` | The QR code, encoding the table URL |
| `GET /api/scenarios` | The 239 official scenarios, by book and number |
| `GET /api/board` | The board layout the island map is drawn from |
| `GET /health` | `{ "status": "ok" }` |

The table URL in the QR code is built from the request's `Host` header, so every proxy in front of
this server forwards it unchanged.

## The hub

`/hub/game`. A client calls `Rejoin(roomCode, playerToken)` first; the token decides whether the
connection is a seat or a spectator, and everything after it is refused on that basis.

Server → client: **`State`** (a `PlayerView`), **`Event`**, **`Error`**.

Client → server: `ClaimSeat`, `ReleaseSeat`, `StartGame`, `ConfirmTokensTaken`, `ConfirmPassDone`,
`AnnounceFewestTerrain`, `SpendAbility`, `AskQuestion`, `AskSecretQuestion`, `Accuse`, `GiveUp`.

## How hidden information stays hidden

Four separate mechanisms, in the order they take effect:

1. **`Contracts` cannot see `Rules`**, so no hub signature can name `GameState`, `Deal` or `Seat`.
2. **`Projector.Project(state, viewerSeat)`** is the only thing that turns state into a DTO.
3. **`GameBroadcaster` sends per connection**, for that connection's viewer seat. `Clients.All`,
   `Clients.Group` and `Clients.Others` are never used for state.
4. **`HiddenStateGuard`** sits at the front of the serializer's resolver chain, at both exits — the
   hub's JSON protocol and the minimal API's — and refuses to give `Rules` types contract metadata.
   It catches what no compiler check can: secret state arriving as `object`.

`WiringTests` asserts the guard is installed at both exits; the leak tests read the raw bytes of
every frame.

## What is in memory

`IGameStore`, `IPlayerTokenStore` and `IConnectionRegistry` are all in-memory singletons, so
restarting the process ends every game. `InMemoryGameStore.MutateAsync` holds a per-game semaphore,
which is what makes two players reaching for one seat resolve to one winner.

Room codes are four characters from a 24-letter alphabet; I and O are left out because they are read
aloud at a table. Nothing is ever evicted — see [known issue 4](../../docs/known-issues.md).

`Dockerfile` builds the container half. See [docs/technical.md](../../docs/technical.md) for how the
two containers meet on the host and how either one swaps for a dev server.
