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
import { catchError, firstValueFrom, forkJoin, of } from 'rxjs';
import { AuthService } from '../auth.service';
import { continueInterviewFirstMessage } from '../continue-interview-first-message';
import { toElevenLabsLanguage } from '../eleven-labs-language';
import {
  InteractiveTaskResponse,
  InteractiveTaskService,
} from '../interactive-task.service';
import { interviewOpeningMessage } from '../interview-opening-message';
import {
  VoiceAgentContextResponse,
  VoiceAgentPurpose,
  VoiceAgentService,
} from '../voice-agent.service';
import { walkthroughFirstMessage } from '../walkthrough-first-message';
import { InteractivePanel } from './interactive-panel';
import { InteractiveSubmissionPayload } from './walkthrough-submission';

type VoicePhase = 'idle' | 'interview' | 'paused' | 'walkthrough';
type DisconnectIntent = 'none' | 'pause' | 'walkthrough_done' | 'user_end';

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
  protected readonly panelCollapsed = signal(false);
  protected readonly voicePhase = signal<VoicePhase>('idle');

  protected sessionId = '';
  private signedUrl: string | null = null;
  private conversationId: string | null = null;
  private isResume = false;
  private agentContext: VoiceAgentContextResponse | null = null;
  private conversation: VoiceConversation | TextConversation | null = null;
  private ending = false;
  private disconnectIntent: DisconnectIntent = 'none';
  private pendingPauseAfterSpeech = false;
  private sawSpeakingForPause = false;
  private pauseGraceTimer: ReturnType<typeof setTimeout> | null = null;
  private pauseSettleTimer: ReturnType<typeof setTimeout> | null = null;
  private pauseDebounceTimer: ReturnType<typeof setTimeout> | null = null;
  private activePurpose: VoiceAgentPurpose = 'interview';
  private walkthroughSubmission: InteractiveSubmissionPayload | null = null;

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
      signed: this.voiceAgent.getSignedUrl(this.sessionId, 'interview'),
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
        if (interactive && interactive.status !== 'Answered') {
          this.interactiveTask.set(interactive);
          this.voicePhase.set('paused');
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
    this.clearPauseTimers();
    if (this.voicePhase() === 'paused') {
      this.conversation = null;
      return;
    }

    void this.endConversationLocally();
  }

  protected async startConversation(): Promise<void> {
    if (this.voicePhase() === 'paused' || this.starting()) {
      return;
    }

    await this.connectVoice({
      purpose: 'interview',
      firstMessage: interviewOpeningMessage({
        isResume: this.isResume,
        language: toElevenLabsLanguage(this.agentContext?.language),
        jobTitle: this.agentContext?.jobTitle,
        company: this.agentContext?.company,
      }),
      phase: 'interview',
    });
  }

  protected async stopConversation(): Promise<void> {
    this.clearPauseTimers();
    this.pendingPauseAfterSpeech = false;
    this.disconnectIntent = 'user_end';
    if (!this.conversation) {
      void this.completeAndLeave();
      return;
    }

    await this.conversation.endSession();
  }

  protected clearInteractiveTask(): void {
    this.panelCollapsed.set(true);
  }

  protected showInteractiveTask(): void {
    this.panelCollapsed.set(false);
  }

  protected onInteractiveSubmitted(payload: InteractiveSubmissionPayload): void {
    this.panelCollapsed.set(false);
    this.walkthroughSubmission = payload;
    void this.startWalkthrough();
  }

  private async startWalkthrough(): Promise<void> {
    this.clearPauseTimers();
    this.pendingPauseAfterSpeech = false;
    this.error.set(null);

    try {
      const signed = await firstValueFrom(
        this.voiceAgent.getSignedUrl(this.sessionId, 'walkthrough'),
      );
      this.signedUrl = signed.signedUrl;
      this.conversationId = signed.conversationId;

      const submission = this.walkthroughSubmission;
      const extraDynamicVariables: Record<string, string> = {};
      if (submission) {
        extraDynamicVariables['expected_input'] = submission.expectedInput;
        extraDynamicVariables['task_module'] = submission.module;
        if (submission.taskPrompt) {
          extraDynamicVariables['task_prompt'] = submission.taskPrompt;
        }
        if (submission.submittedSolution) {
          extraDynamicVariables['submitted_solution'] = submission.submittedSolution;
        }
      }

      await this.connectVoice({
        purpose: 'walkthrough',
        firstMessage: walkthroughFirstMessage(
          toElevenLabsLanguage(this.agentContext?.language),
        ),
        phase: 'walkthrough',
        extraDynamicVariables,
      });
    } catch (err: unknown) {
      this.voicePhase.set('paused');
      this.error.set(this.describeError(err));
    }
  }

  private async continueInterviewAfterWalkthrough(): Promise<void> {
    this.error.set(null);
    this.interactiveTask.set(null);
    this.panelCollapsed.set(false);
    this.walkthroughSubmission = null;

    try {
      const signed = await firstValueFrom(
        this.voiceAgent.getSignedUrl(this.sessionId, 'interview'),
      );
      this.signedUrl = signed.signedUrl;
      this.conversationId = signed.conversationId;
      this.isResume = true;

      await this.connectVoice({
        purpose: 'interview',
        firstMessage: continueInterviewFirstMessage(
          toElevenLabsLanguage(this.agentContext?.language),
        ),
        phase: 'interview',
      });
    } catch (err: unknown) {
      this.voicePhase.set('idle');
      this.connectionStatus.set('idle');
      this.error.set(this.describeError(err));
    }
  }

  private async connectVoice(options: {
    purpose: VoiceAgentPurpose;
    firstMessage: string;
    phase: Exclude<VoicePhase, 'idle' | 'paused'>;
    extraDynamicVariables?: Record<string, string>;
  }): Promise<void> {
    if (!this.sessionId || !this.signedUrl || this.starting()) {
      return;
    }

    this.starting.set(true);
    this.error.set(null);
    this.activePurpose = options.purpose;
    this.disconnectIntent = 'none';

    try {
      await navigator.mediaDevices.getUserMedia({ audio: true });
    } catch {
      this.starting.set(false);
      this.error.set('Microphone access is required to start the interview.');
      return;
    }

    const language = toElevenLabsLanguage(this.agentContext?.language);
    const dynamicVariables: Record<string, string> = {
      sessionId: this.sessionId,
      is_resume: this.isResume ? 'true' : 'false',
      ...options.extraDynamicVariables,
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
            firstMessage: options.firstMessage,
          },
        },
        clientTools: {
          start_interactive_task: async (parameters: { taskId?: string }) => {
            const taskId = parameters?.taskId?.trim();
            if (!taskId) {
              return 'Missing taskId';
            }

            await this.openInteractiveTask(taskId);
            this.armPauseAfterSpeech();
            return (
              'Interactive task opened on screen. Now speak the interactive question aloud, ' +
              'briefly tell the candidate the full exercise is on screen, and that you will continue after they submit. ' +
              'The voice call will pause only after you finish that spoken intro.'
            );
          },
          finish_solution_walkthrough: async () => {
            this.disconnectIntent = 'walkthrough_done';
            if (this.conversation) {
              await this.conversation.endSession();
            }
            return 'Returning to the main interview';
          },
        },
        onStatusChange: ({ status }) => {
          this.connectionStatus.set(status);
        },
        onModeChange: ({ mode }) => {
          this.mode.set(mode);
          this.onVoiceModeChange(mode);
        },
        onError: () => {
          this.error.set('The voice interview connection failed. Try again.');
          this.starting.set(false);
        },
        onConnect: () => {
          this.starting.set(false);
          this.voicePhase.set(options.phase);
        },
        onDisconnect: () => {
          this.connectionStatus.set('disconnected');
          this.onVoiceDisconnected();
        },
      });
    } catch {
      this.starting.set(false);
      this.error.set('Could not start the voice interview. Try again.');
    }
  }

  private armPauseAfterSpeech(): void {
    this.clearPauseTimers();
    this.pendingPauseAfterSpeech = true;
    // Do not count speech that was already in progress during the tool call —
    // that caused an immediate pause on the next listening blip before the intro.
    this.sawSpeakingForPause = false;

    // If the agent keeps speaking through the tool into the intro, mark it after settle.
    this.pauseSettleTimer = setTimeout(() => {
      if (this.pendingPauseAfterSpeech && this.mode() === 'speaking') {
        this.sawSpeakingForPause = true;
        this.clearPauseGraceTimer();
      }
    }, 600);

    // Fallback if the agent never starts the spoken intro.
    this.pauseGraceTimer = setTimeout(() => {
      if (this.pendingPauseAfterSpeech && !this.sawSpeakingForPause) {
        void this.pauseInterviewLocally();
      }
    }, 20_000);
  }

  private onVoiceModeChange(mode: Mode): void {
    if (!this.pendingPauseAfterSpeech) {
      return;
    }

    if (mode === 'speaking') {
      this.sawSpeakingForPause = true;
      this.clearPauseGraceTimer();
      this.clearPauseDebounceTimer();
      return;
    }

    if (mode === 'listening' && this.sawSpeakingForPause) {
      // Debounce brief listening gaps mid-utterance.
      this.clearPauseDebounceTimer();
      this.pauseDebounceTimer = setTimeout(() => {
        if (this.pendingPauseAfterSpeech && this.mode() === 'listening') {
          void this.pauseInterviewLocally();
        }
      }, 700);
    }
  }

  private async pauseInterviewLocally(): Promise<void> {
    if (!this.pendingPauseAfterSpeech) {
      return;
    }

    this.pendingPauseAfterSpeech = false;
    this.clearPauseTimers();
    this.disconnectIntent = 'pause';
    this.voicePhase.set('paused');

    if (!this.conversation) {
      return;
    }

    const conversation = this.conversation;
    this.conversation = null;
    await conversation.endSession();
  }

  private onVoiceDisconnected(): void {
    const intent = this.disconnectIntent;
    this.disconnectIntent = 'none';
    this.conversation = null;
    this.starting.set(false);

    if (intent === 'pause') {
      this.voicePhase.set('paused');
      this.connectionStatus.set('idle');
      return;
    }

    if (intent === 'walkthrough_done') {
      void this.continueInterviewAfterWalkthrough();
      return;
    }

    if (intent === 'user_end') {
      void this.completeAndLeave();
      return;
    }

    // Agent ended the call (end_call) or unexpected drop.
    if (this.activePurpose === 'walkthrough') {
      void this.continueInterviewAfterWalkthrough();
      return;
    }

    void this.completeAndLeave();
  }

  private openInteractiveTask(taskId: string): Promise<void> {
    return new Promise((resolve, reject) => {
      this.interactiveTasks.getById(this.sessionId, taskId).subscribe({
        next: (task) => {
          this.interactiveTask.set(task);
          this.panelCollapsed.set(false);
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
    const conversationId = this.conversationId;
    this.conversation = null;
    this.voicePhase.set('idle');

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

  private clearPauseGraceTimer(): void {
    if (this.pauseGraceTimer) {
      clearTimeout(this.pauseGraceTimer);
      this.pauseGraceTimer = null;
    }
  }

  private clearPauseDebounceTimer(): void {
    if (this.pauseDebounceTimer) {
      clearTimeout(this.pauseDebounceTimer);
      this.pauseDebounceTimer = null;
    }
  }

  private clearPauseTimers(): void {
    this.clearPauseGraceTimer();
    this.clearPauseDebounceTimer();
    if (this.pauseSettleTimer) {
      clearTimeout(this.pauseSettleTimer);
      this.pauseSettleTimer = null;
    }
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
