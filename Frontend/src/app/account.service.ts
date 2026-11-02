import { HttpClient } from '@angular/common/http';
import { Injectable, computed, inject, signal } from '@angular/core';
import { OidcSecurityService } from 'angular-auth-oidc-client';
import {
  Observable,
  catchError,
  filter,
  finalize,
  of,
  retry,
  shareReplay,
  switchMap,
  take,
  tap,
  throwError,
  timeout,
  timer,
} from 'rxjs';
import { environment } from '../environments/environment';
import { AuthService } from './auth.service';

export type UserRole = 'Candidate' | 'Employer';

export interface CurrentUser {
  userId: string;
  email: string;
  role: UserRole | null;
}

@Injectable({
  providedIn: 'root',
})
export class AccountService {
  private readonly http = inject(HttpClient);
  private readonly auth = inject(AuthService);
  private readonly oidc = inject(OidcSecurityService);
  private readonly baseUrl = `${environment.apiUrl}/api/me`;
  private inflight$: Observable<CurrentUser | null> | null = null;

  readonly profile = signal<CurrentUser | null>(null);
  readonly loaded = signal(false);
  readonly role = computed(() => this.profile()?.role ?? null);
  readonly isEmployer = computed(() => this.role() === 'Employer');
  readonly isCandidate = computed(() => this.role() === 'Candidate');

  load(): Observable<CurrentUser | null> {
    if (this.loaded()) {
      return of(this.profile());
    }

    if (!this.inflight$) {
      this.inflight$ = this.fetchProfile().pipe(
        finalize(() => {
          this.inflight$ = null;
        }),
        shareReplay({ bufferSize: 1, refCount: true }),
      );
    }

    return this.inflight$;
  }

  setRole(role: UserRole): Observable<CurrentUser> {
    return this.http.put<CurrentUser>(this.baseUrl, { role, email: this.auth.email().trim() }).pipe(
      tap((profile) => {
        this.profile.set(profile);
        this.loaded.set(true);
      }),
    );
  }

  clear(): void {
    this.inflight$ = null;
    this.profile.set(null);
    this.loaded.set(false);
  }

  private fetchProfile(): Observable<CurrentUser | null> {
    return this.waitForAccessToken().pipe(
      switchMap(() => this.http.get<CurrentUser>(this.baseUrl)),
      retry({ count: 3, delay: 400 }),
      switchMap((profile) => this.afterLoad(profile)),
      tap(() => this.loaded.set(true)),
      catchError(() => {
        this.loaded.set(true);
        return of(null);
      }),
    );
  }

  private afterLoad(profile: CurrentUser): Observable<CurrentUser> {
    this.profile.set(profile);
    const email = this.auth.email().trim();
    if (profile.role && email && !profile.email?.trim()) {
      return this.setRole(profile.role);
    }

    return of(profile);
  }

  private waitForAccessToken(): Observable<string> {
    return timer(0, 200).pipe(
      switchMap(() => this.oidc.getAccessToken('cognito')),
      filter((token): token is string => !!token),
      take(1),
      timeout({ first: 8_000 }),
      catchError(() => throwError(() => new Error('Timed out waiting for access token'))),
    );
  }
}
