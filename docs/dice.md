# The dice

Reference only. **The app does not model the roll** — the dice stay physical, and the app lets a
player construct any question and trusts that their roll permits it. Nothing in the code reads this
file. It is recorded here because the faces are not printed in any machine-readable form and were
tedious to recover.

## Faces

Three **twelve-sided** dice, 36 faces in total. Each face carries a compass direction and a colour:

| Colour | Meaning |
|---|---|
| Yellow | Beach |
| Green | Forest |
| Grey | Mountain |
| Blue | **Wild** — matches whatever terrain the other chosen die shows |

Only the four main cardinals (N, E, S, W) carry a blue face, so the set holds exactly **four wild
faces**.

| Die | Faces |
|---|---|
| 1 | `E-M`, `E-Wild`, `N-Wild`, `E-F`, `S-F`, `S-B`, `W-Wild`, `N-F`, `N-B`, `W-B`, `W-M`, `S-M` |
| 2 | `S-Wild`, `S-M`, `E-B`, `E-B`, `W-F`, `NW-B`, `SW-B`, `N-M`, `SE-F`, `N-M`, `NE-M`, `W-F` |
| 3 | `NW-B`, `SW-B`, `NE-M`, `NW-F`, `NE-B`, `NE-F`, `SE-B`, `NW-M`, `SE-M`, `SW-M`, `SW-F`, `SE-F` |

## Structure

Each die has a distinct character:

- **Die 1** carries only the four main cardinals, three faces each, and three of the four wilds
  (`N`, `E`, `W`). Every cardinal shows three of the four colours: N is missing Mountain, E is
  missing Beach, S is missing Wild, W is missing Forest.
- **Die 2** is the mixed die: each main cardinal appears twice — with `E-B`, `N-M` and `W-F` each
  duplicated — plus one face for each diagonal. It carries the remaining wild, `S-Wild`.
- **Die 3** carries only the four diagonals, all three terrains each, and no wilds.

All 24 (direction, terrain) pairs appear somewhere across the three dice, so every question is
rollable in principle.

Note that **wild is a property of the dice, not a terrain**. `LootOfLima.Rules.Terrain` therefore
has exactly three members; any future dice model needs its own
`DieFaceTerrain { Beach, Forest, Mountain, Wild }`.

## If in-app rolling is ever added

Backlogged, for players without the physical dice to hand. It would read a
`dice.json` mirroring the table above, with the same data-not-logic treatment as
`src/LootOfLima.Rules/Data/board-layout.json`.
