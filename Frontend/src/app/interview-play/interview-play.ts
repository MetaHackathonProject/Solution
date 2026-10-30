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
import {
  Subscription,
  catchError,
  firstValueFrom,
  forkJoin,
  of,
  switchMap,
  takeWhile,
  timer,
} from 'rxjs';
import { AuthService } from '../auth.service';
import { continueInterviewFirstMessage } from '../continue-interview-first-message';
import { toElevenLabsLanguage } from '../eleven-labs-language';
import {
  InteractiveTaskResponse,
  InteractiveTaskService,
} from '../interactive-task.service';
import { interviewOpeningMessage } from '../interview-opening-message';
import {
  InterviewSessionsService,
  QuestionHelper,
} from '../interview-sessions.service';
import { QuestionHelperPanel } from '../question-helper-panel/question-helper-panel';
import {
  VoiceAgentContextResponse,
  VoiceAgentPurpose,
  VoiceAgentService,
} from '../voice-agent.service';
import { walkthroughFirstMessage } from '../walkthrough-first-message';
import { InteractivePanel } from './interactive-panel';
import {
  InteractiveSubmissionPayload,
  VISION_REVIEW_MAX_WAIT_MS,
  VISION_REVIEW_POLL_MS,
  WALKTHROUGH_SOLUTION_MAX_CHARS,
  truncateForWalkthrough,
} from './walkthrough-submission';

type VoicePhase = 'idle' | 'interview' | 'paused' | 'reviewing' | 'walkthrough';
type DisconnectIntent = 'none' | 'pause' | 'walkthrough_done' | 'user_end';
/** After current TTS finishes, disconnect for pause or full interview end. */
type AfterSpeechAction = 'none' | 'pause' | 'complete';

const AGENT_GOODBYE_RE =
  /\b(goodbye|good\s*bye|bye[\s-]*bye|farewell|take care|best of luck|wish you (the )?best|do widzenia|żegnaj|do zobaczenia|auf wiedersehen|au revoir|adios|adiós|arrivederci|tchau|do widzenia)\b/i;

@Component({
  selector: 'app-interview-play',
  imports: [RouterLink, InteractivePanel, QuestionHelperPanel],
  templateUrl: './interview-play.html',
  styleUrl: './interview-play.scss',
})
export class InterviewPlay implements OnInit, OnDestroy {
  protected readonly auth = inject(AuthService);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly voiceAgent = inject(VoiceAgentService);
  private readonly interactiveTasks = inject(InteractiveTaskService);
  private readonly sessionsApi = inject(InterviewSessionsService);

  protected readonly loading = signal(true);
  protected readonly starting = signal(false);
  protected readonly error = signal<string | null>(null);
  protected readonly connectionStatus = signal<Status | 'idle'>('idle');
  protected readonly mode = signal<Mode>('listening');
  protected readonly interactiveTask = signal<InteractiveTaskResponse | null>(null);
  protected readonly panelCollapsed = signal(false);
  protected readonly voicePhase = signal<VoicePhase>('idle');
  protected readonly activeHelpers = signal<QuestionHelper[]>([]);

  protected sessionId = '';
  private signedUrl: string | null = null;
  private conversationId: string | null = null;
  private isResume = false;
  private agentContext: VoiceAgentContextResponse | null = null;
  private conversation: VoiceConversation | TextConversation | null = null;
  private ending = false;
  private disconnectIntent: DisconnectIntent = 'none';
  /** Bumped to invalidate callbacks from a session that was forcibly torn down. */
  private connectionGen = 0;
  /** True after interactive open; pause when agent calls pause_for_interactive_task (or fallback). */
  private interactivePauseArmed = false;
  private afterSpeechAction: AfterSpeechAction = 'none';
  private pauseFallbackTimer: ReturnType<typeof setTimeout> | null = null;
  private pauseDebounceTimer: ReturnType<typeof setTimeout> | null = null;
  private activePurpose: VoiceAgentPurpose = 'interview';
  private walkthroughSubmission: InteractiveSubmissionPayload | null = null;
  private visionReviewSub: Subscription | null = null;
  private helperPollSub: Subscription | null = null;
  private transitionInFlight: Promise<void> | null = null;

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
    this.stopVisionReviewPoll();
    this.stopHelperPolling();
    if (this.voicePhase() === 'paused' || this.voicePhase() === 'reviewing') {
      this.conversation = null;
      return;
    }

