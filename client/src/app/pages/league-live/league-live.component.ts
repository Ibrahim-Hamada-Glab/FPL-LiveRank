import { CommonModule } from '@angular/common';
import { Component, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { LeagueLiveService } from '../../api/league-live.service';
import { LeagueLiveRank, LeagueLiveRankEntry } from '../../api/league-live.types';
import { ApiError } from '../../api/manager-live.types';

type ViewState =
  | { kind: 'idle' }
  | { kind: 'loading' }
  | { kind: 'success'; data: LeagueLiveRank }
  | { kind: 'error'; error: ApiError };

type SortKey = 'liveRank' | 'rankChange' | 'entryName' | 'liveGwPoints' | 'liveTotal' | 'officialRank';

@Component({
  selector: 'app-league-live',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterLink],
  templateUrl: './league-live.component.html',
})
export class LeagueLiveComponent {
  private readonly service = inject(LeagueLiveService);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);

  leagueId = signal<string>('');
  eventId = signal<string>('');
  search = signal<string>('');
  sortKey = signal<SortKey>('liveRank');
  sortDir = signal<'asc' | 'desc'>('asc');
  state = signal<ViewState>({ kind: 'idle' });

  readonly rows = computed(() => {
    const s = this.state();
    if (s.kind !== 'success') return [];

    const term = this.search().trim().toLowerCase();
    const filtered = term
      ? s.data.standings.filter(row =>
          row.entryName.toLowerCase().includes(term) ||
          row.playerName.toLowerCase().includes(term) ||
          String(row.managerId).includes(term))
      : s.data.standings;

    const direction = this.sortDir() === 'asc' ? 1 : -1;
    const key = this.sortKey();

    return [...filtered].sort((a, b) => {
      const primary = this.compare(a, b, key);
      return (primary !== 0 ? primary : a.liveRank - b.liveRank) * direction;
    });
  });

  constructor() {
    const routeLeagueId = this.route.snapshot.paramMap.get('id');
    const routeEventId = this.route.snapshot.queryParamMap.get('eventId');
    if (routeLeagueId) this.leagueId.set(routeLeagueId);
    if (routeEventId) this.eventId.set(routeEventId);
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

    const ev = Number.parseInt(this.eventId(), 10);
    const eventArg = Number.isFinite(ev) && ev > 0 ? ev : undefined;

    if (updateRoute) {
      void this.router.navigate(['/league', id], {
        queryParams: eventArg ? { eventId: eventArg } : {},
      });
    }

    this.state.set({ kind: 'loading' });
    this.service.getLive(id, eventArg).subscribe({
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
    this.sortDir.set(key === 'rankChange' || key === 'liveGwPoints' || key === 'liveTotal' ? 'desc' : 'asc');
  }

  sortMark(key: SortKey): string {
    if (this.sortKey() !== key) return '';
    return this.sortDir() === 'asc' ? '↑' : '↓';
  }

  movementText(change: number): string {
    if (change > 0) return `+${change}`;
    return String(change);
  }

  movementClass(change: number): string {
    if (change > 0) return 'text-emerald-300';
    if (change < 0) return 'text-rose-300';
    return 'text-slate-400';
  }

  private compare(a: LeagueLiveRankEntry, b: LeagueLiveRankEntry, key: SortKey): number {
    switch (key) {
      case 'liveRank': return a.liveRank - b.liveRank;
      case 'rankChange': return a.rankChange - b.rankChange;
      case 'entryName': return a.entryName.localeCompare(b.entryName);
      case 'liveGwPoints': return a.liveGwPoints - b.liveGwPoints;
      case 'liveTotal': return a.liveTotal - b.liveTotal;
      case 'officialRank': return a.officialRank - b.officialRank;
    }
  }
}
