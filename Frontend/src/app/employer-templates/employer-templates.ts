import { DatePipe } from '@angular/common';
import { HttpErrorResponse } from '@angular/common/http';
import { Component, OnInit, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { AuthService } from '../auth.service';
import { InterviewTemplate, InterviewTemplatesService } from '../interview-templates.service';

@Component({
  selector: 'app-employer-templates',
  imports: [RouterLink, DatePipe],
  templateUrl: './employer-templates.html',
  styleUrl: './employer-templates.scss',
})
export class EmployerTemplates implements OnInit {
  protected readonly auth = inject(AuthService);
  private readonly templatesApi = inject(InterviewTemplatesService);

  protected readonly loading = signal(true);
  protected readonly error = signal<string | null>(null);
  protected readonly templates = signal<InterviewTemplate[]>([]);
  protected readonly copiedId = signal<string | null>(null);

  ngOnInit(): void {
    if (!this.auth.isAuthenticated()) {
      this.loading.set(false);
      return;
    }

    this.templatesApi.list().subscribe({
      next: (templates) => {
        this.templates.set(templates);
        this.loading.set(false);
      },
      error: (err: unknown) => {
        this.loading.set(false);
        this.error.set(this.describeError(err, 'Could not load templates.'));
      },
    });
  }

  protected inviteUrl(template: InterviewTemplate): string {
    return `${window.location.origin}/join/${template.inviteCode}`;
  }

  protected async copyLink(event: Event, template: InterviewTemplate): Promise<void> {
    event.preventDefault();
    event.stopPropagation();
    try {
      await navigator.clipboard.writeText(this.inviteUrl(template));
      this.copiedId.set(template.templateId);
    } catch {
      this.error.set('Could not copy the invite link.');
    }
  }

  protected createdLabel(unixSeconds: number): Date {
    return new Date(unixSeconds * 1000);
  }

  private describeError(err: unknown, fallback: string): string {
    if (err instanceof HttpErrorResponse) {
      return err.error?.detail || err.message || fallback;
    }

    return fallback;
  }
}
