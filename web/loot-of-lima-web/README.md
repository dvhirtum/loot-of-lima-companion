# Loot of Lima — web client

Angular 22, standalone components, zoneless, signals, Vitest. See the repository's `CLAUDE.md` for
the project as a whole.

## Running it

The quickest way is not to run this at all: `docker compose up --build` from the repository root
serves the built app at <http://localhost:4200> with the server behind it. Stop the `web` container
and `npm start` takes its place on the same port.

Otherwise, two processes. The dev server proxies `/api` and `/hub` to Kestrel so the browser sees
one origin, which the hub needs.

```
dotnet run --project ../../src/LootOfLima.Server   # :5000
npm start                                          # :4200
```

`Dockerfile`, `nginx.conf` and `nginx-proxy.conf` are the container's half of the same job:
`nginx.conf` sets up the two proxies `proxy.conf.json` sets up for the dev server, and serves the
built bundle with a SPA fallback so refreshing on `/play/ABCD` is a running game rather than a 404.

To play from a phone on the same wifi, use `npm run start:lan` and open the host screen on the
laptop — the QR code encodes an address built from the request's `Host` header, so it points at
whatever address the phone actually reached.

```
npm test     # Vitest
npm run build
```

## The e2e suite

```
npm run e2e         # boots `dotnet run` and `ng serve` itself, then drives Chromium
npm run e2e:docker  # the same specs against `docker compose up -d`
```

`e2e/` is the only suite that drives a browser, and the only one that can see what a player
actually sees. It asserts nothing a cheaper suite already covers — no arc arithmetic, no oracle, no
frame-level redaction, since `tests/LootOfLima.Server.Tests` checks the raw bytes of every frame and
is far harder to fool than the DOM.

What it is for is the round trips nothing else reaches: reloading mid-game and getting the seat back
out of `localStorage`, two browser contexts at one table proving that neither player's screen shows
the other's hand, the book's own `F M F F M` on screen, and `/play/ABCD` served on a refresh rather
than 404'd.

Two things to know before adding a spec. Value assertions belong to five-player scenario games only,
because a generated deal is seeded per run and nothing about it repeats — 3- and 4-player specs
assert structure. And `e2e/fixtures/table.ts` holds the journey helpers; `field()` in particular
exists because every `<select>` here sits inside its `<label>`, which makes its accessible name the
caption plus every option and `getByLabel` useless.

## How it is put together

- `core/contracts.ts` mirrors `LootOfLima.Contracts` by hand. It is the only place the client
  describes the wire.
- `core/game-store.ts` holds the newest `PlayerView` and slices it. It is a holder, not a reducer:
  there is no client-side rules logic anywhere in this app, which is the last of the five layers
  the hidden-information design rests on.
- `core/game-connection.ts` owns the one `HubConnection` and turns commands into hub calls. State
  arrives on `State` and nowhere else.
- `pages/` is one folder per route, all lazily loaded. `play/` is what the game page is made of.

The server has no Swagger by design; `tests/LootOfLima.Server.Tests` is the API contract.
