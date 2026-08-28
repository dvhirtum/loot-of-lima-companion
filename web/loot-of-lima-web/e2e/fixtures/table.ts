import { Browser, BrowserContext, Page, expect } from '@playwright/test';

/**
 * The journey every spec starts with, written once.
 *
 * These drive the app the way a table does -- open a table, join it, sit down, deal -- and they
 * assert nothing beyond what they need to make the next step safe. A helper that asserted the
 * product would put the interesting claims somewhere no spec names them.
 *
 * Everything goes through the UI on purpose. Reaching for the REST API to set a game up would test
 * a path no player takes and would skip the client that is supposed to be under test.
 */

/** A player at a table: their own browser context, which is what makes their storage their own. */
export interface Player {
  readonly name: string;
  readonly page: Page;
  readonly context: BrowserContext;
  readonly seatIndex: number;
}

/** Opens a table and returns its room code, read from the URL the host screen lands on. */
export async function openTable(page: Page, playerCount: 3 | 4 | 5): Promise<string> {
  await page.goto('/new');
  await page.getByRole('button', { name: String(playerCount), exact: true }).click();
  await page.getByRole('button', { name: 'Open the table' }).click();

  await page.waitForURL(/\/host\/[A-Z]+$/);

  const roomCode = page.url().split('/').pop();
  if (roomCode === undefined || roomCode.length === 0) {
    throw new Error(`The host screen did not land on a room code: ${page.url()}`);
  }

  return roomCode;
}

/**
 * Joins a table in a browser context of its own and takes the first free seat.
 *
 * A separate context rather than a separate page, because the seat is held by a token in
 * `localStorage` and two pages of one context share it -- which would make the second player the
 * first one again, and would quietly make every two-player assertion vacuous.
 *
 * The whole journey is now one page. The room code opens the table, everybody arrives watching it,
 * and the name is asked for by the seat card at the moment a seat is taken -- so this walks the
 * route a scanned QR code actually walks, which is the reason the helper exists at all.
 *
 * Seats fill in order: the first free "Sit here" is always the lowest-numbered bot seat, so the
 * n-th player to call this sits in seat n - 1.
 */
export async function sitDown(browser: Browser, roomCode: string, name: string, seatIndex: number): Promise<Player> {
  const context = await browser.newContext();
  const page = await context.newPage();

  await page.goto(`/play/${roomCode}`);
  await page.getByRole('button', { name: 'Sit here' }).first().click();
  await page.getByLabel('Your name').fill(name);
  await page.getByRole('button', { name: 'Sit down', exact: true }).click();

  // The seat is not taken until the server says so, and every following step assumes it is.
  //
  // `exact`, and it is load-bearing rather than tidy. `getByText` matches case-insensitive
  // substrings, and the name field this card now opens is captioned "Your name" -- which contains
  // "you". Loosely matched, this went green the instant the field appeared, so `sitDown` returned
  // before the claim had reached the server and the caller dealt at a table the player was not yet
  // sitting at. It surfaced three files away, as a wait for a "Ready" button on a page that had
  // never been given a seat.
  await expect(seatCard(page, seatIndex).getByText('you', { exact: true })).toBeVisible();

  // And the name in the seat's own heading, which only the server can have put there. The tag above
  // says this card is the viewer's; this says the table has been told who the viewer is.
  await expect(seatCard(page, seatIndex).locator('strong')).toHaveText(name);

  return { name, page, context, seatIndex };
}

/**
 * The select in the field with this caption.
 *
 * Not `getByLabel`, and the reason is a trap worth naming: every select here sits *inside* its
 * `<label>`, so the accessible name is the whole label's text -- caption and every option. The
 * scenario picker's name is "Deal Shuffle a new deal Book 1, scenario 1 ...", which no exact match
 * will ever find, and which a substring match finds for reasons that have nothing to do with the
 * caption. Anchoring on the caption says what is meant and does not change when an option is added.
 */
export function field(page: Page, caption: string): ReturnType<Page['locator']> {
  return page
    .locator('label.field')
    .filter({ hasText: new RegExp(`^\\s*${caption}`) })
    .locator('select');
}

/**
 * Starts the game on the named scenario, or on a shuffled deal when given null.
 *
 * The scenario is chosen as a book and a number, which is what is printed on the booklet somebody
 * is holding. A shuffled deal is the default, so passing null touches nothing.
 */
