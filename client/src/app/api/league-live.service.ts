import { HttpClient, HttpErrorResponse, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable, catchError, throwError } from 'rxjs';
import { environment } from '../../environments/environment';
import { ApiError } from './manager-live.types';
import { LeagueEffectiveOwnership, LeagueLiveRank } from './league-live.types';

@Injectable({ providedIn: 'root' })
export class LeagueLiveService {
  private readonly http = inject(HttpClient);

  getLive(leagueId: number, eventId?: number): Observable<LeagueLiveRank> {
    let params = new HttpParams();
    if (eventId !== undefined) {
      params = params.set('eventId', String(eventId));
    }
    return this.http
      .get<LeagueLiveRank>(`${environment.apiBaseUrl}/fpl/league/${leagueId}/live`, { params })
      .pipe(catchError((err: HttpErrorResponse) => throwError(() => this.toApiError(err))));
  }

  refresh(leagueId: number, eventId?: number): Observable<LeagueLiveRank> {
    let params = new HttpParams();
    if (eventId !== undefined) {
      params = params.set('eventId', String(eventId));
    }
    return this.http
      .post<LeagueLiveRank>(`${environment.apiBaseUrl}/fpl/league/${leagueId}/refresh`, null, { params })
      .pipe(catchError((err: HttpErrorResponse) => throwError(() => this.toApiError(err))));
  }

  getEffectiveOwnership(
    leagueId: number,
    eventId?: number,
    managerId?: number,
  ): Observable<LeagueEffectiveOwnership> {
    let params = new HttpParams();
    if (eventId !== undefined) {
      params = params.set('eventId', String(eventId));
    }
    if (managerId !== undefined) {
      params = params.set('managerId', String(managerId));
    }

    return this.http
      .get<LeagueEffectiveOwnership>(`${environment.apiBaseUrl}/fpl/league/${leagueId}/effective-ownership`, {
        params,
      })
      .pipe(catchError((err: HttpErrorResponse) => throwError(() => this.toApiError(err))));
  }

  private toApiError(err: HttpErrorResponse): ApiError {
    const body = err.error ?? {};
    return {
      status: err.status,
      title: body.title ?? err.statusText ?? 'Request failed',
      detail: body.detail,
      traceId: body.traceId,
    };
  }
}
