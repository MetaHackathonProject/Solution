import {
  AfterViewInit,
  Component,
  ElementRef,
  OnDestroy,
  ViewChild,
  effect,
  input,
  output,
  signal,
} from '@angular/core';
import loader from '@monaco-editor/loader';
import type * as Monaco from 'monaco-editor';

export type CodeEditorLanguage =
  | 'javascript'
  | 'typescript'
  | 'python'
  | 'java'
  | 'csharp'
  | 'go'
  | 'cpp'
  | 'rust'
  | 'sql'
  | 'json'
  | 'plaintext';

interface EditorFile {
  id: string;
  name: string;
  language: CodeEditorLanguage;
  model: Monaco.editor.ITextModel;
}

const LANGUAGE_OPTIONS: { id: CodeEditorLanguage; label: string }[] = [
  { id: 'python', label: 'Python' },
  { id: 'javascript', label: 'JavaScript' },
  { id: 'typescript', label: 'TypeScript' },
  { id: 'java', label: 'Java' },
  { id: 'csharp', label: 'C#' },
  { id: 'go', label: 'Go' },
  { id: 'cpp', label: 'C++' },
  { id: 'rust', label: 'Rust' },
  { id: 'sql', label: 'SQL' },
  { id: 'json', label: 'JSON' },
  { id: 'plaintext', label: 'Plain text' },
];

const EXTENSION_BY_LANGUAGE: Record<CodeEditorLanguage, string> = {
  python: 'py',
  javascript: 'js',
  typescript: 'ts',
  java: 'java',
  csharp: 'cs',
  go: 'go',
  cpp: 'cpp',
  rust: 'rs',
  sql: 'sql',
  json: 'json',
  plaintext: 'txt',
};

const LANGUAGE_BY_EXTENSION: Record<string, CodeEditorLanguage> = {
  py: 'python',
  js: 'javascript',
  mjs: 'javascript',
  cjs: 'javascript',
  ts: 'typescript',
  tsx: 'typescript',
  java: 'java',
  cs: 'csharp',
  go: 'go',
  cpp: 'cpp',
  cc: 'cpp',
  cxx: 'cpp',
  h: 'cpp',
  hpp: 'cpp',
  rs: 'rust',
  sql: 'sql',
  json: 'json',
  txt: 'plaintext',
  md: 'plaintext',
};

const STARTERS: Partial<Record<CodeEditorLanguage, string>> = {
  python: 'def solve():\n    pass\n\n\nif __name__ == "__main__":\n    solve()\n',
  javascript: 'function solve() {\n  // write your solution\n}\n\nsolve();\n',
  typescript:
    'function solve(): void {\n  // write your solution\n}\n\nsolve();\n',
  java: 'class Solution {\n    public static void main(String[] args) {\n        // write your solution\n    }\n}\n',
  csharp:
    'using System;\n\nclass Solution {\n    static void Main() {\n        // write your solution\n    }\n}\n',
  go: 'package main\n\nimport "fmt"\n\nfunc main() {\n\t// write your solution\n\tfmt.Println()\n}\n',
  cpp: '#include <bits/stdc++.h>\nusing namespace std;\n\nint main() {\n    ios::sync_with_stdio(false);\n    cin.tie(nullptr);\n    // write your solution\n    return 0;\n}\n',
  rust: 'fn main() {\n    // write your solution\n}\n',
  sql: '-- write your query\nSELECT 1;\n',
  json: '{\n  \n}\n',
  plaintext: '',
};

@Component({
  selector: 'app-code-editor',
  templateUrl: './code-editor.html',
  styleUrl: './code-editor.scss',
})
export class CodeEditor implements AfterViewInit, OnDestroy {
  readonly initialValue = input<string>('');
  readonly preferredLanguage = input<CodeEditorLanguage | null>(null);
  readonly readOnly = input(false);
  readonly valueChange = output<string>();

  @ViewChild('host', { static: true })
  private readonly hostRef!: ElementRef<HTMLDivElement>;

