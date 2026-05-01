import { Routes } from '@angular/router';

export const routes: Routes = [
  {
    path: '',
    loadComponent: () =>
      import('./pages/manager-live/manager-live.component').then(m => m.ManagerLiveComponent),
  },
  {
    path: 'league/:id',
    loadComponent: () =>
      import('./pages/league-live/league-live.component').then(m => m.LeagueLiveComponent),
  },
  {
    path: 'league/:id/eo',
    loadComponent: () =>
      import('./pages/league-effective-ownership/league-effective-ownership.component').then(m => m.LeagueEffectiveOwnershipComponent),
  },
  { path: '**', redirectTo: '' },
];
