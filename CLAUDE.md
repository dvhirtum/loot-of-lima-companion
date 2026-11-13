# Loot of Lima companion app

A web companion for the board game
*[Loot of Lima](https://www.allplay.com/board-games/loot-of-lima)* (Allplay, 1-5 players).
Players scan a QR code, claim a seat, and see their own information on the island map. The app owns
the deal, routes questions, answers on behalf of bot **and** human players, tracks the special
abilities, and keeps secret questions secret.

**It is a companion, not a replacement.** The physical board, screens, tokens and dice stay on the
table. The app replaces the printed scenario booklet and the bookkeeping.

Stack: Angular 22 + .NET 10. Local-only; the one workflow, `.github/workflows/docs.yml`, publishes the docs site to GitHub Pages when `docs/` changes on `main`.

## The documentation

@docs/rules.md — **the rulebook, and the authority on how the game works.** Transcribed by hand from
the physical copy; it supersedes the source PDFs and anything elsewhere that disagrees with it. It
covers 3, 4 and 5 players — solo and two-player are separate rules not in it — and it does not
settle the arc's inclusive/exclusive ends, which the oracle does.

@docs/functional.md — **what the app does**: phases, deal, questions, abilities, redaction,
accusation, the screens, and every departure from the rulebook.

@docs/technical.md — **how it is built**: layout, invariants, running it locally and in Docker, the
e2e procedure, and the conventions.

@docs/known-issues.md — defects that are understood and deliberately unfixed. Check it before
touching the importer, `BoardLayout`, or how a location is spelled.

## Build and test

```
dotnet build      # warnings are errors
dotnet test       # expect 1665 passing
dotnet run --project src/LootOfLima.Server
```

```
cd web/loot-of-lima-web
npm test          # Vitest; expect 122 passing
npm start         # dev server on :4200, proxying /api and /hub to Kestrel on :5000
npm run e2e       # Playwright; expect 50 passing. Boots both halves itself
```

`global.json` pins SDK **10.0.203**. Do not remove it.

## Making a change

Branch off `main` and open a PR; never commit to `main` directly.

**A change is finished on all four terms, and one meeting three of them is not done:**

1. `dotnet test`, `npm test` and `npm run e2e` green.
2. `docker compose up --build` still stands the whole thing up, and both images still build.
3. Both swap modes still work: `compose stop web` with `npm start`, `compose stop api` with
   `dotnet run`.
4. The documentation reflects what is now true — test counts, any new invariant.

**Terms 2 and 3 are run and reported by the user, never by you.** Say which terms were checked and
which are outstanding; never call a change done off the back of the suites alone.

Also:

- **The compose stack's default state is down; bringing it up or down is the user's call.** Run
  `docker compose ps` before `npm run e2e` and ask if anything is listed. Never run
  `docker compose down`/`up` unprompted, and never run `npm run e2e:docker`.
- **A new guard is not finished until it has been seen to fail.**
- **Documentation states what is true, briefly.** No paragraphs justifying past decisions.