  protected readonly languages = LANGUAGE_OPTIONS;
  protected readonly language = signal<CodeEditorLanguage>('python');
  protected readonly files = signal<EditorFile[]>([]);
  protected readonly activeFileId = signal<string | null>(null);
  protected readonly ready = signal(false);
  protected readonly loadError = signal<string | null>(null);
  protected readonly addingFile = signal(false);
  protected readonly newFileName = signal('');

  private editor: Monaco.editor.IStandaloneCodeEditor | null = null;
  private monaco: typeof Monaco | null = null;
  private resizeObserver: ResizeObserver | null = null;
  private disposed = false;
  private nextFileIndex = 1;

  constructor() {
    effect(() => {
      const readOnly = this.readOnly();
      this.editor?.updateOptions({ readOnly });
    });
  }

  ngAfterViewInit(): void {
    const preferred = this.preferredLanguage();
    if (preferred) {
      this.language.set(preferred);
    }

    void this.mountEditor();
  }

  ngOnDestroy(): void {
    this.disposed = true;
    this.resizeObserver?.disconnect();
    this.resizeObserver = null;
    this.editor?.dispose();
    this.editor = null;
    for (const file of this.files()) {
      file.model.dispose();
    }
  }

  protected activeFile(): EditorFile | null {
    const id = this.activeFileId();
    return this.files().find((file) => file.id === id) ?? null;
  }

  protected onLanguageChange(event: Event): void {
    const select = event.target as HTMLSelectElement;
    const next = select.value as CodeEditorLanguage;
    this.setActiveLanguage(next, true);
  }

  protected selectFile(fileId: string): void {
    if (fileId === this.activeFileId()) {
      return;
    }

    this.activeFileId.set(fileId);
    const file = this.files().find((item) => item.id === fileId);
    if (!file || !this.editor) {
      return;
    }

    this.editor.setModel(file.model);
    this.language.set(file.language);
    this.emitValue();
  }

  protected startAddFile(): void {
    if (this.readOnly()) {
      return;
    }

    const language = this.language();
    const extension = EXTENSION_BY_LANGUAGE[language];
    this.newFileName.set(`file${this.nextFileIndex}.${extension}`);
    this.addingFile.set(true);
  }

  protected cancelAddFile(): void {
    this.addingFile.set(false);
    this.newFileName.set('');
  }

  protected onNewFileNameInput(event: Event): void {
    this.newFileName.set((event.target as HTMLInputElement).value);
  }

  protected confirmAddFile(): void {
    if (!this.monaco || this.readOnly()) {
      return;
    }

    const rawName = this.newFileName().trim();
    const name = this.normalizeFileName(rawName);
    if (!name) {
      return;
    }

    if (this.files().some((file) => file.name.toLowerCase() === name.toLowerCase())) {
      this.loadError.set(`A file named ${name} already exists.`);
      return;
    }

    this.loadError.set(null);
    const language = languageFromFileName(name) ?? this.language();
    const file = this.createFile(name, language, STARTERS[language] ?? '');
    this.files.update((items) => [...items, file]);
    this.nextFileIndex += 1;
    this.addingFile.set(false);
    this.newFileName.set('');
    this.selectFile(file.id);
  }

  protected removeFile(fileId: string, event: Event): void {
    event.stopPropagation();
    if (this.readOnly() || this.files().length <= 1) {
      return;
    }

    const remaining = this.files().filter((file) => file.id !== fileId);
    const removed = this.files().find((file) => file.id === fileId);
    removed?.model.dispose();
    this.files.set(remaining);

    if (this.activeFileId() === fileId) {
      this.selectFile(remaining[0].id);
    } else {
      this.emitValue();
    }
  }

  protected formatDocument(): void {
    void this.editor?.getAction('editor.action.formatDocument')?.run();
  }

  getValue(): string {
    return this.serializeFiles();
  }

