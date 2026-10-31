import { HttpClient } from '@angular/common/http';
import { Injectable, computed, inject, signal } from '@angular/core';
import { Observable, tap } from 'rxjs';
import { environment } from '../environments/environment';

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
  private readonly baseUrl = `${environment.apiUrl}/api/me`;

  readonly profile = signal<CurrentUser | null>(null);
  readonly loaded = signal(false);
  readonly role = computed(() => this.profile()?.role ?? null);
  readonly isEmployer = computed(() => this.role() === 'Employer');
  readonly isCandidate = computed(() => this.role() === 'Candidate');

  load(): Observable<CurrentUser> {
    return this.http.get<CurrentUser>(this.baseUrl).pipe(
      tap((profile) => {
        this.profile.set(profile);
        this.loaded.set(true);
      }),
    );
  }

  setRole(role: UserRole): Observable<CurrentUser> {
    return this.http.put<CurrentUser>(this.baseUrl, { role }).pipe(
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
