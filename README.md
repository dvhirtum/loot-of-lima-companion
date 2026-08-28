# Loot of Lima companion

A web companion for the board game *Loot of Lima* (Allplay, 1-5 players). Players scan a QR code,
land on the table, claim a seat, and see their own information on an island map. The app owns the
deal, routes questions, answers on behalf of bot and human players, tracks the special abilities,
and keeps secret questions secret.

It is a companion, not a replacement. The physical board, screens, tokens and dice stay on the
table; the app replaces the printed scenario booklet and the bookkeeping.

Angular 22 + .NET 10.

## Running it

```
docker compose up --build     # the whole thing at http://localhost:4200
```

Either half swaps for a dev server by stopping only that container:

```
docker compose stop web   →   cd web/loot-of-lima-web && npm start
docker compose stop api   →   dotnet run --project src/LootOfLima.Server
```

## Documentation

- [docs/functional.md](docs/functional.md) — what the app does: phases, the deal, questions,
  abilities, what each viewer is told, and every departure from the rulebook.
- [docs/technical.md](docs/technical.md) — how it is built: layout, invariants, running it locally
  and in Docker, the test procedure, and the conventions.
- [docs/rules.md](docs/rules.md) — the rulebook, transcribed from the physical copy, and the
  authority on how the game works.
- [docs/known-issues.md](docs/known-issues.md) — defects that are understood and deliberately
  unfixed.
- [docs/dice.md](docs/dice.md) — the die faces, for reference. No code reads them.

## The pieces

- [web/loot-of-lima-web](web/loot-of-lima-web/README.md) — the Angular client, its dev server and
  its container.
- [tools/LootOfLima.Harness](tools/LootOfLima.Harness/README.md) — headless demo. Replays two
  scripted games and prints the result once per seat.
- [tools/LootOfLima.ScenarioImporter](tools/LootOfLima.ScenarioImporter/README.md) — offline
  decoder for the official scenario books. Its output is committed.