export async function deal(page: Page, scenario: { book: number; number: number } | null): Promise<void> {
  if (scenario !== null) {
    await page.getByRole('button', { name: 'Play a printed scenario' }).click();
    await field(page, 'Book').selectOption(String(scenario.book));
    await field(page, 'Scenario').selectOption(String(scenario.number));
  }

  await page.getByRole('button', { name: 'Deal and begin' }).click();
  await expect(page.getByRole('heading', { name: 'Set up behind your screen' })).toBeVisible();
}

/**
 * Confirms the deal for every seated player.
 *
 * Bots need no confirming -- the engine only waits on human seats -- so this is exactly the list of
 * people at the table.
 *
 * Named for the rulebook step rather than for what a player physically does, which is now to take
 * any handful of counters: the tokens on the boards are props, and the hand this confirms lives
 * only in the app.
 */
export async function takeTokens(players: readonly Player[]): Promise<void> {
  for (const player of players) {
    await player.page.getByRole('button', { name: 'Ready' }).click();
  }
}

/** One seat's card, addressed by the name the app gives that seat. */
export function seatCard(page: Page, seatIndex: number): ReturnType<Page['locator']> {
  return page.locator('.seat').nth(seatIndex);
}

/**
 * The tokens the viewer holds, written the way the app writes them: `1B`, not `B1`.
 *
 * Two vocabularies meet in this file, and which one a helper speaks follows from what it does.
 * Anything that *reads the screen* -- this, `faceUpTokens`, `visibleTokens` -- comes back in the
 * box's spelling, because that is what is on the screen. Anything that *addresses an element* --
 * `mapToken` through `data-location`, `accuse` through a select value -- takes the server's id,
 * because that is what the DOM is keyed by and what goes back on the wire.
 *
 * Anchored on the heading rather than taken as the first `.row` in the panel. Hand, face-up and
 * treasure are three rows in one section, and the first of them is only the hand when there *is* a
 * hand: a spectator has no hand row, so a positional read hands back the public reveal and calls it
 * somebody's hand. This returns an empty list for a viewer with no hand, which is the truth.
 *
 * The panel is waited for first. Reading tokens is a one-shot, and a spec that loops over the
 * result to prove something is absent elsewhere passes for free if the read came back early.
 *
 * Matched exactly, and that is not fussiness. `getByRole`'s `name` and CSS `:has-text` are both
 * case-insensitive *substring* matches. This once resolved to two elements and failed on strict
 * mode, because the Dealing panel was then headed "Take your tokens" and both panels are on screen
 * together in that phase. That heading has since been renamed, so the collision is gone -- but the
 * exact match stays, because the next overlap will not announce itself either. It only surfaced
 * under Docker, where the phase changes fast enough to still be showing when the read happens.
 */
export async function ownHand(page: Page): Promise<string[]> {
  await expect(page.getByRole('heading', { name: 'Your tokens', exact: true })).toBeVisible();
  return page.locator('h3:text-is("Your tokens") + .row .token').allInnerTexts();
}

/**
 * The locations dealt face up: the remainder after 22 are dealt evenly, so one at three players and
 * two at four or five. Read off the screen, so spelled `1B` like everything else a player reads.
 */
export async function faceUpTokens(page: Page): Promise<string[]> {
  await expect(page.getByRole('heading', { name: 'Face up', exact: true })).toBeVisible();
  return page.locator('h3:text-is("Face up") + .row .token').allInnerTexts();
}

/** Every history line currently on screen, newest first, as the player reads them. */
export async function historyLines(page: Page): Promise<string[]> {
  const entries = await page.locator('.log .entry').allInnerTexts();
  return entries.map((entry) => entry.replace(/\s+/g, ' ').trim());
}

/** Asks a public question and waits for the answer to reach the log. */
export async function askQuestion(
  page: Page,
  question: { of: string; from: string; upTo: string; terrain?: 'Beach' | 'Forest' | 'Mountain' },
): Promise<void> {
  await field(page, 'Ask').selectOption({ label: question.of });
  await field(page, 'From').selectOption(question.from);
  await field(page, 'Up to').selectOption(question.upTo);
  await field(page, 'Terrain').selectOption(question.terrain ?? '');

  // Counted here rather than on the way in, and that ordering is the whole point: `count` is a
  // one-shot with no auto-waiting, so reading it first raced the frame that carries the setup
  // announcements and made every question look like the first thing that had ever happened. The
  // selects above do wait, so by this line the log is whatever the server last said it was.
  const before = await page.locator('.log .entry').count();

  await page.getByRole('button', { name: 'Ask', exact: true }).click();
  await expect(page.locator('.log .entry')).toHaveCount(before + 1);
}

