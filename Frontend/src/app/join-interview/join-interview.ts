import { HttpErrorResponse } from '@angular/common/http';
import { Component, OnInit, inject, signal } from '@angular/core';
import { ActivatedRoute, Router } from '@angular/router';
import { Observable, switchMap } from 'rxjs';
import { AccountService } from '../account.service';
import { AuthService } from '../auth.service';
import {
  InterviewInvitePreview,
  InterviewTemplatesService,
  JoinInterviewTemplateResponse,
} from '../interview-templates.service';

@Component({
  selector: 'app-join-interview',
  templateUrl: './join-interview.html',
  styleUrl: './join-interview.scss',
})
export class JoinInterview implements OnInit {
  protected readonly auth = inject(AuthService);
  protected readonly account = inject(AccountService);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly templates = inject(InterviewTemplatesService);

  protected readonly inviteCode = signal('');
  protected readonly preview = signal<InterviewInvitePreview | null>(null);
  protected readonly loading = signal(true);
  protected readonly joining = signal(false);
  protected readonly error = signal<string | null>(null);

  ngOnInit(): void {
    const code = this.route.snapshot.paramMap.get('inviteCode') ?? '';
    this.inviteCode.set(code);
    if (!code) {
      this.loading.set(false);
      this.error.set('This invite link is missing a code.');
      return;
    }

    this.templates.previewInvite(code).subscribe({
      next: (preview) => {
        this.preview.set(preview);
        this.loading.set(false);
      },
      error: (err: unknown) => {
        this.loading.set(false);
        if (err instanceof HttpErrorResponse && (err.status === 0 || err.status === 401)) {
          return;
        }

        this.error.set(
          err instanceof HttpErrorResponse && err.status === 404
            ? 'This invite link is not valid.'
            : this.describeError(err, 'Could not open this invite.'),
        );
      },
    });
  }

  protected accept(): void {
    const code = this.inviteCode();
    if (!code || this.joining()) {
      return;
    }

    if (this.account.isEmployer()) {
      this.error.set('Employer accounts cannot take a candidate interview.');
      return;
    }

    this.joining.set(true);
    this.error.set(null);

    const join$: Observable<JoinInterviewTemplateResponse> = this.account.role()
      ? this.templates.join(code)
      : this.account.setRole('Candidate').pipe(switchMap(() => this.templates.join(code)));

    join$.subscribe({
      next: (joined) => {
        void this.router.navigate(['/interviews', joined.sessionId]);
      },
      error: (err: unknown) => {
        this.joining.set(false);
        this.error.set(this.describeError(err, 'Could not start this interview.'));
      },
    });
  }

  private describeError(err: unknown, fallback: string): string {
    if (err instanceof HttpErrorResponse) {
      return err.error?.detail || err.error?.title || err.message || fallback;
    }

    return fallback;
  }
}
