# Headless harness

The M2 demo. Plays two scripted games through the rules engine — no server, no UI — and prints the
result **once per seat**, so the hidden-information design can be read straight off the terminal
instead of inferred from the test names.

```
dotnet run --project tools/LootOfLima.Harness
dotnet run --project tools/LootOfLima.Harness -- --scenario b3s7 --seed 12345
```

| Option | Default | Meaning |
|---|---|---|
| `--scenario` | `b1s1` | Which printed scenario to replay in the first game |
| `--seed` | `20260825` | Seed for the generated second game, and for its passing |

## What the two games show

**Game 1 — a printed scenario.** Five seats, one human, four bots, no token passing: the puzzle the
solo books pose. The setup announcements come straight from the book's printed letters. Four dice
questions, then a Pistol, then the winning accusation.

**Game 2 — a generated four-player deal.** Two humans, so setup passing happens (`+2` seats, one
token each), the players announce their own fewest terrain, and a wrong accusation eliminates its
author, handing the win to the last player standing.

## What to look for

Scroll to `--- what each seat is allowed to see ---` and compare the same line across seats:

- The **secret question** reads `SECRET … SE->NE -> 2` for its asker and its target, and
  `secret … asked seat 1 something (redacted)` for everybody else.
- Each **token pass** names its location for the two endpoints only; everyone else sees
  `gave something to seat N`.
- The **failed accusation** in game 2 shows the pair to the accuser and `(pair redacted)` to
  everyone else.
- **`treasure hidden`** on every seat until the game reaches `Finished`.

The `fewest terrain announced` block marks which choices broke a tie. That annotation is drawn from
the hands the harness can see, **not** from anything a player is told: the rulebook says a player
breaking a tie must not reveal there was one, so no projection carries it. In `b1s1`, seat 1 holds
`F1 F2 F3 F8` — no beach and no mountain — and the book chose mountain out of that tie, which is
exactly why the printed letters are carried through rather than recomputed.

These are the same properties `LootOfLima.Projection.Tests` asserts. The harness exists because a
person can check them at a glance, and because it exercises the engine end to end in a way no single
unit test does.
