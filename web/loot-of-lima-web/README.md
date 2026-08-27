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
