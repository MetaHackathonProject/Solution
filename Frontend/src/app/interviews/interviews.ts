import { DatePipe } from '@angular/common';
import { HttpErrorResponse } from '@angular/common/http';
import { Component, OnDestroy, OnInit, computed, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { Subscription, catchError, of, switchMap, timer } from 'rxjs';
import { AuthService } from '../auth.service';
import {
  isPreparingStatus,
  statusLabel,
  statusTone,
} from '../interview-session-status';
import { InterviewSession, InterviewSessionsService } from '../interview-sessions.service';
import { SessionActionsMenu } from '../session-actions-menu/session-actions-menu';

@Component({
  selector: 'app-interviews',
  imports: [RouterLink, DatePipe, SessionActionsMenu],
  templateUrl: './interviews.html',
  styleUrl: './interviews.scss',
})
export class Interviews implements OnInit, OnDestroy {
  protected readonly auth = inject(AuthService);
  private readonly sessionsApi = inject(InterviewSessionsService);

  protected readonly loading = signal(true);
  protected readonly error = signal<string | null>(null);
  protected readonly sessions = signal<InterviewSession[]>([]);
  protected readonly deletingId = signal<string | null>(null);

  protected readonly preparing = computed(() =>
    this.sessions().filter((session) => isPreparingStatus(session.status)),
  );
  protected readonly ready = computed(() =>
    this.sessions().filter((session) => session.status === 'SUCCESS'),
  );
  protected readonly failed = computed(() =>
    this.sessions().filter((session) => session.status === 'FAILED'),
  );

  private pollSub: Subscription | null = null;

  ngOnInit(): void {
    if (!this.auth.isAuthenticated()) {
      this.loading.set(false);
      return;
    }

    this.startPolling();
  }

  ngOnDestroy(): void {
    this.pollSub?.unsubscribe();
  }

  protected statusLabel = statusLabel;
  protected statusTone = statusTone;

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

  protected deleteSession(event: Event, session: InterviewSession): void {
    event.preventDefault();
    event.stopPropagation();

    if (this.deletingId()) {
      return;
    }

    if (
      !window.confirm(
        `Delete ${session.name}? This removes the plan, questions, and progress.`,
      )
    ) {
      return;
    }

    this.deletingId.set(session.sessionId);
    this.sessionsApi.delete(session.sessionId).subscribe({
      next: () => {
        this.sessions.update((items) =>
          items.filter((item) => item.sessionId !== session.sessionId),
        );
        this.deletingId.set(null);
        this.error.set(null);

        if (
          !this.sessions().some((item) => isPreparingStatus(item.status)) &&
          this.pollSub
        ) {
          this.pollSub.unsubscribe();
          this.pollSub = null;
        }
      },
      error: (err: unknown) => {
        this.deletingId.set(null);
        this.error.set(this.describeDeleteError(err));
      },
    });
  }

  private startPolling(): void {
    this.pollSub?.unsubscribe();
    this.pollSub = timer(0, 4000)
      .pipe(
        switchMap(() =>
          this.sessionsApi.list().pipe(
            catchError((err: unknown) => {
              this.error.set(this.describeError(err));
              this.loading.set(false);
              return of(null);
            }),
          ),
        ),
      )
      .subscribe((sessions) => {
        if (!sessions) {
          return;
        }

        this.sessions.set(sessions);
        this.loading.set(false);
        this.error.set(null);

        if (!sessions.some((session) => isPreparingStatus(session.status))) {
          this.pollSub?.unsubscribe();
          this.pollSub = null;
        }
      });
  }

  private describeError(err: unknown): string {
    if (err instanceof HttpErrorResponse) {
      if (err.status === 401) {
        return 'Sign in expired. Sign in again and retry.';
      }

      if (err.status === 0) {
        return 'Could not reach the API. Check your connection and try again.';
      }

      return err.error?.title || err.error?.detail || err.message || 'Could not load interviews.';
    }

    return 'Could not load interviews.';
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
}
