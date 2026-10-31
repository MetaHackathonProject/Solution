import { DatePipe } from '@angular/common';
import { HttpErrorResponse } from '@angular/common/http';
import { Component, OnInit, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { AuthService } from '../auth.service';
import { statusLabel } from '../interview-session-status';
import { EmployerAssignment, InterviewTemplatesService } from '../interview-templates.service';

@Component({
  selector: 'app-employer-assignments',
  imports: [RouterLink, DatePipe],
  templateUrl: './employer-assignments.html',
  styleUrl: './employer-assignments.scss',
})
export class EmployerAssignments implements OnInit {
  protected readonly auth = inject(AuthService);
  private readonly templatesApi = inject(InterviewTemplatesService);

  protected readonly loading = signal(true);
  protected readonly error = signal<string | null>(null);
  protected readonly assignments = signal<EmployerAssignment[]>([]);
  protected readonly statusLabel = statusLabel;

  ngOnInit(): void {
    if (!this.auth.isAuthenticated()) {
      this.loading.set(false);
      return;
    }

    this.templatesApi.listAssignments().subscribe({
      next: (assignments) => {
        this.assignments.set(assignments);
        this.loading.set(false);
      },
      error: (err: unknown) => {
        this.loading.set(false);
        this.error.set(
          err instanceof HttpErrorResponse
            ? err.error?.detail || err.message || 'Could not load assignments.'
            : 'Could not load assignments.',
        );
      },
    });
  }

  protected createdLabel(unixSeconds: number): Date {
    return new Date(unixSeconds * 1000);
  }
}
