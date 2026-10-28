import { HttpErrorResponse } from '@angular/common/http';
import { Component, OnDestroy, OnInit, inject, signal } from '@angular/core';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import {
  Conversation,
  Mode,
  Status,
  TextConversation,
  VoiceConversation,
} from '@elevenlabs/client';
import { catchError, forkJoin, of } from 'rxjs';
import { AuthService } from '../auth.service';
import { toElevenLabsLanguage } from '../eleven-labs-language';
import {
  InteractiveTaskResponse,
  InteractiveTaskService,
} from '../interactive-task.service';
import { interviewOpeningMessage } from '../interview-opening-message';
import {
  VoiceAgentContextResponse,
  VoiceAgentService,
} from '../voice-agent.service';
import { InteractivePanel } from './interactive-panel';

@Component({
  selector: 'app-interview-play',
  imports: [RouterLink, InteractivePanel],
  templateUrl: './interview-play.html',
  styleUrl: './interview-play.scss',
})
export class InterviewPlay implements OnInit, OnDestroy {
  protected readonly auth = inject(AuthService);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly voiceAgent = inject(VoiceAgentService);
  private readonly interactiveTasks = inject(InteractiveTaskService);

  protected readonly loading = signal(true);
  protected readonly starting = signal(false);
  protected readonly error = signal<string | null>(null);
  protected readonly connectionStatus = signal<Status | 'idle'>('idle');
  protected readonly mode = signal<Mode>('listening');
  protected readonly interactiveTask = signal<InteractiveTaskResponse | null>(null);

  protected sessionId = '';
  private signedUrl: string | null = null;
  private conversationId: string | null = null;
  private isResume = false;
  private agentContext: VoiceAgentContextResponse | null = null;
  private conversation: VoiceConversation | TextConversation | null = null;
  private ending = false;

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

    forkJoin({
      signed: this.voiceAgent.getSignedUrl(this.sessionId),
      context: this.voiceAgent.getVoiceContext(this.sessionId),
      interactive: this.interactiveTasks.getActive(this.sessionId).pipe(
        catchError(() => of(null)),
      ),
    }).subscribe({
      next: ({ signed, context, interactive }) => {
        this.signedUrl = signed.signedUrl;
        this.conversationId = signed.conversationId;
        this.isResume = signed.isResume;
        this.agentContext = context;
        if (interactive) {
          this.interactiveTask.set(interactive);
        }
        this.loading.set(false);
      },
      error: (err: unknown) => {
        this.loading.set(false);
        this.error.set(this.describeError(err));
      },
    });
  }

  ngOnDestroy(): void {
    void this.endConversationLocally();
  }

  protected async startConversation(): Promise<void> {
    if (!this.sessionId || !this.signedUrl || this.starting()) {
      return;
    }

    this.starting.set(true);
    this.error.set(null);

    try {
      await navigator.mediaDevices.getUserMedia({ audio: true });
    } catch {
      this.starting.set(false);
      this.error.set('Microphone access is required to start the interview.');
      return;
    }

    const language = toElevenLabsLanguage(this.agentContext?.language);
    const openingMessage = interviewOpeningMessage({
      isResume: this.isResume,
      language,
      jobTitle: this.agentContext?.jobTitle,
      company: this.agentContext?.company,
    });

    const dynamicVariables: Record<string, string> = {
      sessionId: this.sessionId,
      is_resume: this.isResume ? 'true' : 'false',
    };

    if (this.agentContext?.jobTitle) {
      dynamicVariables['job_title'] = this.agentContext.jobTitle;
    }
    if (this.agentContext?.company) {
      dynamicVariables['company'] = this.agentContext.company;
    }
    if (this.agentContext?.jobOverview) {
      dynamicVariables['job_overview'] = this.agentContext.jobOverview;
    }

    try {
      this.conversation = await Conversation.startSession({
        signedUrl: this.signedUrl,
        dynamicVariables,
        overrides: {
          agent: {
            ...(language ? { language } : {}),
            // Always override: language presets would otherwise force the stock intro.
            firstMessage: openingMessage,
          },
        },
        clientTools: {
          start_interactive_task: async (parameters: { taskId?: string }) => {
            const taskId = parameters?.taskId?.trim();
            if (!taskId) {
              return 'Missing taskId';
            }

            await this.openInteractiveTask(taskId);
            return 'Interactive task opened on screen';
          },
        },
        onStatusChange: ({ status }) => {
          this.connectionStatus.set(status);
        },
        onModeChange: ({ mode }) => {
          this.mode.set(mode);
        },
        onError: () => {
          this.error.set('The voice interview connection failed. Try again.');
          this.starting.set(false);
        },
        onConnect: () => {
          this.starting.set(false);
        },
        onDisconnect: () => {
          this.connectionStatus.set('disconnected');
          this.completeAndLeave();
        },
      });
    } catch {
      this.starting.set(false);
      this.error.set('Could not start the voice interview. Try again.');
    }
  }

  protected async stopConversation(): Promise<void> {
    if (!this.conversation) {
      return;
    }

    await this.conversation.endSession();
  }

  protected clearInteractiveTask(): void {
    this.interactiveTask.set(null);
  }

  private openInteractiveTask(taskId: string): Promise<void> {
    return new Promise((resolve, reject) => {
      this.interactiveTasks.getById(this.sessionId, taskId).subscribe({
        next: (task) => {
          this.interactiveTask.set(task);
          resolve();
        },
        error: (err: unknown) => {
          this.error.set(this.describeError(err));
          reject(err instanceof Error ? err : new Error('Could not load interactive task.'));
        },
      });
    });
  }

  private async endConversationLocally(): Promise<void> {
    if (!this.conversation) {
      return;
    }

    const conversation = this.conversation;
    this.conversation = null;
    await conversation.endSession();
  }

  private completeAndLeave(): void {
    if (this.ending) {
      return;
    }

    this.ending = true;
    const conversationId = this.conversation?.getId() ?? this.conversationId;
    this.conversation = null;

    if (!conversationId) {
      void this.router.navigate(['/interviews', this.sessionId]);
      return;
    }

    this.voiceAgent.endConversation(this.sessionId, conversationId).subscribe({
      next: () => {
        void this.router.navigate(['/interviews', this.sessionId]);
      },
      error: () => {
        void this.router.navigate(['/interviews', this.sessionId]);
      },
    });
  }

  private describeError(err: unknown): string {
    if (err instanceof HttpErrorResponse) {
      if (err.status === 400) {
        return err.error?.detail || 'This session is not ready for a live interview.';
      }

      if (err.status === 404) {
        return 'Interview session not found.';
      }

      if (err.status === 401) {
        return 'Sign in expired. Sign in again and retry.';
      }

      if (err.status === 0) {
        return 'Could not reach the API. Check your connection and try again.';
      }

      return err.error?.title || err.error?.detail || err.message || 'Could not prepare the interview.';
    }

    return 'Could not prepare the interview.';
  }
}