    void this.endConversationLocally();
  }

  protected async startConversation(): Promise<void> {
    if (this.voicePhase() === 'paused' || this.starting() || this.transitionInFlight) {
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
    this.stopVisionReviewPoll();
    this.stopHelperPolling();
    this.interactivePauseArmed = false;
    this.afterSpeechAction = 'none';
    // Invalidate onDisconnect so a forced teardown does not also navigate/reconnect.
    this.disconnectIntent = 'none';
    await this.ensureDisconnected();
    void this.completeAndLeave();
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
    if (payload.awaitVisionReview) {
      this.beginVisionReview(payload);
      return;
    }

    void this.startWalkthrough();
  }

  private beginVisionReview(payload: InteractiveSubmissionPayload): void {
    this.stopVisionReviewPoll();
    this.error.set(null);
    this.voicePhase.set('reviewing');
    // Keep the submitted solution visible during review + walkthrough.
    this.panelCollapsed.set(false);

    const startedAt = Date.now();
    this.visionReviewSub = timer(0, VISION_REVIEW_POLL_MS)
      .pipe(
        switchMap(() =>
          this.interactiveTasks.getVisionReview(this.sessionId, payload.taskId).pipe(
            catchError(() => of(null)),
          ),
        ),
        takeWhile((review) => {
          if (Date.now() - startedAt >= VISION_REVIEW_MAX_WAIT_MS) {
            return false;
          }

          if (!review) {
            return true;
          }

          return review.status === 'Pending' || review.status === 'Running';
        }, true),
      )
      .subscribe({
        next: (review) => {
          if (!review) {
            return;
          }

          if (review.status === 'Pending' || review.status === 'Running') {
            return;
          }

          if (review.status === 'Ready' && review.description?.trim()) {
            this.walkthroughSubmission = {
              ...payload,
              submittedSolution: truncateForWalkthrough(
                review.description.trim(),
                WALKTHROUGH_SOLUTION_MAX_CHARS,
              ),
              awaitVisionReview: false,
            };
          } else {
            this.walkthroughSubmission = {
              ...payload,
              awaitVisionReview: false,
            };
          }

          // Leave reviewing before unsubscribe so complete does not double-start.
          this.voicePhase.set('paused');
          this.stopVisionReviewPoll();
          void this.startWalkthrough();
        },
        complete: () => {
          // Timed out still in-flight — continue without description.
          if (this.voicePhase() !== 'reviewing') {
            return;
          }

          this.walkthroughSubmission = {
            ...payload,
            awaitVisionReview: false,
          };
          this.voicePhase.set('paused');
          void this.startWalkthrough();
        },
      });
  }

  private stopVisionReviewPoll(): void {
    this.visionReviewSub?.unsubscribe();
    this.visionReviewSub = null;
  }

  private startHelperPolling(): void {
    this.stopHelperPolling();
    if (!this.sessionId) {
      return;
    }

    this.helperPollSub = timer(0, 2500)
      .pipe(
        switchMap(() =>
          this.sessionsApi.getActiveQuestionHelpers(this.sessionId).pipe(
            catchError(() => of([] as QuestionHelper[])),
          ),
        ),
      )
      .subscribe((helpers) => {
        // Backend should return only the current question; keep a single card if not.
        this.activeHelpers.set(helpers.slice(0, 1));
      });
  }

  private stopHelperPolling(): void {
    this.helperPollSub?.unsubscribe();
    this.helperPollSub = null;
  }

  private async startWalkthrough(): Promise<void> {
    if (this.transitionInFlight) {
      await this.transitionInFlight;
      return;
    }

    const run = this.runStartWalkthrough();
    this.transitionInFlight = run.finally(() => {
      if (this.transitionInFlight === run) {
        this.transitionInFlight = null;
      }
    });
    await this.transitionInFlight;
  }

  private async runStartWalkthrough(): Promise<void> {
    this.clearPauseTimers();
    this.stopVisionReviewPoll();
    this.interactivePauseArmed = false;
    this.afterSpeechAction = 'none';
    this.error.set(null);

    // Kill a lingering interview agent before the walkthrough connects.
    this.disconnectIntent = 'none';
    await this.ensureDisconnected();
    this.voicePhase.set('paused');
    this.connectionStatus.set('idle');

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
    if (this.transitionInFlight) {
      await this.transitionInFlight;
      if (this.voicePhase() === 'interview' && this.conversation) {
        return;
      }
    }

    const run = this.runContinueInterviewAfterWalkthrough();
    this.transitionInFlight = run.finally(() => {
      if (this.transitionInFlight === run) {
        this.transitionInFlight = null;
      }
    });
    await this.transitionInFlight;
  }

  private async runContinueInterviewAfterWalkthrough(): Promise<void> {
    this.error.set(null);
    this.interactiveTask.set(null);
    this.panelCollapsed.set(false);
    this.walkthroughSubmission = null;
    this.clearPauseTimers();
    this.interactivePauseArmed = false;
    this.afterSpeechAction = 'none';
    this.starting.set(true);
    this.connectionStatus.set('connecting');

    // Always drop the walkthrough agent before reconnecting the interviewer.
    this.disconnectIntent = 'none';
    await this.ensureDisconnected();

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

      if (!this.conversation) {
        this.starting.set(false);
        this.voicePhase.set('idle');
        this.connectionStatus.set('idle');
        this.error.set('Could not resume the main interview. Try Start interview again.');
      }
    } catch (err: unknown) {
      this.starting.set(false);
      this.voicePhase.set('idle');
      this.connectionStatus.set('idle');
      this.error.set(this.describeError(err));
    }
  }

  private async connectVoice(options: {
    purpose: VoiceAgentPurpose;
    firstMessage: string;
    phase: Exclude<VoicePhase, 'idle' | 'paused' | 'reviewing'>;
    extraDynamicVariables?: Record<string, string>;
  }): Promise<void> {
    if (!this.sessionId || !this.signedUrl) {
      this.starting.set(false);
      this.error.set('Missing voice connection details.');
      return;
    }

    this.starting.set(true);
    this.error.set(null);

    // Never stack two ConvAI sessions (pause miss + submit, or walkthrough → interview).
    this.disconnectIntent = 'none';
    await this.ensureDisconnected();

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

    const gen = ++this.connectionGen;

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
            if (gen !== this.connectionGen) {
              return 'Stale session';
            }

            const taskId = parameters?.taskId?.trim();
            if (!taskId) {
              return 'Missing taskId';
            }

            await this.openInteractiveTask(taskId);
            this.armInteractivePauseFallback();
            return (
              'Interactive task opened on screen. Speak the interactive question aloud now, ' +
              'briefly say the full exercise is on screen and you will continue after they submit. ' +
              'When that spoken intro is fully finished, call pause_for_interactive_task.'
            );
          },
          pause_for_interactive_task: async () => {
            if (gen !== this.connectionGen) {
              return 'Stale session';
            }

            this.requestActionAfterAgentSpeech('pause');
            return 'Pausing the voice call for the interactive exercise';
          },
          finish_interview: async () => {
            if (gen !== this.connectionGen) {
              return 'Stale session';
            }

            // Client-side hangup — do not wait for the candidate to answer the goodbye.
            this.requestActionAfterAgentSpeech('complete');
            return 'Interview closed';
          },
          finish_solution_walkthrough: async () => {
            if (gen !== this.connectionGen) {
              return 'Stale session';
            }

            // Return first so ConvAI can send the tool result; closing the socket
            // inside this handler caused "WebSocket is already CLOSING" and blocked resume.
            window.setTimeout(() => {
              if (gen !== this.connectionGen) {
                return;
              }

              void this.continueInterviewAfterWalkthrough();
            }, 450);
            return 'Returning to the main interview';
          },
        },
        onStatusChange: ({ status }) => {
          if (gen !== this.connectionGen) {
            return;
          }

          this.connectionStatus.set(status);
        },
        onModeChange: ({ mode }) => {
          if (gen !== this.connectionGen) {
            return;
          }

          this.mode.set(mode);
          this.onVoiceModeChange(mode);
        },
        onMessage: (payload) => {
          if (gen !== this.connectionGen) {
            return;
          }

          this.onAgentMessage(payload);
        },
        onError: () => {
          if (gen !== this.connectionGen) {
            return;
          }

          this.error.set('The voice interview connection failed. Try again.');
          this.starting.set(false);
        },
        onConnect: () => {
          if (gen !== this.connectionGen) {
            return;
          }

          this.starting.set(false);
          this.voicePhase.set(options.phase);
          if (options.phase === 'interview') {
            this.startHelperPolling();
          } else {
            this.stopHelperPolling();
            this.activeHelpers.set([]);
          }
        },
        onDisconnect: () => {
          if (gen !== this.connectionGen) {
            return;
          }

          this.connectionStatus.set('disconnected');
          this.onVoiceDisconnected();
        },
      });

      if (gen !== this.connectionGen) {
        // Superseded while startSession was resolving — drop the late session.
        const orphan = this.conversation;
        this.conversation = null;
        try {
          await orphan.endSession();
        } catch {
          // ignore
        }
        this.starting.set(false);
      }
    } catch {
      if (gen === this.connectionGen) {
        this.starting.set(false);
        this.error.set('Could not start the voice interview. Try again.');
      }
    }
  }

  /**
   * Ends any live ConvAI session and invalidates its callbacks so a replacement
   * agent cannot overlap with the previous one.
   */
  private async ensureDisconnected(): Promise<void> {
    this.clearPauseTimers();
    this.afterSpeechAction = 'none';
    this.interactivePauseArmed = false;

    const conversation = this.conversation;
    if (!conversation) {
      return;
    }

    this.connectionGen += 1;
    this.conversation = null;

    try {
      await Promise.race([
        conversation.endSession(),
        new Promise<void>((resolve) => setTimeout(resolve, 4000)),
      ]);
    } catch {
      // Session may already be closed.
    }

    // Brief beat so the browser can release the mic before the next startSession.
    await new Promise<void>((resolve) => setTimeout(resolve, 200));
  }

  /** Opens the exercise; agent must still speak, then call pause_for_interactive_task. */
  private armInteractivePauseFallback(): void {
    this.clearPauseTimers();
    this.interactivePauseArmed = true;
    this.afterSpeechAction = 'none';

    // Safety net if the agent never calls pause_for_interactive_task.
    this.pauseFallbackTimer = setTimeout(() => {
      if (this.interactivePauseArmed && this.conversation) {
        void this.disconnectAfterSpeech('pause');
      }
    }, 90_000);
  }

  /** Wait for current TTS (if any), then pause or fully end the interview. */
  private requestActionAfterAgentSpeech(action: Exclude<AfterSpeechAction, 'none'>): void {
    if (!this.conversation && action === 'pause' && !this.interactivePauseArmed) {
      return;
    }

    if (action === 'pause') {
      this.interactivePauseArmed = true;
    }

    this.clearPauseFallbackTimer();
    this.afterSpeechAction = action;
    const gen = this.connectionGen;

    if (this.mode() === 'speaking') {
      return;
    }

    this.clearPauseDebounceTimer();
    this.pauseDebounceTimer = setTimeout(() => {
      if (gen !== this.connectionGen) {
        return;
      }

      void this.disconnectAfterSpeech(action);
    }, 400);
  }

  private onVoiceModeChange(mode: Mode): void {
    if (this.afterSpeechAction === 'none') {
      return;
    }

    if (mode === 'speaking') {
      this.clearPauseDebounceTimer();
      return;
    }

    if (mode === 'listening') {
      const action = this.afterSpeechAction;
      const gen = this.connectionGen;
      this.clearPauseDebounceTimer();
      this.pauseDebounceTimer = setTimeout(() => {
        if (
          gen !== this.connectionGen ||
          this.afterSpeechAction !== action ||
          this.mode() !== 'listening'
        ) {
          return;
        }

        void this.disconnectAfterSpeech(action);
      }, 500);
    }
  }

  /** Backup: agent said goodbye but never called finish_interview / end_call. */
  private onAgentMessage(payload: { message?: string; source?: string; role?: string }): void {
    if (this.activePurpose !== 'interview' || this.voicePhase() === 'paused') {
      return;
    }

    if (this.afterSpeechAction !== 'none') {
      return;
    }

    const source = payload.source ?? payload.role;
    if (source && source !== 'ai' && source !== 'agent') {
      return;
    }

    const text = payload.message?.trim() ?? '';
    if (!text || !AGENT_GOODBYE_RE.test(text)) {
      return;
    }

    this.requestActionAfterAgentSpeech('complete');
  }

  private async disconnectAfterSpeech(action: Exclude<AfterSpeechAction, 'none'>): Promise<void> {
    if (action === 'pause' && this.voicePhase() === 'paused' && !this.conversation) {
      return;
    }

    this.interactivePauseArmed = false;
    this.afterSpeechAction = 'none';
    this.clearPauseTimers();

    if (action === 'pause') {
      this.voicePhase.set('paused');
      this.disconnectIntent = 'none';
      this.stopHelperPolling();
      this.activeHelpers.set([]);
      await this.ensureDisconnected();
      this.connectionStatus.set('idle');
      this.starting.set(false);
      return;
    }

    this.disconnectIntent = 'none';
    await this.ensureDisconnected();
    this.starting.set(false);
    void this.completeAndLeave();
  }

  private onVoiceDisconnected(): void {
    const intent = this.disconnectIntent;
    this.disconnectIntent = 'none';
    this.conversation = null;
    this.starting.set(false);

    // Intentional teardowns (pause / walkthrough→interview / hangup) use ensureDisconnected
    // and bump connectionGen so this handler is skipped. Remaining cases are agent/network drops.

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
    this.disconnectIntent = 'none';
    await this.ensureDisconnected();
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

  private clearPauseFallbackTimer(): void {
    if (this.pauseFallbackTimer) {
      clearTimeout(this.pauseFallbackTimer);
      this.pauseFallbackTimer = null;
    }
  }

  private clearPauseDebounceTimer(): void {
    if (this.pauseDebounceTimer) {
      clearTimeout(this.pauseDebounceTimer);
      this.pauseDebounceTimer = null;
    }
  }

  private clearPauseTimers(): void {
    this.clearPauseFallbackTimer();
    this.clearPauseDebounceTimer();
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
