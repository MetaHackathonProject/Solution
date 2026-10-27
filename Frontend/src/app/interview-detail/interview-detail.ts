import { DatePipe } from '@angular/common';
import { HttpErrorResponse } from '@angular/common/http';
import { Component, OnDestroy, OnInit, computed, inject, signal } from '@angular/core';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { Subscription, catchError, of, switchMap, timer } from 'rxjs';
import { AuthService } from '../auth.service';
import {
  PREPARING_STEPS,
  isPreparingStatus,
  isTerminalStatus,
  statusDetail,
  statusLabel,
  stepState,
} from '../interview-session-status';
import { InterviewSession, InterviewSessionsService } from '../interview-sessions.service';

@Component({
  selector: 'app-interview-detail',
  imports: [RouterLink, DatePipe],
  templateUrl: './interview-detail.html',
  styleUrl: './interview-detail.scss',
})
export class InterviewDetail implements OnInit, OnDestroy {
  protected readonly auth = inject(AuthService);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly sessionsApi = inject(InterviewSessionsService);

  protected readonly preparingSteps = PREPARING_STEPS;
  protected readonly loading = signal(true);
  protected readonly error = signal<string | null>(null);
  protected readonly session = signal<InterviewSession | null>(null);
  protected readonly lastPreparingStatus = signal('STARTED');
  protected readonly starting = signal(false);
  protected readonly deleting = signal(false);

  protected readonly status = computed(() => this.session()?.status ?? 'STARTED');
  protected readonly preparing = computed(() => isPreparingStatus(this.status()));
  protected readonly ready = computed(() => this.status() === 'SUCCESS');
  protected readonly failed = computed(() => this.status() === 'FAILED');
  protected readonly statusTitle = computed(() => statusLabel(this.status()));
  protected readonly statusCopy = computed(() => statusDetail(this.status()));
  protected readonly stepStatus = computed(() =>
    this.status() === 'FAILED' ? this.lastPreparingStatus() : this.status(),
  );

  private pollSub: Subscription | null = null;
  private sessionId = '';

  ngOnInit(): void {
    this.sessionId = this.route.snapshot.paramMap.get('sessionId') ?? '';
    if (!this.sessionId) {
      this.loading.set(false);
      this.error.set('Missing session id.');
      return;
    }

    if (!this.auth.isAuthenticated()) {
      this.loading.set(false);
      return;
    }

    this.startPolling(this.sessionId);
  }

  ngOnDestroy(): void {
    this.pollSub?.unsubscribe();
  }

  protected stepClass(stepId: (typeof PREPARING_STEPS)[number]['id']): string {
    if (this.status() === 'FAILED' && stepId === this.lastPreparingStatus()) {
      return 'prep-step prep-step--failed';
    }

    return `prep-step prep-step--${stepState(stepId, this.stepStatus())}`;
  }

  protected difficultyLabel(value: string): string {
    switch (value) {
      case 'auto':
        return 'Auto';
      case 'easy':
        return 'Easy';
      case 'medium':
        return 'Medium';
      case 'hard':
        return 'Hard';
      default:
        return value;
    }
  }

  protected createdLabel(unixSeconds: number): Date {
    return new Date(unixSeconds * 1000);
  }

  protected startInterview(): void {
    if (!this.ready()) {
      return;
    }

    this.starting.set(true);
    // Voice interview wiring comes next; keep the ready CTA in place.
    window.setTimeout(() => this.starting.set(false), 600);
  }

  protected deleteSession(): void {
    if (!this.sessionId || this.deleting()) {
      return;
    }

    const name = this.session()?.name || 'this interview';
    if (!window.confirm(`Delete ${name}? This removes the plan, questions, and progress.`)) {
      return;
    }

    this.deleting.set(true);
    this.pollSub?.unsubscribe();
    this.pollSub = null;

    this.sessionsApi.delete(this.sessionId).subscribe({
      next: () => {
        void this.router.navigate(['/interviews']);
      },
      error: (err: unknown) => {
        this.deleting.set(false);
        this.error.set(this.describeDeleteError(err));
        if (!isTerminalStatus(this.status()) && this.sessionId) {
          this.startPolling(this.sessionId);
        }
      },
    });
  }

  private describeDeleteError(err: unknown): string {
    if (err instanceof HttpErrorResponse) {
      if (err.status === 404) {
        return 'Interview session was already deleted.';
      }

      if (err.status === 401) {
        return 'Sign in expired. Sign in again and retry.';
      }

      if (err.status === 0) {
        return 'Could not reach the API. Check your connection and try again.';
      }

      return err.error?.title || err.error?.detail || err.message || 'Could not delete the session.';
    }

    return 'Could not delete the session.';
  }

  private startPolling(sessionId: string): void {
    this.pollSub?.unsubscribe();
    this.pollSub = timer(0, 2500)
      .pipe(
        switchMap(() =>
          this.sessionsApi.get(sessionId).pipe(
            catchError((err: unknown) => {
              this.error.set(this.describeError(err));
              this.loading.set(false);
              return of(null);
            }),
          ),
        ),
      )
      .subscribe((session) => {
        if (!session) {
          return;
        }

        if (isPreparingStatus(session.status)) {
          this.lastPreparingStatus.set(session.status);
        }

        this.session.set(session);
        this.loading.set(false);
        this.error.set(null);

        if (isTerminalStatus(session.status)) {
          this.pollSub?.unsubscribe();
          this.pollSub = null;
        }
      });
  }

  private describeError(err: unknown): string {
    if (err instanceof HttpErrorResponse) {
      if (err.status === 404) {
        return 'Interview session not found.';
      }

      if (err.status === 401) {
        return 'Sign in expired. Sign in again and retry.';
      }

      if (err.status === 0) {
        return 'Could not reach the API. Check your connection and try again.';
      }

      return err.error?.title || err.error?.detail || err.message || 'Could not load the session.';
    }

    return 'Could not load the session.';
  }
}
