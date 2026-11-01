import { HttpClient } from '@angular/common/http';
import { Injectable, computed, inject, signal } from '@angular/core';
import { Observable, of, switchMap, tap } from 'rxjs';
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
  private readonly baseUrl = `${environment.apiUrl}/api/me`;

  readonly profile = signal<CurrentUser | null>(null);
  readonly loaded = signal(false);
  readonly role = computed(() => this.profile()?.role ?? null);
  readonly isEmployer = computed(() => this.role() === 'Employer');
  readonly isCandidate = computed(() => this.role() === 'Candidate');

  load(): Observable<CurrentUser> {
    return this.http.get<CurrentUser>(this.baseUrl).pipe(
      switchMap((profile) => {
        this.profile.set(profile);
        const email = this.auth.email().trim();
        if (profile.role && email && !profile.email?.trim()) {
          return this.setRole(profile.role);
        }

        this.loaded.set(true);
        return of(profile);
      }),
    );
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
    this.profile.set(null);
    this.loaded.set(false);
  }
}
