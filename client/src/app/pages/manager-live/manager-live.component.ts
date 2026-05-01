import { CommonModule } from '@angular/common';
import { Component, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';
import { ManagerLiveService } from '../../api/manager-live.service';
import { ApiError, CaptaincyStatus, ManagerLeague, ManagerLeagues, ManagerLive, ManagerLivePick } from '../../api/manager-live.types';

type ViewState =
  | { kind: 'idle' }
  | { kind: 'loading' }
  | { kind: 'success'; data: ManagerLive }
  | { kind: 'error'; error: ApiError };

type LeagueState =
  | { kind: 'idle' }
  | { kind: 'loading' }
  | { kind: 'success'; data: ManagerLeagues }
  | { kind: 'error'; error: ApiError };

@Component({
  selector: 'app-manager-live',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './manager-live.component.html',
})
export class ManagerLiveComponent {
  private readonly managerStorageKey = 'fpl-live-rank.managerId';
  private readonly service = inject(ManagerLiveService);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);

  managerId = signal<string>('');
  savedManagerId = signal<string | null>(null);
  selectedLeagueId = signal<string>('');
  eventId = signal<string>('');
  state = signal<ViewState>({ kind: 'idle' });
  leagueState = signal<LeagueState>({ kind: 'idle' });

  readonly managerLabel = computed(() => {
    const s = this.leagueState();
    if (s.kind === 'success') {
      return s.data.teamName || s.data.playerName || `#${s.data.managerId}`;
    }
    return this.savedManagerId() ? `#${this.savedManagerId()}` : '';
  });

  readonly discoverableLeagues = computed(() => {
    const s = this.leagueState();
    if (s.kind !== 'success') return [];
    return [...s.data.classicLeagues].sort((a, b) => {
      const systemSort = Number(a.isSystemLeague) - Number(b.isSystemLeague);
      return systemSort !== 0 ? systemSort : a.name.localeCompare(b.name);
    });
  });

  readonly starters = computed(() => {
    const s = this.state();
    return s.kind === 'success' ? s.data.picks.filter(p => p.position <= 11) : [];
  });
  readonly bench = computed(() => {
    const s = this.state();
    return s.kind === 'success' ? s.data.picks.filter(p => p.position >= 12) : [];
  });
  readonly pitchLines = computed(() => {
    const starters = this.starters();
    return [
      starters.filter(p => p.elementType === 1),
      starters.filter(p => p.elementType === 2),
      starters.filter(p => p.elementType === 3),
      starters.filter(p => p.elementType === 4),
    ].filter(line => line.length > 0);
  });
  readonly topContributors = computed(() => {
    const s = this.state();
    if (s.kind !== 'success') return [];
    return [...s.data.picks]
      .filter(p => p.contributedPoints > 0)
      .sort((a, b) => b.contributedPoints - a.contributedPoints || a.position - b.position)
      .slice(0, 3);
  });
  readonly subbedOutIds = computed(() => {
    const s = this.state();
    return s.kind === 'success' ? new Set(s.data.autoSubs.map(x => x.outElementId)) : new Set<number>();
  });
  readonly subbedInIds = computed(() => {
    const s = this.state();
    return s.kind === 'success' ? new Set(s.data.autoSubs.map(x => x.inElementId)) : new Set<number>();
  });
  readonly blockedIds = computed(() => {
    const s = this.state();
    return s.kind === 'success' ? new Set(s.data.blockedStarterElementIds) : new Set<number>();
  });
  readonly calculatedAt = computed(() => {
    const s = this.state();
    return s.kind === 'success' ? s.data.calculatedAtUtc : null;
  });

  constructor() {
    const queryManagerId = this.route.snapshot.queryParamMap.get('managerId');
    const queryEventId = this.route.snapshot.queryParamMap.get('eventId');
    const storedManagerId = this.readStoredManagerId();

    if (queryManagerId) {
      this.managerId.set(queryManagerId);
      this.saveManagerId(queryManagerId);
    } else if (storedManagerId) {
      this.managerId.set(storedManagerId);
      this.savedManagerId.set(storedManagerId);
    }

    if (queryEventId) this.eventId.set(queryEventId);
    if (this.managerId()) {
      queueMicrotask(() => {
        this.loadLeagues();
        if (queryManagerId) this.fetch();
      });
    }
  }

  webName(id: number): string {
    const s = this.state();
    if (s.kind !== 'success') return `#${id}`;
    return s.data.picks.find(p => p.elementId === id)?.webName ?? `#${id}`;
  }

  managerTitle(data: ManagerLive): string {
    return data.teamName || data.playerName || `Manager #${data.managerId}`;
  }

  managerSubtitle(data: ManagerLive): string {
    if (data.teamName && data.playerName) return data.playerName;
    return `Gameweek ${data.eventId}`;
  }

  pickTone(pick: ManagerLivePick): string {
    if (this.subbedOutIds().has(pick.elementId) || this.blockedIds().has(pick.elementId)) return 'opacity-60';
    if (pick.isCaptain) return 'ring-2 ring-accent/70 bg-slate-950/95';
    if (pick.isViceCaptain) return 'ring-1 ring-sky-300/60 bg-slate-950/85';
    if (this.subbedInIds().has(pick.elementId)) return 'ring-1 ring-emerald-300/60 bg-emerald-950/70';
    return 'ring-1 ring-white/15 bg-slate-950/80';
  }

  fetch(): void {
    const id = Number.parseInt(this.managerId(), 10);
    if (!Number.isFinite(id) || id <= 0) {
      this.state.set({ kind: 'error', error: { status: 0, title: 'Enter a valid manager id.' } });
      return;
    }
    this.saveManagerId(String(id));

    const ev = Number.parseInt(this.eventId(), 10);
    const eventArg = Number.isFinite(ev) && ev > 0 ? ev : undefined;

    this.state.set({ kind: 'loading' });
    this.service.getLive(id, eventArg).subscribe({
      next: data => this.state.set({ kind: 'success', data }),
      error: error => this.state.set({ kind: 'error', error }),
    });
  }

  loadLeagues(): void {
    const id = Number.parseInt(this.managerId(), 10);
    if (!Number.isFinite(id) || id <= 0) {
      this.leagueState.set({ kind: 'error', error: { status: 0, title: 'Enter a valid manager id.' } });
      return;
    }

    this.saveManagerId(String(id));
    this.leagueState.set({ kind: 'loading' });
    this.service.getLeagues(id).subscribe({
      next: data => {
        this.leagueState.set({ kind: 'success', data });
        if (!this.selectedLeagueId() && data.classicLeagues.length > 0) {
          const firstCustom = data.classicLeagues.find(league => !league.isSystemLeague);
          this.selectedLeagueId.set(String((firstCustom ?? data.classicLeagues[0]).id));
        }
      },
      error: error => this.leagueState.set({ kind: 'error', error }),
    });
  }

  clearSavedManager(): void {
    this.managerId.set('');
    this.savedManagerId.set(null);
    this.selectedLeagueId.set('');
    this.leagueState.set({ kind: 'idle' });
    if (this.canUseLocalStorage()) {
      localStorage.removeItem(this.managerStorageKey);
    }
  }

  openSelectedLeague(): void {
    const id = Number.parseInt(this.selectedLeagueId(), 10);
    if (!Number.isFinite(id) || id <= 0) {
      this.leagueState.set({ kind: 'error', error: { status: 0, title: 'Choose a mini-league.' } });
      return;
    }

    const ev = Number.parseInt(this.eventId(), 10);
    const eventArg = Number.isFinite(ev) && ev > 0 ? ev : undefined;
    void this.router.navigate(['/league', id], {
      queryParams: eventArg ? { eventId: eventArg } : {},
    });
  }

  openLeague(league: ManagerLeague): void {
    this.selectedLeagueId.set(String(league.id));
    this.openSelectedLeague();
  }

  captaincyLabel(status: CaptaincyStatus): string {
    switch (status) {
      case 'CaptainPlayed': return 'Captain played';
      case 'Projected': return 'Projected (captain has not played)';
      case 'VicePromoted': return 'Vice promoted';
      case 'NoCaptainPoints': return 'No captain points';
    }
  }

  captaincyBadgeClass(status: CaptaincyStatus): string {
    switch (status) {
      case 'CaptainPlayed': return 'bg-emerald-600/20 text-emerald-300 ring-emerald-500/40';
      case 'Projected': return 'bg-amber-600/20 text-amber-300 ring-amber-500/40';
      case 'VicePromoted': return 'bg-sky-600/20 text-sky-300 ring-sky-500/40';
      case 'NoCaptainPoints': return 'bg-rose-600/20 text-rose-300 ring-rose-500/40';
    }
  }

  positionLabel(elementType: number): string {
    return ['—', 'GK', 'DEF', 'MID', 'FWD'][elementType] ?? '—';
  }

  private readStoredManagerId(): string | null {
    if (!this.canUseLocalStorage()) return null;
    const value = localStorage.getItem(this.managerStorageKey);
    return value && Number.parseInt(value, 10) > 0 ? value : null;
  }

  private saveManagerId(value: string): void {
    const id = Number.parseInt(value, 10);
    if (!Number.isFinite(id) || id <= 0) return;
    const normalized = String(id);
    this.savedManagerId.set(normalized);
    if (this.canUseLocalStorage()) {
      localStorage.setItem(this.managerStorageKey, normalized);
    }
  }

  private canUseLocalStorage(): boolean {
    return typeof localStorage !== 'undefined';
  }
}
