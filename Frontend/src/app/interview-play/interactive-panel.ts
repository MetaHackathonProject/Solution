import { HttpErrorResponse } from '@angular/common/http';
import {
  Component,
  ViewChild,
  computed,
  inject,
  input,
  output,
  signal,
} from '@angular/core';
import { marked } from 'marked';
import {
  InteractiveTaskResponse,
  InteractiveTaskService,
  SubmitInteractiveAnswerResponse,
} from '../interactive-task.service';
import { CodeEditor } from './code-editor';
import { DrawioEditor, DrawioEditorState } from './drawio-editor';
import { detectCodeLanguage } from './detect-code-language';
import {
  InteractiveSubmissionPayload,
  WALKTHROUGH_SOLUTION_MAX_CHARS,
  WALKTHROUGH_TASK_PROMPT_MAX_CHARS,
  truncateForWalkthrough,
} from './walkthrough-submission';

marked.setOptions({ gfm: true, breaks: true });

@Component({
  selector: 'app-interactive-panel',
  imports: [CodeEditor, DrawioEditor],
  templateUrl: './interactive-panel.html',
  styleUrl: './interactive-panel.scss',
})
export class InteractivePanel {
  private readonly tasksApi = inject(InteractiveTaskService);

  readonly sessionId = input.required<string>();
  readonly task = input.required<InteractiveTaskResponse>();
  readonly closed = output<void>();
  readonly submitted = output<InteractiveSubmissionPayload>();

  @ViewChild(CodeEditor) private codeEditor?: CodeEditor;
  @ViewChild(DrawioEditor) private drawioEditor?: DrawioEditor;

  protected readonly answerText = signal('');
  protected readonly submitting = signal(false);
  protected readonly error = signal<string | null>(null);
  protected readonly submittedOk = signal(false);
  protected readonly diagramReady = signal(false);
  protected readonly diagramHasContent = signal(false);

  protected readonly isCodeTask = computed(() => this.task().expectedInput === 'code');
  protected readonly isTextLike = computed(() => {
    const expected = this.task().expectedInput;
    return expected === 'text' || expected === 'free_string';
  });
  protected readonly isDrawLike = computed(() => {
    const expected = this.task().expectedInput;
    return expected === 'diagram' || expected === 'schema';
  });
  protected readonly preferredLanguage = computed(() =>
    detectCodeLanguage(this.task().questionContentFormatted),
  );
  /** Angular sanitizes [innerHTML] bindings; no bypass. */
  protected readonly promptHtml = computed(() => {
    const source = this.task().questionContentFormatted?.trim() ?? '';
    if (!source) {
      return '';
    }

    return marked.parse(source, { async: false }) as string;
  });

  private pendingSolution: string | null = null;

  protected onTextInput(event: Event): void {
    const target = event.target as HTMLTextAreaElement;
    this.answerText.set(target.value);
  }

  protected onCodeChange(value: string): void {
    this.answerText.set(value);
  }

  protected onFileSelected(event: Event): void {
    const input = event.target as HTMLInputElement;
    const file = input.files?.[0];
    if (!file) {
      return;
    }

    this.pendingSolution = null;
    this.submitFile(file);
  }

  protected submitCode(): void {
    if (this.submitting() || !this.codeEditor?.hasContent()) {
      return;
    }

    const solutionText = this.codeEditor.getWalkthroughSource();
    void this.codeEditor.buildSubmissionZip().then((file) => {
      if (!file) {
        this.error.set('Add some code before submitting.');
        return;
      }

      this.pendingSolution = solutionText;
      this.submitFile(file);
    });
  }

  protected submitText(): void {
    const text = this.answerText().trim();
    if (!text || this.submitting()) {
      return;
    }

    this.pendingSolution = text;
    this.submitTextValue(text);
  }

  protected onDiagramState(state: DrawioEditorState): void {
    this.diagramReady.set(state.ready);
    this.diagramHasContent.set(state.hasContent);
  }

  protected canSubmitDiagram(): boolean {
    return this.diagramReady() && this.diagramHasContent();
  }

  protected submitDiagram(): void {
    if (this.submitting() || !this.canSubmitDiagram() || !this.drawioEditor) {
      return;
    }

    this.submitting.set(true);
    this.error.set(null);

    void this.drawioEditor.exportAnswer().then((file) => {
      if (!file) {
        this.submitting.set(false);
        this.error.set('Could not export the diagram. Try again.');
        return;
      }

      this.pendingSolution = null;
      this.submitFile(file);
    });
  }

  protected dismiss(): void {
    this.closed.emit();
  }

  private submitTextValue(text: string): void {
    this.submitting.set(true);
    this.error.set(null);
    this.tasksApi.submitText(this.sessionId(), this.task().taskId, text).subscribe({
      next: (response) => this.markSubmitted(response),
      error: (err: unknown) => {
        this.submitting.set(false);
        this.error.set(this.describeError(err));
      },
    });
  }

  private submitFile(file: File): void {
    this.submitting.set(true);
    this.error.set(null);
    this.tasksApi.submitFile(this.sessionId(), this.task().taskId, file).subscribe({
      next: (response) => this.markSubmitted(response),
      error: (err: unknown) => {
        this.submitting.set(false);
        this.error.set(this.describeError(err));
      },
    });
  }

  private markSubmitted(response: SubmitInteractiveAnswerResponse): void {
    this.submitting.set(false);
    this.submittedOk.set(true);

    const task = this.task();
    const taskPrompt = (
      task.questionContentFormatted?.trim() ||
      task.questionContent?.trim() ||
      ''
    );
    const solution = this.pendingSolution?.trim() || null;
    this.pendingSolution = null;

    const visionStatus = response.visionReviewStatus ?? 'Skipped';
    this.submitted.emit({
      expectedInput: task.expectedInput,
      module: task.module,
      taskId: task.taskId,
      taskPrompt: truncateForWalkthrough(taskPrompt, WALKTHROUGH_TASK_PROMPT_MAX_CHARS),
      submittedSolution: solution
        ? truncateForWalkthrough(solution, WALKTHROUGH_SOLUTION_MAX_CHARS)
        : null,
      awaitVisionReview: visionStatus === 'Pending' || visionStatus === 'Running',
    });
  }

  private describeError(err: unknown): string {
    if (err instanceof HttpErrorResponse) {
      return err.error?.detail || err.message || 'Could not submit the answer.';
    }

    return 'Could not submit the answer.';
  }
}
