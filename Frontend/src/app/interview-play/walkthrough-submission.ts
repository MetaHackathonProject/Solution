/** ElevenLabs dynamic-variable budget for submitted code/text. ~150–250 typical LOC. */
export const WALKTHROUGH_SOLUTION_MAX_CHARS = 10_000;
export const WALKTHROUGH_TASK_PROMPT_MAX_CHARS = 8_000;

export interface InteractiveSubmissionPayload {
  expectedInput: string;
  module: string;
  taskPrompt: string;
  /** Code/text snapshot for dynamic var; null when only an image/file was submitted. */
  submittedSolution: string | null;
  /**
   * Diagram/schema PNG (or other image/PDF) uploaded to the walkthrough conversation
   * after connect via uploadFile + sendMultimodalMessage.
   */
  submittedImage: File | null;
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

/** Files ElevenLabs ConvAI conversation upload accepts for vision/context. */
export function isWalkthroughAttachableFile(file: File): boolean {
  const type = file.type.toLowerCase();
  if (type.startsWith('image/')) {
    return true;
  }

  if (type === 'application/pdf') {
    return true;
  }

  // draw.io export may omit type in some browsers; trust the extension.
  return /\.(png|jpe?g|gif|webp|pdf)$/i.test(file.name);
}
