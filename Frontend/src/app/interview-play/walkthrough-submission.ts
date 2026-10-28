/** ElevenLabs dynamic-variable budget for submitted code/text. ~150–250 typical LOC. */
export const WALKTHROUGH_SOLUTION_MAX_CHARS = 10_000;
export const WALKTHROUGH_TASK_PROMPT_MAX_CHARS = 8_000;

/** Client safety ceiling while polling vision review (SF harness timeout is 180s). */
export const VISION_REVIEW_POLL_MS = 2000;
export const VISION_REVIEW_MAX_WAIT_MS = 210_000;

export interface InteractiveSubmissionPayload {
  expectedInput: string;
  module: string;
  taskPrompt: string;
  taskId: string;
  /** Code/text snapshot, or vision description once Ready. */
  submittedSolution: string | null;
  /** True when API started AgentCore vision describe — poll before walkthrough. */
  awaitVisionReview: boolean;
}

export function truncateForWalkthrough(text: string, maxChars: number): string {
  if (text.length <= maxChars) {
    return text;
  }

  return `${text.slice(0, maxChars)}\n…[truncated]`;
}

export function formatMultiFileWalkthroughSource(
  files: ReadonlyArray<{ name: string; content: string }>,
): string {
  if (files.length === 0) {
    return '';
  }

  if (files.length === 1) {
    return files[0].content.replace(/\s+$/, '');
  }

  return files
    .map((file) => `===== ${file.name} =====\n${file.content.replace(/\s+$/, '')}\n`)
    .join('\n');
}