/**
 * Announces truthfully, picking a terrain this seat really does hold fewest of.
 *
 * The counts are on the buttons because the panel shows the player their own hand while they
 * choose. Reading them back rather than hard-coding one keeps this usable on a shuffled deal, where
 * nothing about the hand repeats between runs -- and the engine rejects an untruthful announcement,
 * so guessing would fail roughly two runs in three.
 */
export async function announceFewest(page: Page): Promise<string> {
  const buttons = page.locator('.card').filter({ hasText: 'Announce your fewest terrain' }).getByRole('button');

  // Waited for explicitly, because `allInnerTexts` is another one-shot: without this it reads an
  // empty list off a panel that has not arrived yet and the failure is a reduce over nothing,
  // which says nothing about what went wrong.
  await expect(buttons.first()).toBeVisible();

  const captions = await buttons.allInnerTexts();

  const held = captions.map((caption) => {
    const [, terrain, count] = /^(\w+)\s*\((\d+)\)/.exec(caption.replace(/\s+/g, ' ').trim()) ?? [];
    return { terrain: terrain ?? '', count: Number(count ?? Number.NaN) };
  });

  const fewest = held.reduce((best, next) => (next.count < best.count ? next : best));

  // `\s*` before the caption, not a bare `^`. `hasText` tests raw `textContent`, which keeps the
  // template's indentation, while `allInnerTexts` above returns it collapsed -- so the string read
  // out of the button a moment ago will not match an anchored pattern put back into it.
  await buttons.filter({ hasText: new RegExp(`^\\s*${fewest.terrain}`) }).click();
  return fewest.terrain;
}

/** How many tokens the table can see this seat holds. Public by the rules: hand sizes never change. */
export async function statedHandSize(page: Page, seatIndex: number): Promise<number> {
  const facts = await seatCard(page, seatIndex).locator('.seat__facts').innerText();
  return Number(/(\d+) tokens/.exec(facts)?.[1] ?? Number.NaN);
}

/**
 * Every token *chip* rendered anywhere on this page, wherever it came from.
 *
 * The island map is deliberately not included, and that is not an oversight to fix. The map draws
 * all 24 locations labelled, exactly as the printed board is labelled, and it draws them the same
 * way for everyone at the table: where a location is on the island and what terrain it is are both
 * geography, and public. Extending this to the map would make it match all 24 on every page and
 * quietly stop testing anything. What the map says is checked by `mapTokens` instead, which asks
 * whether two players see the same island rather than what one of them sees.
 *
 * Comes back in the box's spelling. Both sides of every leak assertion are read off a screen, so
 * they compare like with like; a spec that mixed a screen read against a hard-coded `B1` would
 * pass by never matching anything, which is the one way a leak test can fail silently.
 */
export async function visibleTokens(page: Page): Promise<string[]> {
  return page.locator('.token').allInnerTexts();
}

/**
 * The treasure, once somebody has earned the right to see it. Spelled `3B`, like everything read
 * off a screen.
 *
 * Anchored on its own heading and matched exactly: hand, face-up and treasure are three rows in
 * one section, and `getByRole`'s name is a case-insensitive substring match.
 */
export async function treasureTokens(page: Page): Promise<string[]> {
  await expect(page.getByRole('heading', { name: 'The treasure', exact: true })).toBeVisible();
  return page.locator('h3:text-is("The treasure") + .row .token').allInnerTexts();
}

/**
 * One location on the island map.
 *
 * Takes the server's id, `B1`. Addressed by `data-location`, which the map writes for exactly this
 * reason: the label is also drawn on the token, and a text match would find the log, the hand panel
 * and the seat strip as well.
 */
export function mapToken(page: Page, location: string): ReturnType<Page['locator']> {
  return page.locator(`.map [data-location="${location}"]`);
}

