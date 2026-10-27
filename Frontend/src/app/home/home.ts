import { HttpErrorResponse } from '@angular/common/http';
import { Component, inject, signal } from '@angular/core';
import { environment } from '../../environments/environment';
import { AuthService } from '../auth.service';
import { InterviewSessionsService } from '../interview-sessions.service';

@Component({
  selector: 'app-home',
  templateUrl: './home.html',
  styleUrl: './home.scss',
})
export class Home {
  protected readonly auth = inject(AuthService);
  private readonly sessions = inject(InterviewSessionsService);

  protected readonly createAction = `${environment.apiUrl}/api/interview-sessions`;
  protected readonly defaultLanguage = 'English';
  protected readonly defaultDifficulty = 'auto';
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
  protected readonly createdSessionId = signal<string | null>(null);

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
          this.createdSessionId.set(result.sessionId);
        },
        error: (err: unknown) => {
          this.submitting.set(false);
          this.error.set(this.describeError(err));
        },
      });
  }

  protected resetForm(): void {
    this.createdSessionId.set(null);
    this.error.set(null);
    this.submitting.set(false);
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
