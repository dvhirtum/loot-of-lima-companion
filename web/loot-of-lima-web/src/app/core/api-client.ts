import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import {
  BoardLayoutDto,
  CreateGameRequest,
  CreateGameResponse,
  JoinRequest,
  JoinResponse,
  LobbySummary,
  ScenarioSummary,
} from './contracts';

/**
 * The handful of things that happen before a game has a connection.
 *
 * Everything after that is the hub's, because everything after that is game state, and game state
 * reaches this client through exactly one channel. Nothing here returns a hand, a deal or a
 * history: a lobby summary is what is written on the outside of the box.
 */
@Injectable({ providedIn: 'root' })
export class ApiClient {
  private readonly http = inject(HttpClient);

  createGame(playerCount: number): Promise<CreateGameResponse> {
    const body: CreateGameRequest = { playerCount };
    return firstValueFrom(this.http.post<CreateGameResponse>('/api/games', body));
  }

  lobby(roomCode: string): Promise<LobbySummary> {
    return firstValueFrom(this.http.get<LobbySummary>(`/api/games/${encodeURIComponent(roomCode)}`));
  }

  join(roomCode: string, displayName: string): Promise<JoinResponse> {
    const body: JoinRequest = { displayName };
    return firstValueFrom(this.http.post<JoinResponse>(`/api/games/${encodeURIComponent(roomCode)}/join`, body));
  }

  scenarios(): Promise<readonly ScenarioSummary[]> {
    return firstValueFrom(this.http.get<readonly ScenarioSummary[]>('/api/scenarios'));
  }

  board(): Promise<BoardLayoutDto> {
    return firstValueFrom(this.http.get<BoardLayoutDto>('/api/board'));
  }

  /**
   * Where the host screen points its `img` tag.
   *
   * A URL rather than bytes, because the server renders the code and the browser is perfectly good
   * at fetching an image. Drawing it here would mean this page assembling the address players
   * scan, and the server is the only thing that knows an address they can actually reach.
   */
  qrCodeUrl(roomCode: string): string {
    return `/api/games/${encodeURIComponent(roomCode)}/qr.png`;
  }
}
