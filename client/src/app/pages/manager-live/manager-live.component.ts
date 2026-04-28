import { CommonModule } from '@angular/common';
import { Component, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';
import { ManagerLiveService } from '../../api/manager-live.service';
import { ApiError, CaptaincyStatus, ManagerLive } from '../../api/manager-live.types';

type ViewState =
  | { kind: 'idle' }
  | { kind: 'loading' }
  | { kind: 'success'; data: ManagerLive }
  | { kind: 'error'; error: ApiError };

@Component({
  selector: 'app-manager-live',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './manager-live.component.html',
})
export class ManagerLiveComponent {
  private readonly service = inject(ManagerLiveService);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);

  managerId = signal<string>('');
  leagueId = signal<string>('');
  eventId = signal<string>('');
  state = signal<ViewState>({ kind: 'idle' });

  readonly starters = computed(() => {
    const s = this.state();
    return s.kind === 'success' ? s.data.picks.filter(p => p.position <= 11) : [];
  });
  readonly bench = computed(() => {
    const s = this.state();
    return s.kind === 'success' ? s.data.picks.filter(p => p.position >= 12) : [];
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

  constructor() {
    const queryManagerId = this.route.snapshot.queryParamMap.get('managerId');
    const queryEventId = this.route.snapshot.queryParamMap.get('eventId');
    if (queryManagerId) this.managerId.set(queryManagerId);
    if (queryEventId) this.eventId.set(queryEventId);
    if (queryManagerId) {
      queueMicrotask(() => this.fetch());
    }
  }

  webName(id: number): string {
    const s = this.state();
    if (s.kind !== 'success') return `#${id}`;
    return s.data.picks.find(p => p.elementId === id)?.webName ?? `#${id}`;
  }

  fetch(): void {
    const id = Number.parseInt(this.managerId(), 10);
    if (!Number.isFinite(id) || id <= 0) {
      this.state.set({ kind: 'error', error: { status: 0, title: 'Enter a valid manager id.' } });
      return;
    }
    const ev = Number.parseInt(this.eventId(), 10);
    const eventArg = Number.isFinite(ev) && ev > 0 ? ev : undefined;

    this.state.set({ kind: 'loading' });
    this.service.getLive(id, eventArg).subscribe({
      next: data => this.state.set({ kind: 'success', data }),
      error: error => this.state.set({ kind: 'error', error }),
    });
  }

  openLeague(): void {
    const id = Number.parseInt(this.leagueId(), 10);
    if (!Number.isFinite(id) || id <= 0) {
      this.state.set({ kind: 'error', error: { status: 0, title: 'Enter a valid league id.' } });
      return;
    }

    const ev = Number.parseInt(this.eventId(), 10);
    const eventArg = Number.isFinite(ev) && ev > 0 ? ev : undefined;
    void this.router.navigate(['/league', id], {
      queryParams: eventArg ? { eventId: eventArg } : {},
    });
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
}
