import { HttpErrorResponse } from '@angular/common/http';
import { Component, OnDestroy, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { Subscription, catchError, of, switchMap, timer } from 'rxjs';
import { isPreparingStatus, statusDetail, statusLabel } from '../interview-session-status';
import {
  EXPECTED_INPUT_OPTIONS,
  expectedInputDescription,
  GeneratedTemplateFromOffer,
  JOB_CONTRACT_TYPES,
  JOB_WORK_TYPES,
  InterviewTemplate,
  InterviewTemplateJobFacts,
  InterviewTemplatesService,
  MODULE_OPTIONS,
  moduleDescription,
  TemplateInteractiveTask,
  TemplateSpokenQuestion,
} from '../interview-templates.service';

@Component({
  selector: 'app-employer-template-edit',
  imports: [FormsModule, RouterLink],
  templateUrl: './employer-template-edit.html',
  styleUrl: './employer-template-edit.scss',
})
export class EmployerTemplateEdit implements OnInit, OnDestroy {
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly templatesApi = inject(InterviewTemplatesService);

  protected readonly moduleOptions = MODULE_OPTIONS;
  protected readonly expectedInputOptions = EXPECTED_INPUT_OPTIONS;
  protected readonly workTypes = JOB_WORK_TYPES;
  protected readonly contractTypes = JOB_CONTRACT_TYPES;
  protected readonly moduleDescription = moduleDescription;
  protected readonly expectedInputDescription = expectedInputDescription;
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
  ];

  protected readonly loading = signal(true);
  protected readonly saving = signal(false);
  protected readonly importing = signal(false);
  protected readonly error = signal<string | null>(null);
  protected readonly copied = signal(false);
  protected readonly templateId = signal<string | null>(null);
  protected readonly inviteCode = signal('');
  protected readonly offerUrl = signal('');
  protected readonly generateStatus = signal<string | null>(null);

  protected name = '';
  protected jobTitle = '';
  protected company = '';
  protected language = 'English';
  protected autoGenerate = false;
  protected spoken: TemplateSpokenQuestion[] = [this.emptySpoken()];
  protected tasks: TemplateInteractiveTask[] = [];
  protected jobFacts = this.emptyJobFacts();
  protected benefitDraft = '';
  private generatePoll: Subscription | null = null;

  ngOnInit(): void {
    const id = this.route.snapshot.paramMap.get('templateId');
    if (!id || id === 'new') {
      this.loading.set(false);
      return;
    }

    this.templateId.set(id);
    this.templatesApi.get(id).subscribe({
      next: (template) => this.apply(template),
      error: (err: unknown) => {
        this.loading.set(false);
        this.error.set(this.describeError(err, 'Could not load this template.'));
      },
    });
  }

  ngOnDestroy(): void {
    this.generatePoll?.unsubscribe();
  }

  protected addSpoken(): void {
    this.spoken = [...this.spoken, this.emptySpoken()];
  }

  protected removeSpoken(index: number): void {
    this.spoken = this.spoken.filter((_, i) => i !== index);
    if (this.spoken.length === 0) {
      this.spoken = [this.emptySpoken()];
    }
  }

  protected addTask(): void {
    this.tasks = [...this.tasks, this.emptyTask()];
  }

  protected removeTask(index: number): void {
    this.tasks = this.tasks.filter((_, i) => i !== index);
  }

  protected addBenefit(): void {
    const value = this.benefitDraft.trim();
    if (!value || this.jobFacts.benefits.length >= 12) {
      return;
    }

    if (this.jobFacts.benefits.some((benefit) => benefit.toLowerCase() === value.toLowerCase())) {
      this.benefitDraft = '';
      return;
    }

    this.jobFacts.benefits = [...this.jobFacts.benefits, value];
    this.benefitDraft = '';
  }

  protected removeBenefit(index: number): void {
    this.jobFacts.benefits = this.jobFacts.benefits.filter((_, i) => i !== index);
  }

  protected importOffer(): void {
    const url = this.normalizeUrl(this.offerUrl().trim());
    if (!url || this.importing()) {
      return;
    }

    this.importing.set(true);
    this.error.set(null);
    this.offerUrl.set(url);
    this.applyImportedMeta(url, !this.autoGenerate);

    if (!this.autoGenerate) {
      return;
    }

    this.generateStatus.set('STARTED');
    this.templatesApi.startOfferGeneration(url, this.language).subscribe({
      next: (started) => this.pollGeneration(started.sessionId),
      error: (err: unknown) => {
        this.importing.set(false);
        this.generateStatus.set(null);
        this.error.set(this.describeError(err, 'Could not start question generation.'));
      },
    });
  }

  protected generating(): boolean {
    const status = this.generateStatus();
    return status !== null && isPreparingStatus(status);
  }

  protected generateLabel(): string {
    const status = this.generateStatus();
    return status ? statusDetail(status) : '';
  }

  protected generateTitle(): string {
    const status = this.generateStatus();
    return status ? statusLabel(status) : '';
  }

  private applyImportedMeta(url: string, finishWhenDone: boolean): void {
    this.templatesApi.importOffer(url).subscribe({
      next: (imported) => {
        this.offerUrl.set(imported.offerUrl);
        if (imported.title && !this.jobTitle.trim()) {
          this.jobTitle = imported.title;
        }
        if (imported.company && !this.company.trim()) {
          this.company = imported.company;
        }
        if (finishWhenDone) {
          this.importing.set(false);
        }
      },
      error: (err: unknown) => {
        if (finishWhenDone) {
          this.importing.set(false);
          this.error.set(this.describeError(err, 'Could not read that posting.'));
        }
      },
    });
  }

  private pollGeneration(sessionId: string): void {
    this.generatePoll?.unsubscribe();
    this.generatePoll = timer(0, 2500)
      .pipe(
        switchMap(() =>
          this.templatesApi.getGeneratedFromOffer(sessionId).pipe(
            catchError((err: unknown) => {
              this.error.set(this.describeError(err, 'Could not check generation status.'));
              return of(null);
            }),
          ),
        ),
      )
      .subscribe((generated) => {
        if (!generated) {
          return;
        }

        this.generateStatus.set(generated.status);
        if (isPreparingStatus(generated.status)) {
          return;
        }

        this.generatePoll?.unsubscribe();
        this.generatePoll = null;
        this.importing.set(false);

        if (generated.status === 'FAILED') {
          this.error.set('Question generation failed. You can write questions manually.');
          return;
        }

        if (generated.status !== 'SUCCESS') {
          return;
        }

        this.applyGenerated(generated);
      });
  }

  private applyGenerated(generated: GeneratedTemplateFromOffer): void {
    if (generated.offerUrl) {
      this.offerUrl.set(generated.offerUrl);
    }
    if (generated.title) {
      this.jobTitle = generated.title;
    }
    if (generated.company) {
      this.company = generated.company;
    }
    if (!this.name.trim() && generated.title) {
      this.name = generated.company
        ? `${generated.title} · ${generated.company}`
        : generated.title;
    }

    this.spoken = generated.spokenQuestions.length
      ? generated.spokenQuestions.map((question) => ({ ...question }))
      : [this.emptySpoken()];
    this.tasks = generated.interactiveTasks.map((task) => ({ ...task }));
  }

  protected save(): void {
    if (this.saving()) {
      return;
    }

    this.saving.set(true);
    this.error.set(null);
    const request = {
      name: this.name.trim(),
      jobTitle: this.jobTitle.trim(),
      company: this.company.trim(),
      language: this.language,
      offerUrl: this.normalizeUrl(this.offerUrl().trim()) || null,
      jobFacts: {
        workType: this.jobFacts.workType || null,
        contractType: this.jobFacts.contractType || null,
        location: this.jobFacts.location.trim() || null,
        salary: this.jobFacts.salary.trim() || null,
        benefits: this.jobFacts.benefits,
        notes: this.jobFacts.notes.trim() || null,
      },
      spokenQuestions: this.spoken.map((question) => ({
        ...question,
        question: question.question.trim(),
        assessmentGoal: question.assessmentGoal.trim(),
      })),
      interactiveTasks: this.tasks.map((task) => ({
        ...task,
        questionContent: task.questionContent.trim(),
        assessmentGoal: task.assessmentGoal.trim(),
      })),
    };

    const id = this.templateId();
    const save$ = id
      ? this.templatesApi.update(id, request)
      : this.templatesApi.create(request);

    save$.subscribe({
      next: (template) => {
        this.apply(template);
        this.saving.set(false);
        if (!id) {
          void this.router.navigate(['/templates', template.templateId], { replaceUrl: true });
        }
      },
      error: (err: unknown) => {
        this.saving.set(false);
        this.error.set(this.describeError(err, 'Could not save this template.'));
      },
    });
  }

  protected inviteUrl(): string {
    return this.inviteCode() ? `${window.location.origin}/join/${this.inviteCode()}` : '';
  }

  protected async copyLink(): Promise<void> {
    const url = this.inviteUrl();
    if (!url) {
      return;
    }

    try {
      await navigator.clipboard.writeText(url);
      this.copied.set(true);
    } catch {
      this.error.set('Could not copy the invite link.');
    }
  }

  private apply(template: InterviewTemplate): void {
    this.templateId.set(template.templateId);
    this.inviteCode.set(template.inviteCode);
    this.name = template.name;
    this.jobTitle = template.jobTitle;
    this.company = template.company;
    this.language = template.language;
    this.offerUrl.set(template.offerUrl ?? '');
    this.jobFacts = this.emptyJobFacts(template.jobFacts);
    this.benefitDraft = '';
    this.spoken = template.spokenQuestions.length
      ? template.spokenQuestions.map((question) => ({ ...question }))
      : [this.emptySpoken()];
    this.tasks = template.interactiveTasks.map((task) => ({ ...task }));
    this.copied.set(false);
    this.loading.set(false);
  }

  private emptyJobFacts(facts?: InterviewTemplateJobFacts | null): {
    workType: string;
    contractType: string;
    location: string;
    salary: string;
    benefits: string[];
    notes: string;
  } {
    return {
      workType: facts?.workType ?? '',
      contractType: facts?.contractType ?? '',
      location: facts?.location ?? '',
      salary: facts?.salary ?? '',
      benefits: [...(facts?.benefits ?? [])],
      notes: facts?.notes ?? '',
    };
  }

  private emptySpoken(): TemplateSpokenQuestion {
    return {
      module: 'Communication',
      question: '',
      assessmentGoal: '',
    };
  }

  private emptyTask(): TemplateInteractiveTask {
    return {
      module: 'PracticalSkills',
      questionContent: '',
      expectedInput: 'code',
      assessmentGoal: '',
    };
  }

  private normalizeUrl(value: string): string {
    if (!value || /^https?:\/\//i.test(value)) {
      return value;
    }

    return `https://${value}`;
  }

  private describeError(err: unknown, fallback: string): string {
    if (err instanceof HttpErrorResponse) {
      return err.error?.detail || err.message || fallback;
    }

    return fallback;
  }
}
