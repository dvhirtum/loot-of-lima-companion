---
title: Loot of Lima companion
---

# Loot of Lima companion

A web companion for the board game
*[Loot of Lima](https://www.allplay.com/board-games/loot-of-lima)* (Allplay, 1-5 players).
Players scan a QR code, claim a seat, and see their own information on the island map. The app owns
the deal, routes questions, answers on behalf of bot and human players, tracks the special
abilities, and keeps secret questions secret.

It is a companion, not a replacement. The physical board, screens, tokens and dice stay on the
table; the app replaces the printed scenario booklet and the bookkeeping.

The app needs its .NET server, so it does not run on GitHub Pages. These pages are its
documentation. The code is on [GitHub](https://github.com/dvhirtum/loot-of-lima-companion).

## Documentation

- [What the app does](functional.md): phases, the deal, questions, abilities, what each viewer is
  told, and every departure from the rulebook.
- [How it is built](technical.md): layout, invariants, running it locally and in Docker, the test
  procedure, and the conventions.
- [The rulebook](rules.md): transcribed from the physical copy; the authority on how the game works.
- [Known issues](known-issues.md): defects that are understood and deliberately unfixed.
- [Dice](dice.md): the die faces, for reference.

## Running it

```
docker compose up --build     # the whole thing at http://localhost:4200
```
