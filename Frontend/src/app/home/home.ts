import { HttpErrorResponse } from '@angular/common/http';
import { Component, OnDestroy, computed, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { Subscription, catchError, of, switchMap, timer } from 'rxjs';
import { environment } from '../../environments/environment';
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
  selector: 'app-home',
  imports: [RouterLink],
  templateUrl: './home.html',
  styleUrl: './home.scss',
})
export class Home implements OnDestroy {
  protected readonly auth = inject(AuthService);
  private readonly sessions = inject(InterviewSessionsService);

  protected readonly createAction = `${environment.apiUrl}/api/interview-sessions`;
  protected readonly defaultLanguage = 'English';
  protected readonly defaultDifficulty = 'auto';
  protected readonly preparingSteps = PREPARING_STEPS;
  protected readonly difficulties = [
    {
      id: 'auto',
      title: 'Auto',
      description: 'The AI adapts the difficulty to the job offer requirements and seniority.',
    },
    {
      id: 'easy',
      title: 'Easy',
      description: 'Warm-up questions, gentle follow-ups.',
    },
    {
      id: 'medium',
      title: 'Medium',
      description: 'Realistic interview pace and depth.',
    },
    {
      id: 'hard',
      title: 'Hard',
      description: 'Senior-level rigor, sharper follow-ups.',
    },
  ] as const;
  protected readonly languages = [
    'English',
    'Polish',
    'German',
    'French',
    'Spanish',
    'Italian',
    'Ukrainian',
    'Dutch',
    'Portuguese',
    'Czech',
    'Swedish',
    'Japanese',
  ] as const;

  protected readonly submitting = signal(false);
  protected readonly error = signal<string | null>(null);
  protected readonly session = signal<InterviewSession | null>(null);
  protected readonly lastPreparingStatus = signal('STARTED');

  protected readonly status = computed(() => this.session()?.status ?? 'STARTED');
  protected readonly preparing = computed(() => isPreparingStatus(this.status()));
  protected readonly terminal = computed(() => isTerminalStatus(this.status()));
  protected readonly statusTitle = computed(() => statusLabel(this.status()));
  protected readonly statusCopy = computed(() => statusDetail(this.status()));
  protected readonly stepStatus = computed(() =>
    this.status() === 'FAILED' ? this.lastPreparingStatus() : this.status(),
  );

  private pollSub: Subscription | null = null;

  ngOnDestroy(): void {
    this.pollSub?.unsubscribe();
  }

  protected onSubmit(event: Event): void {
    event.preventDefault();

    const form = event.target as HTMLFormElement;
    if (!form.checkValidity()) {
      form.reportValidity();
      return;
    }

    const data = new FormData(form);
    const name = String(data.get('name') ?? '').trim();
    const offerUrl = this.normalizeUrl(String(data.get('offerUrl') ?? '').trim());
    const recruiterLanguage = String(data.get('recruiterLanguage') ?? this.defaultLanguage);
    const difficulty = this.readDifficulty(String(data.get('difficulty') ?? this.defaultDifficulty));

    this.submitting.set(true);
    this.error.set(null);

    this.sessions
      .create({
        name,
        offerUrl,
        settings: { recruiterLanguage, difficulty },
      })
      .subscribe({
        next: (result) => {
          this.submitting.set(false);
          this.session.set({
            sessionId: result.sessionId,
            name,
            offerUrl,
            status: 'STARTED',
            createdAt: Math.floor(Date.now() / 1000),
            updatedAt: Math.floor(Date.now() / 1000),
            settings: { recruiterLanguage, difficulty },
          });
          this.startPolling(result.sessionId);
        },
        error: (err: unknown) => {
          this.submitting.set(false);
          this.error.set(this.describeError(err));
        },
      });
  }

  protected resetForm(): void {
    this.pollSub?.unsubscribe();
    this.pollSub = null;
    this.session.set(null);
    this.lastPreparingStatus.set('STARTED');
    this.error.set(null);
    this.submitting.set(false);
  }

  protected stepClass(stepId: (typeof PREPARING_STEPS)[number]['id']): string {
    const state = stepState(stepId, this.stepStatus());
    if (this.status() === 'FAILED' && stepId === this.lastPreparingStatus()) {
      return 'prep-step prep-step--failed';
    }

    return `prep-step prep-step--${state}`;
  }

  private startPolling(sessionId: string): void {
    this.pollSub?.unsubscribe();
    this.pollSub = timer(0, 2500)
      .pipe(
        switchMap(() =>
          this.sessions.get(sessionId).pipe(
            catchError((err: unknown) => {
              this.error.set(this.describeError(err));
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
        if (isTerminalStatus(session.status)) {
          this.pollSub?.unsubscribe();
          this.pollSub = null;
        }
      });
  }

  private readDifficulty(value: string): 'auto' | 'easy' | 'medium' | 'hard' {
    if (value === 'easy' || value === 'medium' || value === 'hard' || value === 'auto') {
      return value;
    }

    return this.defaultDifficulty;
  }

  private normalizeUrl(value: string): string {
    if (!value || /^https?:\/\//i.test(value)) {
      return value;
    }

    return `https://${value}`;
  }

  private describeError(err: unknown): string {
    if (err instanceof HttpErrorResponse) {
      if (err.status === 401) {
        return 'Sign in expired. Sign in again and retry.';
      }

      if (err.status === 0) {
        return 'Could not reach the API. Check your connection and try again.';
      }

      return err.error?.title || err.error?.detail || err.message || 'Could not create the session.';
    }

    return 'Could not create the session.';
  }
}