  private async mountEditor(): Promise<void> {
    try {
      const base = document.querySelector('base')?.getAttribute('href') || '/';
      const vsPath = new URL('assets/monaco/vs', window.location.origin + base)
        .pathname.replace(/\/$/, '');

      loader.config({
        paths: { vs: vsPath },
      });

      const monaco = await loader.init();
      if (this.disposed) {
        return;
      }

      this.monaco = monaco;
      const language = this.language();
      const startingValue = this.initialValue().trim()
        ? this.initialValue()
        : (STARTERS[language] ?? '');
      const defaultName = `main.${EXTENSION_BY_LANGUAGE[language]}`;
      const firstFile = this.createFile(defaultName, language, startingValue);
      this.files.set([firstFile]);
      this.activeFileId.set(firstFile.id);
      this.nextFileIndex = 2;

      const editor = monaco.editor.create(this.hostRef.nativeElement, {
        model: firstFile.model,
        theme: 'vs',
        automaticLayout: true,
        minimap: { enabled: true, scale: 1 },
        fontSize: 14,
        lineHeight: 22,
        tabSize: 2,
        insertSpaces: true,
        wordWrap: 'on',
        scrollBeyondLastLine: false,
        renderLineHighlight: 'line',
        padding: { top: 12, bottom: 12 },
        readOnly: this.readOnly(),
        scrollbar: {
          verticalScrollbarSize: 10,
          horizontalScrollbarSize: 10,
        },
      });
      this.editor = editor;

      editor.onDidChangeModelContent(() => {
        this.emitValue();
      });

      this.resizeObserver = new ResizeObserver(() => {
        editor.layout();
      });
      this.resizeObserver.observe(this.hostRef.nativeElement);

      this.emitValue();
      this.ready.set(true);
    } catch {
      this.loadError.set('Could not load the code editor. Refresh and try again.');
    }
  }

  private createFile(
    name: string,
    language: CodeEditorLanguage,
    value: string,
  ): EditorFile {
    if (!this.monaco) {
      throw new Error('Monaco is not ready');
    }

    const uri = this.monaco.Uri.parse(`inmemory://interview/${encodeURIComponent(name)}`);
    const existing = this.monaco.editor.getModel(uri);
    existing?.dispose();

    const model = this.monaco.editor.createModel(value, language, uri);
    return {
      id: crypto.randomUUID(),
      name,
      language,
      model,
    };
  }

  private setActiveLanguage(language: CodeEditorLanguage, seedStarter: boolean): void {
    this.language.set(language);
    const file = this.activeFile();
    if (!this.monaco || !file) {
      return;
    }

    this.monaco.editor.setModelLanguage(file.model, language);
    file.language = language;

    const extension = EXTENSION_BY_LANGUAGE[language];
    const baseName = file.name.includes('.')
      ? file.name.slice(0, file.name.lastIndexOf('.'))
      : file.name;
    const nextName = `${baseName || 'main'}.${extension}`;
    if (
      nextName !== file.name &&
      !this.files().some(
        (item) =>
          item.id !== file.id && item.name.toLowerCase() === nextName.toLowerCase(),
      )
    ) {
      file.name = nextName;
      this.files.update((items) => items.map((item) => (item.id === file.id ? file : item)));
    }

    if (seedStarter) {
      const current = file.model.getValue().trim();
      const wasStarter = Object.values(STARTERS).some(
        (starter) => starter && current === starter.trim(),
      );
      if (!current || wasStarter) {
        file.model.setValue(STARTERS[language] ?? '');
      }
    }

    this.emitValue();
  }

  private normalizeFileName(value: string): string | null {
    const cleaned = value.trim().replaceAll('\\', '/').split('/').pop()?.trim();
    if (!cleaned || cleaned === '.' || cleaned === '..') {
      return null;
    }

    if (!/^[\w.-]+$/.test(cleaned)) {
      this.loadError.set('Use letters, numbers, dots, dashes, or underscores in file names.');
      return null;
    }

    return cleaned;
  }

  private serializeFiles(): string {
    const files = this.files();
    if (files.length === 1) {
      return files[0].model.getValue();
    }

    return files
      .map((file) => `===== ${file.name} =====\n${file.model.getValue().replace(/\s+$/, '')}\n`)
      .join('\n');
  }

  private emitValue(): void {
    this.valueChange.emit(this.serializeFiles());
  }
}

function languageFromFileName(name: string): CodeEditorLanguage | null {
  const extension = name.includes('.')
    ? name.slice(name.lastIndexOf('.') + 1).toLowerCase()
    : '';
  return LANGUAGE_BY_EXTENSION[extension] ?? null;
}
