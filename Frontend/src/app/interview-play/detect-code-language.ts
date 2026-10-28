import type { CodeEditorLanguage } from './code-editor';

const HINTS: { language: CodeEditorLanguage; pattern: RegExp }[] = [
  { language: 'python', pattern: /\bpython\b|def\s+\w+\s*\(/i },
  { language: 'typescript', pattern: /\btypescript\b|\binterface\b|\btype\s+\w+\s*=/i },
  { language: 'javascript', pattern: /\bjavascript\b|\bnode\.?js\b|function\s+\w+\s*\(/i },
  { language: 'java', pattern: /\bjava\b|public\s+class\b|System\.out/i },
  { language: 'csharp', pattern: /\bc#\b|\.net\b|Console\.Write/i },
  { language: 'go', pattern: /\bgolang\b|\bgo\b|func\s+main\s*\(/i },
  { language: 'cpp', pattern: /\bc\+\+\b|\bcpp\b|#include\s*</i },
  { language: 'rust', pattern: /\brust\b|fn\s+main\s*\(/i },
  { language: 'sql', pattern: /\bsql\b|select\s+.+\s+from\b/i },
];

export function detectCodeLanguage(
  promptMarkdown: string | null | undefined,
): CodeEditorLanguage {
  const text = promptMarkdown ?? '';
  for (const hint of HINTS) {
    if (hint.pattern.test(text)) {
      return hint.language;
    }
  }

  return 'python';
}
