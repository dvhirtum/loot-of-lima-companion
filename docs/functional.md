# What the app does

A web companion for *Loot of Lima* (Allplay, 1-5 players). The physical board, screens, tokens and
dice stay on the table; the app replaces the printed scenario booklet and the bookkeeping.

`docs/rules.md` is the rulebook and the authority on how the game works. This file says what the app
does with it; [Departures from the rulebook](#departures-from-the-rulebook) lists every difference.

## Screens

| Screen | Route | What it is |
|---|---|---|
| Host | `/host` | Creates the table, shows the QR code and one **Join table** button |
| Table | `/play/{code}` | What the QR code encodes. Everybody lands here, seated or not |

There is no join page and no `/join` route. Arriving gives you no seat and no token; becoming a
player is one tap on a seat card plus the name that card then asks for. A latecomer who scans after
the deal watches the game.

A seat is offered only to somebody who has not got one. Moving seat is **Leave seat** on your own
card, then claiming another. When two people reach for one seat the server orders them and one wins;
the loser keeps their name and needs one more tap.

A spectator sees the island, the log, the seat strip, and a tokens panel saying they are watching
without a seat. No phase panel, and no command the server would refuse. In `Lobby` the seat strip
still offers them a seat.

## Phases

```
Lobby → Dealing → [Passing] → Announcing → InProgress → Finished
```

- Seats are claimed during `Lobby`. There is no `SeatClaim` phase.
- `Passing` is skipped when the deal settles it — three players, or any scenario deal.
- `Announcing` always happens, and resolves itself when the deal prescribes the answers.

## The deal

24 locations: 8 Beach `B1`-`B8`, 8 Forest `F1`-`F8`, 8 Mountain `M1`-`M8`. Two are set aside as the
treasure; the other 22 are dealt evenly and the remainder revealed publicly.

| Players | Hand | Revealed | Passing |
|---|---|---|---|
| 3 | 7 | 1 | none |
| 4 | 5 | 2 | pass one recorded token across the table (`+2` seats) |
| 5 | 4 | 2 | pass one right, then one left |

The app always owns the deal; nobody types a hand in. Official scenarios are five-player only, so 3-
and 4-player games always use generated deals.

**The physical tokens are counters.** Every player takes any handful face down, and the app says so
at setup — they do not correspond to the dealt hand. Setup passing moves no physical token either;
every seat gives as many as it receives, and the panel states what the app moved.

## Setup announcement

After passing and before play, in turn order, each player announces the terrain they hold fewest of.
Holding none of a terrain makes it the fewest. On a tie the player chooses and must not reveal that
there was a tie, so the engine checks only that an announcement is truthful and nothing records how
many terrains were eligible.

The panel shows all three terrains with the viewer's own count beside each and disables the
untruthful ones. A forced announcement opens one button but is still sent by a human.

A scenario's printed "fewest terrain" letters *are* the announcements, carried through as
`Deal.PrescribedFewestTerrain`. They are never recomputed: 656 of the books' 1195 hands are tied at
the minimum, and the book has already made the choice.

## Questions

A question names a clockwise arc and, optionally, one terrain. The arc runs **clockwise from start,
inclusive of start, exclusive of end**; coincident arrows sweep the whole circle. Sector *k* holds
`{Bk, Fk, Mk}` — sector 1 is N, through sector 8 in NW — and is the gap *clockwise* from compass
line *k*, so the compass letters sit on the eight lines, not in the middle of the sectors.

The app words this exactly: "clockwise from N up to, but not including, SE".

**The roll decides only which terrains a question may name.** Two matching terrains means that
terrain; two different terrains means all three; blue faces are wild. So a question is about one
terrain or about all three, never two — `QueryRange.TerrainFilter` is a single nullable terrain.

The app answers on behalf of bot **and** human players, from the hand it dealt. Bots never take
turns; they only answer.

## Abilities

Once each per game, spent on your own turn while constructing your question:

| Ability | Effect |
|---|---|
| Barrel | Re-roll the dice |
| Shovel | Treat a die as another terrain |
| Pistol | Secret question, free range |

The app does not model the roll, so Barrel and Shovel are only marked as spent. `docs/dice.md`
records the die faces; no code reads them.

A Pistol question may name a terrain. The Pistol lifts the *roll*; the one shape it leaves illegal
is naming two terrains, which the engine cannot express anyway.

## What each viewer is told

Own hand, face-up locations, public history, pass knowledge, ability status. **Nothing is
auto-solved** — the player deduces.

Three kinds of history entry are private, and a non-participant gets a *different entry type* with
no field for the secret at all:

| Entry | What a non-participant gets |
|---|---|
| Secret question | No range, no terrain, no answer |
| Token pass | No location |
| **Failed** accusation | No named pair |

The treasure is filled in only in `Finished`.

## Accusation, elimination and the end

Accusation is legal at any time, even off-turn. A wrong accusation reveals nothing and eliminates
the accuser, who must still answer questions.

- **Two humans:** a wrong accusation ends the game — one survivor is last-player-standing.
- **One human (solo):** nobody is eliminated. The seat stays in, the phase stays `InProgress`, the
  pair is ruled out, and the turn advances as after any other action on your own turn, carrying an
  armed Pistol with it.
- **Giving up** reveals the treasure, and is refused until that seat has accused and been wrong at
  least once. It is the only other way out of a solo game.

Bots are never eliminated and never take turns, so play continuing after an elimination needs a
third human at the table.

## The game page

One layout at every width. The panels are the grid's own children in the order a player uses them:

```
island → what this phase wants from you → history → abilities → accusation → hand
```

No `order` declaration anywhere, so DOM, reading, tab and painted order agree. The width decides
only the column count: on a laptop the island takes `grid-row: span 2`, putting the question builder
and the log beside it rather than below.

The way off the page is a link on the header's own line. Leaving releases no seat — the seat is a
token in that browser's storage. The finished banner is the one place a fresh game is offered.

The accusation is a folded panel behind one button, not a modal: its two locations are named by
tapping the island.

## The island map

Three coloured bands and 24 terrain-coloured tokens, identical on every screen at the table. It
shows where each location is and what it is made of, and says nothing about who holds what.

The highlight follows the question being built, per terrain: a terrain question lights that band and
its tokens inside the arc, a question about anywhere lights all three.

## Locations are shown the way the box stamps them

The id is `B1`; the token is stamped `1B`, and that is what a player sees. The flip happens only in
`core/locations.ts`'s `locationLabel`. The engine, `board-layout.json`, the 239 scenario files, the
wire, an incoming accusation and the map's `data-location` all say `B1`.

## Scenarios

239 of the 240 printed scenarios ship as seed data, chosen as a book and a number. A scenario deal
never passes tokens, even at five players.

`b16s12` is absent: book 16 prints scenario 12's tokens as a duplicate of scenario 11's,
contradicting its own question table.

## Departures from the rulebook

Each follows from the app owning the deal, the app not modelling the dice, or a table size the main
rulebook does not cover.

| Departure | The book | The app |
|---|---|---|
| Physical tokens | Each player slots their own dealt tokens into their screen | Everybody takes any handful face down |
| Setup passing | A named token changes hands | The app moves it; nothing physical moves |
| A wrong accuser | Looks at the set-aside tokens, so has seen the answer | Is told only right or wrong |
| First player | Whoever most recently dug something up | The lowest-numbered human seat |
| Solo wrong accusation | Not covered — the book is 3-5 players | Eliminates nobody; the pair is ruled out |
| Barrel and Shovel | Change the roll | Marked as spent; the app does not model the roll |
