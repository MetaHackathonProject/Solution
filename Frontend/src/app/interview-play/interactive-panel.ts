import { HttpErrorResponse } from '@angular/common/http';
import {
  AfterViewInit,
  Component,
  ElementRef,
  OnDestroy,
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
} from '../interactive-task.service';
import { CodeEditor } from './code-editor';
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
  imports: [CodeEditor],
  templateUrl: './interactive-panel.html',
  styleUrl: './interactive-panel.scss',
})
export class InteractivePanel implements AfterViewInit, OnDestroy {
  private readonly tasksApi = inject(InteractiveTaskService);

  readonly sessionId = input.required<string>();
  readonly task = input.required<InteractiveTaskResponse>();
  readonly closed = output<void>();
  readonly submitted = output<InteractiveSubmissionPayload>();

  @ViewChild('canvas') private canvasRef?: ElementRef<HTMLCanvasElement>;
  @ViewChild(CodeEditor) private codeEditor?: CodeEditor;

  protected readonly answerText = signal('');
  protected readonly submitting = signal(false);
  protected readonly error = signal<string | null>(null);
  protected readonly submittedOk = signal(false);

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

  private drawing = false;
  private lastX = 0;
  private lastY = 0;
  /** Text snapshot for walkthrough; null for diagram/schema until we support those. */
  private pendingSolution: string | null = null;

  ngAfterViewInit(): void {
    this.resetCanvas();
  }

  ngOnDestroy(): void {
    this.drawing = false;
  }

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

    // Binary/file answers are not passed to the walkthrough agent yet.
    this.pendingSolution = null;
    this.submitFile(file);
  }

  protected clearCanvas(): void {
    this.resetCanvas();
  }

  protected onPointerDown(event: PointerEvent): void {
    const canvas = this.canvasRef?.nativeElement;
    if (!canvas) {
      return;
    }

    this.drawing = true;
    const point = this.pointerPosition(event, canvas);
    this.lastX = point.x;
    this.lastY = point.y;
    canvas.setPointerCapture(event.pointerId);
  }

  protected onPointerMove(event: PointerEvent): void {
    if (!this.drawing) {
      return;
    }

    const canvas = this.canvasRef?.nativeElement;
    const ctx = canvas?.getContext('2d');
    if (!canvas || !ctx) {
      return;
    }

    const point = this.pointerPosition(event, canvas);
    ctx.beginPath();
    ctx.moveTo(this.lastX, this.lastY);
    ctx.lineTo(point.x, point.y);
    ctx.stroke();
    this.lastX = point.x;
    this.lastY = point.y;
  }

  protected onPointerUp(event: PointerEvent): void {
    this.drawing = false;
    this.canvasRef?.nativeElement.releasePointerCapture(event.pointerId);
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

  protected submitDrawing(): void {
    const canvas = this.canvasRef?.nativeElement;
    if (!canvas || this.submitting()) {
      return;
    }

    canvas.toBlob((blob) => {
      if (!blob) {
        this.error.set('Could not export the drawing.');
        return;
      }

      this.pendingSolution = null;
      const file = new File([blob], 'answer.png', { type: 'image/png' });
      this.submitFile(file);
    }, 'image/png');
  }

  protected dismiss(): void {
    this.closed.emit();
  }

  private submitTextValue(text: string): void {
    this.submitting.set(true);
    this.error.set(null);
    this.tasksApi.submitText(this.sessionId(), this.task().taskId, text).subscribe({
      next: () => this.markSubmitted(),
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
      next: () => this.markSubmitted(),
      error: (err: unknown) => {
        this.submitting.set(false);
        this.error.set(this.describeError(err));
      },
    });
  }

  private markSubmitted(): void {
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

    this.submitted.emit({
      expectedInput: task.expectedInput,
      module: task.module,
      taskPrompt: truncateForWalkthrough(taskPrompt, WALKTHROUGH_TASK_PROMPT_MAX_CHARS),
      submittedSolution: solution
        ? truncateForWalkthrough(solution, WALKTHROUGH_SOLUTION_MAX_CHARS)
        : null,
    });
  }

  private resetCanvas(): void {
    const canvas = this.canvasRef?.nativeElement;
    const ctx = canvas?.getContext('2d');
    if (!canvas || !ctx) {
      return;
    }

    ctx.fillStyle = '#ffffff';
    ctx.fillRect(0, 0, canvas.width, canvas.height);
    ctx.strokeStyle = '#111827';
    ctx.lineWidth = 2;
    ctx.lineCap = 'round';
    ctx.lineJoin = 'round';
  }

  private pointerPosition(
    event: PointerEvent,
    canvas: HTMLCanvasElement,
  ): { x: number; y: number } {
    const rect = canvas.getBoundingClientRect();
    const scaleX = canvas.width / rect.width;
    const scaleY = canvas.height / rect.height;
    return {
      x: (event.clientX - rect.left) * scaleX,
      y: (event.clientY - rect.top) * scaleY,
    };
  }

  private describeError(err: unknown): string {
    if (err instanceof HttpErrorResponse) {
      return err.error?.detail || err.message || 'Could not submit the answer.';
    }

    return 'Could not submit the answer.';
  }
}
