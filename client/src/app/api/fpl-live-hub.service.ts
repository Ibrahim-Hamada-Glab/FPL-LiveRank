import { Injectable, NgZone, inject, signal } from '@angular/core';
import {
  HubConnection,
  HubConnectionBuilder,
  HubConnectionState,
  LogLevel,
} from '@microsoft/signalr';
import { Subject } from 'rxjs';
import { environment } from '../../environments/environment';
import { ManagerLive } from './manager-live.types';
import { LeagueLiveRank } from './league-live.types';

export type LiveHubConnectionStatus = 'disconnected' | 'connecting' | 'connected' | 'reconnecting';

export interface RefreshProgressUpdate {
  scope: string;
  status: 'started' | 'completed' | 'failed' | 'skipped' | string;
  detail: string | null;
}

export interface EventLiveRefreshedUpdate {
  eventId: number;
  refreshedAtUtc: string;
}

@Injectable({ providedIn: 'root' })
export class FplLiveHubService {
  private readonly zone = inject(NgZone);
  private readonly managerUpdatesSubject = new Subject<ManagerLive>();
  private readonly leagueUpdatesSubject = new Subject<LeagueLiveRank>();
  private readonly eventRefreshesSubject = new Subject<EventLiveRefreshedUpdate>();
  private readonly refreshProgressSubject = new Subject<RefreshProgressUpdate>();
  private readonly joinedLeagues = new Set<number>();
  private readonly joinedManagers = new Set<number>();
  private readonly joinedEvents = new Set<number>();

  private connection?: HubConnection;
  private startPromise?: Promise<void>;

  readonly connectionStatus = signal<LiveHubConnectionStatus>('disconnected');
  readonly managerUpdates$ = this.managerUpdatesSubject.asObservable();
  readonly leagueUpdates$ = this.leagueUpdatesSubject.asObservable();
  readonly eventRefreshes$ = this.eventRefreshesSubject.asObservable();
  readonly refreshProgress$ = this.refreshProgressSubject.asObservable();

  async joinLeague(leagueId: number): Promise<void> {
    this.joinedLeagues.add(leagueId);
    await this.joinGroup('JoinLeague', leagueId);
  }

  async leaveLeague(leagueId: number): Promise<void> {
    this.joinedLeagues.delete(leagueId);
    if (this.connection?.state === HubConnectionState.Connected) {
      await this.invoke('LeaveLeague', leagueId);
    }
  }

  async joinManager(managerId: number): Promise<void> {
    this.joinedManagers.add(managerId);
    await this.joinGroup('JoinManager', managerId);
  }

  async leaveManager(managerId: number): Promise<void> {
    this.joinedManagers.delete(managerId);
    if (this.connection?.state === HubConnectionState.Connected) {
      await this.invoke('LeaveManager', managerId);
    }
  }

  async joinEvent(eventId: number): Promise<void> {
    this.joinedEvents.add(eventId);
    await this.joinGroup('JoinEvent', eventId);
  }

  async leaveEvent(eventId: number): Promise<void> {
    this.joinedEvents.delete(eventId);
    if (this.connection?.state === HubConnectionState.Connected) {
      await this.invoke('LeaveEvent', eventId);
    }
  }

  private async ensureConnected(): Promise<void> {
    if (this.connection?.state === HubConnectionState.Connected) return;
    if (this.startPromise) return this.startPromise;

    if (!this.connection) {
      this.connection = this.createConnection();
    }

    this.connectionStatus.set('connecting');
    this.startPromise = this.connection
      .start()
      .then(() => this.zone.run(() => this.connectionStatus.set('connected')))
      .catch(error => {
        this.zone.run(() => this.connectionStatus.set('disconnected'));
        throw error;
      })
      .finally(() => {
        this.startPromise = undefined;
      });

    return this.startPromise;
  }

  private createConnection(): HubConnection {
    const connection = new HubConnectionBuilder()
      .withUrl(this.hubUrl())
      .withAutomaticReconnect()
      .configureLogging(environment.production ? LogLevel.Warning : LogLevel.Information)
      .build();

    connection.on('ManagerLiveScoreUpdated', (dto: ManagerLive) =>
      this.zone.run(() => this.managerUpdatesSubject.next(dto)));
    connection.on('LeagueLiveTableUpdated', (dto: LeagueLiveRank) =>
      this.zone.run(() => this.leagueUpdatesSubject.next(dto)));
    connection.on('EventLiveRefreshed', (update: EventLiveRefreshedUpdate) =>
      this.zone.run(() => this.eventRefreshesSubject.next(update)));
    connection.on('RefreshProgressUpdated', (update: RefreshProgressUpdate) =>
      this.zone.run(() => this.refreshProgressSubject.next(update)));

    connection.onreconnecting(() => this.zone.run(() => this.connectionStatus.set('reconnecting')));
    connection.onreconnected(() => {
      this.zone.run(() => this.connectionStatus.set('connected'));
      void this.rejoinGroups();
    });
    connection.onclose(() => this.zone.run(() => this.connectionStatus.set('disconnected')));

    return connection;
  }

  private async rejoinGroups(): Promise<void> {
    if (this.connection?.state !== HubConnectionState.Connected) return;

    await Promise.all([
      ...Array.from(this.joinedLeagues, id => this.invoke('JoinLeague', id)),
      ...Array.from(this.joinedManagers, id => this.invoke('JoinManager', id)),
      ...Array.from(this.joinedEvents, id => this.invoke('JoinEvent', id)),
    ]);
  }

  private async joinGroup(method: string, id: number): Promise<void> {
    try {
      await this.ensureConnected();
      await this.invoke(method, id);
    } catch (error) {
      console.warn(`SignalR ${method}(${id}) could not connect`, error);
    }
  }

  private async invoke(method: string, id: number): Promise<void> {
    try {
      await this.connection?.invoke(method, id);
    } catch (error) {
      console.warn(`SignalR ${method}(${id}) failed`, error);
    }
  }

  private hubUrl(): string {
    const apiBase = environment.apiBaseUrl.replace(/\/+$/, '');
    const appBase = apiBase.replace(/\/api$/i, '');
    return `${appBase}/hubs/fpl-live`;
  }
}
