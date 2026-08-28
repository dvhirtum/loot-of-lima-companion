/**
 * The wire contract, as TypeScript sees it.
 *
 * Every type here mirrors one in `LootOfLima.Contracts`, and nothing else in this app is allowed
 * to invent a shape the server does not send. Directions, terrains and locations arrive as plain
 * strings because `Contracts` deliberately cannot reference the rules engine, so the client reads
 * them the same way the server does: by name.
 *
 * There is no client-side rules logic anywhere below. The server sends a complete, already
 * projected view on every change and this app renders it, which is the last of the five layers the
 * hidden-information design rests on -- a client that holds no rules cannot derive what it was not
 * told, even in principle.
 */

/** Compass directions in declaration order, which is clockwise. */
export const DIRECTIONS = ['N', 'NE', 'E', 'SE', 'S', 'SW', 'W', 'NW'] as const;

export type Direction = (typeof DIRECTIONS)[number];

export const TERRAINS = ['Beach', 'Forest', 'Mountain'] as const;

export type Terrain = (typeof TERRAINS)[number];

/** The phases a game moves through, in order. */
export type Phase = 'Lobby' | 'Dealing' | 'Passing' | 'Announcing' | 'InProgress' | 'Finished';

export type SeatKind = 'Human' | 'Bot';

export type AbilityType = 'Barrel' | 'Shovel' | 'Pistol';

// --- REST -------------------------------------------------------------------

export interface CreateGameRequest {
  readonly playerCount: number;
}

export interface CreateGameResponse {
  readonly gameId: string;
  readonly roomCode: string;
  /** What the QR code encodes. Handed back by the server, which alone knows a reachable address. */
  readonly joinUrl: string;
}

export interface LobbySeat {
  readonly index: number;
  readonly displayName: string;
  readonly taken: boolean;
}

export interface LobbySummary {
  readonly roomCode: string;
  readonly phase: Phase;
  readonly playerCount: number;
  readonly seats: readonly LobbySeat[];
}

export interface JoinRequest {
  readonly displayName: string;
}

export interface JoinResponse {
  /** A bearer credential: whoever holds it holds the seat. It lives in this browser and nowhere else. */
  readonly playerToken: string;
  readonly playerId: string;
}

export interface ScenarioSummary {
  readonly id: string;
  readonly book: number;
  readonly number: number;
  readonly playerCount: number;
}

export interface BoardSector {
  readonly direction: Direction;
  readonly locations: readonly string[];
}

export interface ArcSemantics {
  readonly startInclusive: boolean;
  readonly endExclusive: boolean;
  readonly startEqualsEndMeansFullCircle: boolean;
}

export interface BoardLayoutDto {
  readonly sectors: readonly BoardSector[];
  readonly arc: ArcSemantics;
}

// --- Hub: state -------------------------------------------------------------

export interface SeatView {
  readonly index: number;
  readonly kind: SeatKind;
  readonly displayName: string;
  readonly isViewer: boolean;
  readonly isActive: boolean;
  readonly eliminated: boolean;
  readonly handSize: number;
  /**
   * What the viewer is entitled to know is in this hand: the whole of their own, and of anybody
   * else's only what setup passing told them. Never read this as a whole hand without
   * `handIsComplete`.
   */
  readonly knownHand: readonly string[];
  readonly handIsComplete: boolean;
  readonly abilitiesRemaining: readonly AbilityType[];
  /**
   * Whether this seat spent its Pistol on the turn now in progress, and may therefore ask a secret
   * question. Public for every seat, and nothing new: spending an ability writes a public history
   * entry carrying the same turn number.
   */
  readonly pistolArmed: boolean;
  /** Announced at setup. Says nothing about whether the choice broke a tie, by design. */
  readonly fewestTerrain: Terrain | null;
  readonly tokensConfirmed: boolean;
  readonly passConfirmed: boolean;
}

/**
 * The discriminator the server writes as `type`. Redaction is by type, never by a blanked field:
 * a viewer who was not party to a secret question receives `secretQuestionOccurred`, which has no
 * property for the range or the answer at all.
 */
export type HistoryEntryType =
  | 'publicQuestion'
  | 'secretQuestion'
  | 'secretQuestionOccurred'
  | 'abilityUsed'
  | 'tokenPass'
  | 'tokenPassOccurred'
  | 'accusation'
  | 'accusationOccurred'
  | 'fewestTerrainAnnounced'
  | 'gameEnded';

interface HistoryEntryBase {
  readonly type: HistoryEntryType;
  readonly sequence: number;
  readonly turnNumber: number;
}

export interface PublicQuestionView extends HistoryEntryBase {
  readonly type: 'publicQuestion';
  readonly askerSeat: number;
  readonly targetSeat: number;
  readonly start: Direction;
  readonly end: Direction;
  readonly terrain: Terrain | null;
  readonly answer: number;
}

