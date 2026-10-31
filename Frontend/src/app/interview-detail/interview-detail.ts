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
import {
  InterviewOverview,
  InterviewSession,
  InterviewSessionsService,
  QuestionHelper,
} from '../interview-sessions.service';
import { QuestionHelperPanel } from '../question-helper-panel/question-helper-panel';
import { SessionActionsMenu } from '../session-actions-menu/session-actions-menu';

@Component({
  selector: 'app-interview-detail',
  imports: [RouterLink, DatePipe, SessionActionsMenu, QuestionHelperPanel],
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
  protected readonly overview = signal<InterviewOverview | null>(null);
  protected readonly overviewError = signal<string | null>(null);
  protected readonly questionHelpers = signal<QuestionHelper[]>([]);
  protected readonly lastPreparingStatus = signal('STARTED');
  protected readonly starting = signal(false);
  protected readonly retaking = signal(false);
  protected readonly deleting = signal(false);

  protected readonly status = computed(() => this.session()?.status ?? 'STARTED');
  protected readonly overviewStatus = computed(
    () => this.session()?.overviewStatus ?? 'NOT_AVAILABLE',
  );
  protected readonly preparing = computed(() => isPreparingStatus(this.status()));
  protected readonly ready = computed(
    () => this.status() === 'SUCCESS' || this.status() === 'IN_PROGRESS',
  );
  protected readonly completed = computed(() => this.status() === 'COMPLETED');
  protected readonly overviewGenerating = computed(
    () =>
      this.completed() &&
      (this.overviewStatus() === 'GENERATING' || this.overviewStatus() === 'NOT_AVAILABLE'),
  );
  protected readonly overviewReady = computed(
    () => this.completed() && this.overviewStatus() === 'READY',
  );
  protected readonly overviewFailed = computed(
    () => this.completed() && this.overviewStatus() === 'ERROR',
  );
  protected readonly failed = computed(() => this.status() === 'FAILED');
  protected readonly statusTitle = computed(() => statusLabel(this.status()));
  protected readonly statusCopy = computed(() => statusDetail(this.status()));
  protected readonly stepStatus = computed(() =>
    this.status() === 'FAILED' ? this.lastPreparingStatus() : this.status(),
  );

  private pollSub: Subscription | null = null;
  private overviewFetchInFlight = false;
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
    if (!this.ready() && this.status() !== 'IN_PROGRESS') {
      return;
    }

    this.starting.set(true);
    void this.router.navigate(['/interviews', this.sessionId, 'play']);
  }

  protected retakeInterview(): void {
    if (!this.sessionId || this.retaking() || this.deleting()) {
      return;
    }

    this.retaking.set(true);
    this.error.set(null);
    this.sessionsApi.retake(this.sessionId).subscribe({
      next: () => {
        void this.router.navigate(['/interviews', this.sessionId, 'play']);
      },
      error: (err: unknown) => {
        this.retaking.set(false);
        this.error.set(this.describeError(err));
      },
    });
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
        if (this.shouldKeepPolling() && this.sessionId) {
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

        if (session.status === 'COMPLETED' && session.overviewStatus === 'READY') {
          this.loadOverview(sessionId);
        }

        if (!this.shouldKeepPolling(session)) {
          this.pollSub?.unsubscribe();
          this.pollSub = null;
        }
      });
  }

  private shouldKeepPolling(session?: InterviewSession | null): boolean {
    const current = session ?? this.session();
    if (!current) {
      return true;
    }

    if (!isTerminalStatus(current.status)) {
      return true;
    }

    if (current.status !== 'COMPLETED') {
      return false;
    }

    const overviewStatus = current.overviewStatus ?? 'NOT_AVAILABLE';
    return overviewStatus === 'GENERATING' || overviewStatus === 'NOT_AVAILABLE';
  }

  private loadOverview(sessionId: string): void {
    if (this.overview() || this.overviewFetchInFlight) {
      return;
    }

    this.overviewFetchInFlight = true;
    this.sessionsApi.getOverview(sessionId).subscribe({
      next: (overview) => {
        this.overviewFetchInFlight = false;
        this.overview.set(overview);
        this.overviewError.set(null);
        this.loadQuestionHelpers(sessionId);
      },
      error: (err: unknown) => {
        this.overviewFetchInFlight = false;
        this.overviewError.set(this.describeOverviewError(err));
      },
    });
  }

  protected cameraVerdict(verdict: string): string {
    switch (verdict) {
      case 'no_clear_risk':
        return 'No clear integrity risk';
      case 'some_concerns':
        return 'Some concerns';
      case 'high_concern':
        return 'High concern';
      default:
        return 'Inconclusive';
    }
  }

  private loadQuestionHelpers(sessionId: string): void {
    this.sessionsApi.getSessionQuestionHelpers(sessionId).subscribe({
      next: (helpers) => this.questionHelpers.set(helpers),
      error: () => this.questionHelpers.set([]),
    });
  }

  private describeOverviewError(err: unknown): string {
    if (err instanceof HttpErrorResponse) {
      if (err.status === 404) {
        return 'Overview is not ready yet.';
      }

      return err.error?.title || err.error?.detail || err.message || 'Could not load the overview.';
    }

    return 'Could not load the overview.';
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
