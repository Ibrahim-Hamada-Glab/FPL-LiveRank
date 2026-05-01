import { HttpClient, HttpErrorResponse, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable, catchError, throwError } from 'rxjs';
import { environment } from '../../environments/environment';
import { ApiError, ManagerLeagues, ManagerLive } from './manager-live.types';

@Injectable({ providedIn: 'root' })
export class ManagerLiveService {
  private readonly http = inject(HttpClient);

  getLive(managerId: number, eventId?: number): Observable<ManagerLive> {
    let params = new HttpParams();
    if (eventId !== undefined) {
      params = params.set('eventId', String(eventId));
    }
    return this.http
      .get<ManagerLive>(`${environment.apiBaseUrl}/fpl/manager/${managerId}/live`, { params })
      .pipe(catchError((err: HttpErrorResponse) => throwError(() => this.toApiError(err))));
  }

  getLeagues(managerId: number): Observable<ManagerLeagues> {
    return this.http
      .get<ManagerLeagues>(`${environment.apiBaseUrl}/fpl/manager/${managerId}/leagues`)
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