export interface SecretQuestionView extends HistoryEntryBase {
  readonly type: 'secretQuestion';
  readonly askerSeat: number;
  readonly targetSeat: number;
  readonly start: Direction;
  readonly end: Direction;
  readonly terrain: Terrain | null;
  readonly answer: number;
}

export interface SecretQuestionOccurredView extends HistoryEntryBase {
  readonly type: 'secretQuestionOccurred';
  readonly askerSeat: number;
  readonly targetSeat: number;
}

export interface AbilityUsedView extends HistoryEntryBase {
  readonly type: 'abilityUsed';
  readonly seatIndex: number;
  readonly ability: AbilityType;
}

export interface TokenPassView extends HistoryEntryBase {
  readonly type: 'tokenPass';
  readonly giverSeat: number;
  readonly receiverSeat: number;
  readonly location: string;
  readonly direction: string;
}

export interface TokenPassOccurredView extends HistoryEntryBase {
  readonly type: 'tokenPassOccurred';
  readonly giverSeat: number;
  readonly receiverSeat: number;
  readonly direction: string;
}

export interface AccusationView extends HistoryEntryBase {
  readonly type: 'accusation';
  readonly accuserSeat: number;
  readonly named: readonly string[];
  readonly correct: boolean;
}

export interface AccusationOccurredView extends HistoryEntryBase {
  readonly type: 'accusationOccurred';
  readonly accuserSeat: number;
}

export interface FewestTerrainAnnouncedView extends HistoryEntryBase {
  readonly type: 'fewestTerrainAnnounced';
  readonly seatIndex: number;
  readonly terrain: Terrain;
}

export interface GameEndedView extends HistoryEntryBase {
  readonly type: 'gameEnded';
  readonly winnerSeat: number | null;
  readonly reason: string;
}

export type HistoryEntryView =
  | PublicQuestionView
  | SecretQuestionView
  | SecretQuestionOccurredView
  | AbilityUsedView
  | TokenPassView
  | TokenPassOccurredView
  | AccusationView
  | AccusationOccurredView
  | FewestTerrainAnnouncedView
  | GameEndedView;

/**
 * Everything one viewer is entitled to know, and nothing else. The only shape of game state that
 * ever leaves the server.
 */
export interface PlayerView {
  readonly gameId: string;
  readonly roomCode: string;
  readonly phase: Phase;
  readonly playerCount: number;
  /** The seat this view was built for, or null for a spectator. */
  readonly viewerSeat: number | null;
  readonly activeSeat: number | null;
  readonly turnNumber: number;
  readonly seats: readonly SeatView[];
  readonly publicReveal: readonly string[];
  /** Null in every phase but Finished. The field the whole design exists to protect. */
  readonly treasure: readonly string[] | null;
  readonly history: readonly HistoryEntryView[];
  readonly winnerSeat: number | null;
  readonly endReason: string | null;
  /**
   * Which revision of the game this view was built from. Commands are ordered, but the sends that
   * follow them are not, so a frame can overtake its successor on the way out; keeping the highest
   * version seen and dropping anything older is what makes that harmless.
   */
  readonly version: number;
}

// --- Hub: events and errors -------------------------------------------------

/**
 * Every event kind the server sends. There is deliberately no kind for a turn passing or a game
 * ending: both are plain from the view arriving alongside, and an event is a nudge to animate or
 * chime, never a source of state.
 */
export const EVENT_KINDS = [
  'SeatClaimed',
  'SeatReleased',
  'GameStarted',
  'FewestTerrainAnnounced',
  'QuestionAsked',
  'AbilitySpent',
  'AccusationMade',
] as const;

export type EventKind = (typeof EVENT_KINDS)[number];

export interface GameEventDto {
  readonly kind: EventKind;
  readonly seatIndex: number | null;
}

export interface ErrorDto {
  readonly code: string;
  readonly message: string;
}

// --- Hub: requests ----------------------------------------------------------

export interface AskQuestionRequest {
  readonly targetSeat: number;
  readonly start: Direction;
  readonly end: Direction;
  readonly terrain: Terrain | null;
}

/** A Pistol question. Shaped like an ordinary one: the ability frees the range, not the shape. */
export interface AskSecretQuestionRequest {
  readonly targetSeat: number;
  readonly start: Direction;
  readonly end: Direction;
  readonly terrain: Terrain | null;
}

export interface AccuseRequest {
  readonly first: string;
  readonly second: string;
}

export interface StartGameRequest {
  /** An official scenario such as `b1s1`, or null to have the app generate a deal. */
  readonly scenarioId: string | null;
}
