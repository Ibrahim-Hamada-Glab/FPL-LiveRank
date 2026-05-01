import { CommonModule } from '@angular/common';
import { Component, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { LeagueEffectiveOwnership, LeagueEffectiveOwnershipEntry } from '../../api/league-live.types';
import { LeagueLiveService } from '../../api/league-live.service';
import { ApiError } from '../../api/manager-live.types';

type ViewState =
  | { kind: 'idle' }
  | { kind: 'loading' }
  | { kind: 'success'; data: LeagueEffectiveOwnership }
  | { kind: 'error'; error: ApiError };

type SortKey = 'effectiveOwnershipPercent' | 'ownershipPercent' | 'captaincyPercent' | 'rankImpactPerPoint' | 'webName';

@Component({
  selector: 'app-league-effective-ownership',
  imports: [CommonModule, FormsModule, RouterLink],
  templateUrl: './league-effective-ownership.component.html',
})
export class LeagueEffectiveOwnershipComponent {
  private readonly service = inject(LeagueLiveService);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);

  leagueId = signal<string>('');
  eventId = signal<string>('');
  managerId = signal<string>('');
  search = signal<string>('');
  sortKey = signal<SortKey>('effectiveOwnershipPercent');
  sortDir = signal<'asc' | 'desc'>('desc');
  state = signal<ViewState>({ kind: 'idle' });

  readonly rows = computed(() => {
    const s = this.state();
    if (s.kind !== 'success') return [];

    const term = this.search().trim().toLowerCase();
    const filtered = term
      ? s.data.players.filter(row =>
          row.webName.toLowerCase().includes(term) ||
          String(row.elementId).includes(term) ||
          String(row.teamId).includes(term))
      : s.data.players;

    const direction = this.sortDir() === 'asc' ? 1 : -1;
    const key = this.sortKey();

    return [...filtered].sort((a, b) => {
      const primary = this.compare(a, b, key);
      return (primary !== 0 ? primary : a.elementId - b.elementId) * direction;
    });
  });

  constructor() {
    const routeLeagueId = this.route.snapshot.paramMap.get('id');
    const routeEventId = this.route.snapshot.queryParamMap.get('eventId');
    const routeManagerId = this.route.snapshot.queryParamMap.get('managerId');
    if (routeLeagueId) this.leagueId.set(routeLeagueId);
    if (routeEventId) this.eventId.set(routeEventId);
    if (routeManagerId) this.managerId.set(routeManagerId);
    if (routeLeagueId) {
      queueMicrotask(() => this.fetch(false));
    }
  }

  fetch(updateRoute = true): void {
    const id = Number.parseInt(this.leagueId(), 10);
    if (!Number.isFinite(id) || id <= 0) {
      this.state.set({ kind: 'error', error: { status: 0, title: 'Enter a valid league id.' } });
      return;
    }

    const event = Number.parseInt(this.eventId(), 10);
    const manager = Number.parseInt(this.managerId(), 10);
    const eventArg = Number.isFinite(event) && event > 0 ? event : undefined;
    const managerArg = Number.isFinite(manager) && manager > 0 ? manager : undefined;

    if (updateRoute) {
      const queryParams: { eventId?: number; managerId?: number } = {};
      if (eventArg) queryParams.eventId = eventArg;
      if (managerArg) queryParams.managerId = managerArg;
      void this.router.navigate(['/league', id, 'eo'], { queryParams });
    }

    this.state.set({ kind: 'loading' });
    this.service.getEffectiveOwnership(id, eventArg, managerArg).subscribe({
      next: data => this.state.set({ kind: 'success', data }),
      error: error => this.state.set({ kind: 'error', error }),
    });
  }

  setSort(key: SortKey): void {
    if (this.sortKey() === key) {
      this.sortDir.set(this.sortDir() === 'asc' ? 'desc' : 'asc');
      return;
    }

    this.sortKey.set(key);
    this.sortDir.set(key === 'webName' ? 'asc' : 'desc');
  }

  sortMark(key: SortKey): string {
    if (this.sortKey() !== key) return '';
    return this.sortDir() === 'asc' ? '↑' : '↓';
  }

  rankImpactClass(rankImpactPerPoint: number): string {
    if (rankImpactPerPoint > 0) return 'text-emerald-300';
    if (rankImpactPerPoint < 0) return 'text-rose-300';
    return 'text-slate-400';
  }

  private compare(a: LeagueEffectiveOwnershipEntry, b: LeagueEffectiveOwnershipEntry, key: SortKey): number {
    switch (key) {
      case 'effectiveOwnershipPercent':
        return a.effectiveOwnershipPercent - b.effectiveOwnershipPercent;
      case 'ownershipPercent':
        return a.ownershipPercent - b.ownershipPercent;
      case 'captaincyPercent':
        return a.captaincyPercent - b.captaincyPercent;
      case 'rankImpactPerPoint':
        return a.rankImpactPerPoint - b.rankImpactPerPoint;
      case 'webName':
        return a.webName.localeCompare(b.webName);
      default: {
        const exhaustive: never = key;
        return exhaustive;
      }
    }
  }
}
import { CommonModule } from '@angular/common';
import { Component, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { LeagueEffectiveOwnership, LeagueEffectiveOwnershipEntry } from '../../api/league-live.types';
import { LeagueLiveService } from '../../api/league-live.service';
import { ApiError } from '../../api/manager-live.types';

type ViewState =
  | { kind: 'idle' }
  | { kind: 'loading' }
  | { kind: 'success'; data: LeagueEffectiveOwnership }
  | { kind: 'error'; error: ApiError };

type SortKey = 'effectiveOwnershipPercent' | 'ownershipPercent' | 'captaincyPercent' | 'rankImpactPerPoint' | 'webName';

@Component({
  selector: 'app-league-effective-ownership',
  imports: [CommonModule, FormsModule, RouterLink],
  templateUrl: './league-effective-ownership.component.html',
})
export class LeagueEffectiveOwnershipComponent {
  private readonly service = inject(LeagueLiveService);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);

  leagueId = signal<string>('');
  eventId = signal<string>('');
  managerId = signal<string>('');
  search = signal<string>('');
  sortKey = signal<SortKey>('effectiveOwnershipPercent');
  sortDir = signal<'asc' | 'desc'>('desc');
  state = signal<ViewState>({ kind: 'idle' });

  readonly rows = computed(() => {
    const s = this.state();
    if (s.kind !== 'success') return [];

    const term = this.search().trim().toLowerCase();
    const filtered = term
      ? s.data.players.filter(row =>
          row.webName.toLowerCase().includes(term) ||
          String(row.elementId).includes(term) ||
          String(row.teamId).includes(term))
      : s.data.players;

    const direction = this.sortDir() === 'asc' ? 1 : -1;
    const key = this.sortKey();

    return [...filtered].sort((a, b) => {
      const primary = this.compare(a, b, key);
      return (primary !== 0 ? primary : a.elementId - b.elementId) * direction;
    });
  });

  constructor() {
    const routeLeagueId = this.route.snapshot.paramMap.get('id');
    const routeEventId = this.route.snapshot.queryParamMap.get('eventId');
    const routeManagerId = this.route.snapshot.queryParamMap.get('managerId');
    if (routeLeagueId) this.leagueId.set(routeLeagueId);
    if (routeEventId) this.eventId.set(routeEventId);
    if (routeManagerId) this.managerId.set(routeManagerId);
    if (routeLeagueId) {
      queueMicrotask(() => this.fetch(false));
    }
  }

  fetch(updateRoute = true): void {
    const id = Number.parseInt(this.leagueId(), 10);
    if (!Number.isFinite(id) || id <= 0) {
      this.state.set({ kind: 'error', error: { status: 0, title: 'Enter a valid league id.' } });
      return;
    }

    const event = Number.parseInt(this.eventId(), 10);
    const manager = Number.parseInt(this.managerId(), 10);
    const eventArg = Number.isFinite(event) && event > 0 ? event : undefined;
    const managerArg = Number.isFinite(manager) && manager > 0 ? manager : undefined;

    if (updateRoute) {
      const queryParams: { eventId?: number; managerId?: number } = {};
      if (eventArg) queryParams.eventId = eventArg;
      if (managerArg) queryParams.managerId = managerArg;
      void this.router.navigate(['/league', id, 'eo'], { queryParams });
    }

    this.state.set({ kind: 'loading' });
    this.service.getEffectiveOwnership(id, eventArg, managerArg).subscribe({
      next: data => this.state.set({ kind: 'success', data }),
      error: error => this.state.set({ kind: 'error', error }),
    });
  }

  setSort(key: SortKey): void {
    if (this.sortKey() === key) {
      this.sortDir.set(this.sortDir() === 'asc' ? 'desc' : 'asc');
      return;
    }

    this.sortKey.set(key);
    this.sortDir.set(key === 'webName' ? 'asc' : 'desc');
  }

  sortMark(key: SortKey): string {
    if (this.sortKey() !== key) return '';
    return this.sortDir() === 'asc' ? '↑' : '↓';
  }

  rankImpactClass(rankImpactPerPoint: number): string {
    if (rankImpactPerPoint > 0) return 'text-emerald-300';
    if (rankImpactPerPoint < 0) return 'text-rose-300';
    return 'text-slate-400';
  }

  private compare(a: LeagueEffectiveOwnershipEntry, b: LeagueEffectiveOwnershipEntry, key: SortKey): number {
    switch (key) {
      case 'effectiveOwnershipPercent':
        return a.effectiveOwnershipPercent - b.effectiveOwnershipPercent;
      case 'ownershipPercent':
        return a.ownershipPercent - b.ownershipPercent;
      case 'captaincyPercent':
        return a.captaincyPercent - b.captaincyPercent;
      case 'rankImpactPerPoint':
        return a.rankImpactPerPoint - b.rankImpactPerPoint;
      case 'webName':
        return a.webName.localeCompare(b.webName);
      default: {
        const exhaustive: never = key;
        return exhaustive;
      }
    }
  }
}
