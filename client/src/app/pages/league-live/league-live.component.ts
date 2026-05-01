import { CommonModule } from '@angular/common';
import { Component, OnDestroy, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { Subscription } from 'rxjs';
import { FplLiveHubService, RefreshProgressUpdate } from '../../api/fpl-live-hub.service';
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
export class LeagueLiveComponent implements OnDestroy {
  private readonly service = inject(LeagueLiveService);
  private readonly liveHub = inject(FplLiveHubService);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly subscriptions = new Subscription();

  private joinedLeagueId?: number;
  private joinedEventId?: number;

  leagueId = signal<string>('');
  eventId = signal<string>('');
  search = signal<string>('');
  sortKey = signal<SortKey>('liveRank');
  sortDir = signal<'asc' | 'desc'>('asc');
  state = signal<ViewState>({ kind: 'idle' });
  refreshProgress = signal<RefreshProgressUpdate | null>(null);
  lastPushAt = signal<Date | null>(null);

  readonly hubStatus = this.liveHub.connectionStatus;

  readonly liveStatusText = computed(() => {
    const progress = this.refreshProgress();
    if (progress) {
      switch (progress.status) {
        case 'started': return 'Refresh started';
        case 'completed': return 'Refresh completed';
        case 'failed': return progress.detail ? `Refresh failed: ${progress.detail}` : 'Refresh failed';
        case 'skipped': return progress.detail ? `Refresh skipped: ${progress.detail}` : 'Refresh skipped';
        default: return progress.detail ? `${progress.status}: ${progress.detail}` : progress.status;
      }
    }

    switch (this.hubStatus()) {
      case 'connected': return 'Live updates connected';
      case 'connecting': return 'Connecting live updates';
      case 'reconnecting': return 'Reconnecting live updates';
      case 'disconnected': return 'Live updates disconnected';
    }
  });

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
    this.bindLiveUpdates();
    if (routeLeagueId) {
      queueMicrotask(() => this.fetch(false));
    }
  }

  ngOnDestroy(): void {
    this.subscriptions.unsubscribe();
    void this.leaveJoinedGroups();
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
      next: data => {
        this.state.set({ kind: 'success', data });
        void this.joinLiveGroups(data.leagueId, data.eventId);
      },
      error: error => this.state.set({ kind: 'error', error }),
    });
  }

  refresh(): void {
    const id = Number.parseInt(this.leagueId(), 10);
    if (!Number.isFinite(id) || id <= 0) {
      this.state.set({ kind: 'error', error: { status: 0, title: 'Enter a valid league id.' } });
      return;
    }

    const ev = Number.parseInt(this.eventId(), 10);
    const eventArg = Number.isFinite(ev) && ev > 0 ? ev : undefined;
    this.refreshProgress.set({ scope: `league-${id}`, status: 'started', detail: null });
    void this.joinLiveGroups(id, eventArg);

    this.service.refresh(id, eventArg).subscribe({
      next: data => {
        this.state.set({ kind: 'success', data });
        this.lastPushAt.set(new Date());
        void this.joinLiveGroups(data.leagueId, data.eventId);
      },
      error: error => {
        this.refreshProgress.set({ scope: `league-${id}`, status: 'failed', detail: error.title });
        this.state.set({ kind: 'error', error });
      },
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

  liveStatusClass(): string {
    const status = this.refreshProgress()?.status;
    if (status === 'failed') return 'text-rose-300';
    if (status === 'started' || this.hubStatus() === 'connecting' || this.hubStatus() === 'reconnecting') return 'text-amber-300';
    if (status === 'completed' || this.hubStatus() === 'connected') return 'text-emerald-300';
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

  private bindLiveUpdates(): void {
    this.subscriptions.add(this.liveHub.leagueUpdates$.subscribe(data => {
      const activeLeague = Number.parseInt(this.leagueId(), 10);
      if (data.leagueId !== activeLeague) return;

      this.state.set({ kind: 'success', data });
      this.lastPushAt.set(new Date());
      this.refreshProgress.set({ scope: `league-${data.leagueId}`, status: 'completed', detail: null });
      void this.joinLiveGroups(data.leagueId, data.eventId);
    }));

    this.subscriptions.add(this.liveHub.refreshProgress$.subscribe(update => {
      const activeLeague = Number.parseInt(this.leagueId(), 10);
      if (update.scope !== `league-${activeLeague}`) return;
      this.refreshProgress.set(update);
    }));

    this.subscriptions.add(this.liveHub.eventRefreshes$.subscribe(update => {
      const s = this.state();
      if (s.kind !== 'success' || s.data.eventId !== update.eventId) return;
      this.lastPushAt.set(new Date(update.refreshedAtUtc));
    }));
  }

  private async joinLiveGroups(leagueId: number, eventId?: number): Promise<void> {
    if (this.joinedLeagueId !== leagueId) {
      if (this.joinedLeagueId !== undefined) {
        await this.liveHub.leaveLeague(this.joinedLeagueId);
      }
      this.joinedLeagueId = leagueId;
      await this.liveHub.joinLeague(leagueId);
    }

    if (eventId !== undefined && this.joinedEventId !== eventId) {
      if (this.joinedEventId !== undefined) {
        await this.liveHub.leaveEvent(this.joinedEventId);
      }
      this.joinedEventId = eventId;
      await this.liveHub.joinEvent(eventId);
    }
  }

  private async leaveJoinedGroups(): Promise<void> {
    if (this.joinedLeagueId !== undefined) {
      await this.liveHub.leaveLeague(this.joinedLeagueId);
    }
    if (this.joinedEventId !== undefined) {
      await this.liveHub.leaveEvent(this.joinedEventId);
    }
  }
}