/**
 * Every token on the island, as one string apiece: id, the terrain class it is coloured by, and
 * everything it says — the hover title and the printed label.
 *
 * Written to be compared between two players rather than read against expected values, because the
 * claim worth making about this map is that there is only one of it. The map no longer knows who
 * holds anything: `IslandMap` injects the board and the ephemeral UI and not `GameStore`, so two
 * players' islands must come back byte for byte identical, and any leak reintroduced here would
 * have to make them differ.
 *
 * The `<g>`'s own classes are deliberately left out. `mark--lit` and `mark--picked` are the range
 * this viewer is typing and the locations they have tapped, which are theirs alone and legitimately
 * differ; the terrain class on the disc, the id and the text are the map itself.
 *
 * The map is waited for first, because `evaluateAll` is a one-shot with no auto-waiting and two
 * empty islands compare equal — a leak spec that raced the first frame would pass for free.
 */
export async function mapTokens(page: Page): Promise<string[]> {
  await expect(page.locator('.map .mark').first()).toBeVisible();

  return page.locator('.map .mark').evaluateAll((marks) =>
    marks.map((mark) => {
      const id = mark.getAttribute('data-location') ?? '';
      const terrain = mark.querySelector('.mark__disc')?.getAttribute('class') ?? '';
      const said = (mark.textContent ?? '').replace(/\s+/g, ' ').trim();

      return `${id} ${terrain} ${said}`;
    }),
  );
}

/** The locations lit by the range being built, which is the tokens inside the highlighted bands. */
export async function litOnMap(page: Page): Promise<string[]> {
  await expect(page.locator('.map .mark').first()).toBeVisible();

  return page
    .locator('.map .mark--lit')
    .evaluateAll((marks) => marks.map((mark) => mark.getAttribute('data-location') ?? ''));
}

/** What a screen reader is told the island is, which is the one place a map can leak silently. */
export async function mapCaption(page: Page): Promise<string | null> {
  await expect(page.locator('.map')).toBeVisible();
  return page.locator('.map').getAttribute('aria-label');
}

/**
 * The lit band segments of the question being built: one per terrain the question covers, so one
 * for a terrain question and three for a question about anywhere.
 */
export function arcPaths(page: Page): ReturnType<Page['locator']> {
  return page.locator('.map__arc');
}

/**
 * Spends one ability token.
 *
 * Named exactly, because a spent token keeps its button and renames itself "Barrel, spent" -- so an
 * exact match is also the assertion that it was still there to spend.
 */
export async function spendAbility(page: Page, ability: 'Barrel' | 'Shovel' | 'Pistol'): Promise<void> {
  await page.getByRole('button', { name: ability, exact: true }).click();
}

/** Asks the secret question the Pistol unlocked, and waits for it to reach the asker's own log. */
export async function askSecretly(
  page: Page,
  question: { of: string; from: string; upTo: string; terrain?: 'Beach' | 'Forest' | 'Mountain' },
): Promise<void> {
  await field(page, 'Ask').selectOption({ label: question.of });
  await field(page, 'From').selectOption(question.from);
  await field(page, 'Up to').selectOption(question.upTo);
  await field(page, 'Terrain').selectOption(question.terrain ?? '');

  const before = await page.locator('.log .entry').count();

  await page.getByRole('button', { name: 'Ask secretly' }).click();
  await expect(page.locator('.log .entry')).toHaveCount(before + 1);
}

/**
 * Names two locations as the treasure.
 *
 * Both clicks are the same locator, and that is not a slip: the button that opens the panel is
 * replaced by the one that confirms it, so only one "Accuse" is ever on the page at a time.
 */
export async function accuse(page: Page, first: string, second: string): Promise<void> {
  await page.getByRole('button', { name: 'Accuse', exact: true }).click();
  await field(page, 'First').selectOption(first);
  await field(page, 'Second').selectOption(second);
  await page.getByRole('button', { name: 'Accuse', exact: true }).click();
}

/**
 * The button that ends a solo game and shows the answer.
 *
 * Returned rather than clicked, because half of what is worth asserting about it is that it is not
 * there. Matched exactly: "Give up and reveal the treasure" and the panel's own prose around it
 * share most of their words, and `getByRole`'s name is a case-insensitive substring match.
 */
export function giveUpButton(page: Page): ReturnType<Page['getByRole']> {
  return page.getByRole('button', { name: 'Give up and reveal the treasure', exact: true });
}

/** Closes the contexts a spec opened. */
export async function leave(players: readonly Player[]): Promise<void> {
  for (const player of players) {
    await player.context.close();
  }
}
