import { Routes } from '@angular/router';

/**
 * Every route is lazy.
 *
 * A phone scanning the QR code lands on `join` and needs nothing else; the laptop hosting the
 * table lands on `host` and needs nothing else. Splitting them means neither waits for the other,
 * which matters most on the device that has just been handed a URL over a hotel wifi.
 */
export const routes: Routes = [
  {
    path: '',
    title: 'Loot of Lima',
    loadComponent: () => import('./pages/home/home').then((m) => m.Home),
  },
  {
    path: 'new',
    title: 'New game',
    loadComponent: () => import('./pages/new-game/new-game').then((m) => m.NewGame),
  },
  {
    path: 'host/:roomCode',
    title: 'Table',
    loadComponent: () => import('./pages/host/host').then((m) => m.Host),
  },
  {
    path: 'join/:roomCode',
    title: 'Join',
    loadComponent: () => import('./pages/join/join').then((m) => m.Join),
  },
  {
    path: 'play/:roomCode',
    title: 'Loot of Lima',
    loadComponent: () => import('./pages/play/play').then((m) => m.Play),
  },
  {
    path: '**',
    title: 'Lost at sea',
    loadComponent: () => import('./pages/not-found/not-found').then((m) => m.NotFound),
  },
];
