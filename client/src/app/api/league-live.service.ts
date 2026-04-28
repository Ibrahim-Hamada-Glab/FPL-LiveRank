import { HttpClient, HttpErrorResponse, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable, catchError, throwError } from 'rxjs';
import { environment } from '../../environments/environment';
import { ApiError } from './manager-live.types';
import { LeagueLiveRank } from './league-live.types';

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
