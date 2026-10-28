import {
  AfterViewInit,
  Component,
  ElementRef,
  OnDestroy,
  ViewChild,
  inject,
  input,
  output,
  signal,
} from '@angular/core';
import { DomSanitizer, SafeResourceUrl } from '@angular/platform-browser';

const DRAWIO_ORIGIN = 'https://embed.diagrams.net';
const EMPTY_DIAGRAM =
  '<mxGraphModel><root><mxCell id="0"/><mxCell id="1" parent="0"/></root></mxGraphModel>';
const DRAWIO_EMBED_URL =
  `${DRAWIO_ORIGIN}/?embed=1&proto=json&libraries=1&spin=1&ui=atlas` +
  '&saveAndExit=0&noSaveBtn=1&noExitBtn=1';

interface DrawioMessage {
  event?: string;
  data?: string;
  xml?: string;
  format?: string;
}

export interface DrawioEditorState {
  ready: boolean;
  hasContent: boolean;
}

@Component({
  selector: 'app-drawio-editor',
  templateUrl: './drawio-editor.html',
  styleUrl: './drawio-editor.scss',
})
export class DrawioEditor implements AfterViewInit, OnDestroy {
  private readonly sanitizer = inject(DomSanitizer);

  readonly readOnly = input(false);
  readonly stateChange = output<DrawioEditorState>();

  @ViewChild('frame', { static: true })
  private readonly frameRef!: ElementRef<HTMLIFrameElement>;

  protected readonly editorUrl: SafeResourceUrl =
    this.sanitizer.bypassSecurityTrustResourceUrl(DRAWIO_EMBED_URL);

  protected readonly ready = signal(false);
  protected readonly hasContent = signal(false);

  private ignoreNextAutosave = false;

  private readonly onMessage = (event: MessageEvent): void => {
    this.handleMessage(event);
  };

  private exportWaiter:
    | {
        resolve: (file: File | null) => void;
        timer: ReturnType<typeof setTimeout>;
      }
    | null = null;

  ngAfterViewInit(): void {
    window.addEventListener('message', this.onMessage);
  }

  ngOnDestroy(): void {
    window.removeEventListener('message', this.onMessage);
    this.failExport(null);
  }

  /** PNG with embedded draw.io XML so the answer stays re-editable. */
  exportAnswer(): Promise<File | null> {
    const win = this.frameRef.nativeElement.contentWindow;
    if (!win || !this.ready()) {
      return Promise.resolve(null);
    }

    this.failExport(null);

    return new Promise((resolve) => {
      const timer = setTimeout(() => {
        if (this.exportWaiter?.resolve === resolve) {
          this.exportWaiter = null;
          resolve(null);
        }
      }, 30_000);

      this.exportWaiter = { resolve, timer };
      win.postMessage(
        JSON.stringify({
          action: 'export',
          format: 'xmlpng',
          spinKey: 'export',
          background: '#ffffff',
          border: 10,
        }),
        DRAWIO_ORIGIN,
      );
    });
  }

  private handleMessage(event: MessageEvent): void {
    if (event.origin !== DRAWIO_ORIGIN) {
      return;
    }

    if (event.source !== this.frameRef.nativeElement.contentWindow) {
      return;
    }

    const msg = this.parseMessage(event.data);
    if (!msg?.event) {
      return;
    }

    switch (msg.event) {
      case 'init':
        this.loadEmptyDiagram();
        break;
      case 'load':
        this.setReady(true);
        this.ignoreNextAutosave = true;
        break;
      case 'autosave':
        if (this.ignoreNextAutosave) {
          this.ignoreNextAutosave = false;
          break;
        }
        this.setHasContent(true);
        break;
      case 'export':
        this.onExport(msg);
        break;
      default:
        break;
    }
  }

  private loadEmptyDiagram(): void {
    const win = this.frameRef.nativeElement.contentWindow;
    if (!win) {
      return;
    }

    win.postMessage(
      JSON.stringify({
        action: 'load',
        xml: EMPTY_DIAGRAM,
        autosave: 1,
      }),
      DRAWIO_ORIGIN,
    );
  }

  private onExport(msg: DrawioMessage): void {
    const waiter = this.exportWaiter;
    if (!waiter) {
      return;
    }

    clearTimeout(waiter.timer);
    this.exportWaiter = null;

    const dataUri = msg.data;
    if (!dataUri?.startsWith('data:')) {
      waiter.resolve(null);
      return;
    }

    try {
      waiter.resolve(dataUriToFile(dataUri, 'answer.drawio.png'));
    } catch {
      waiter.resolve(null);
    }
  }

  private setReady(value: boolean): void {
    if (this.ready() === value) {
      return;
    }

    this.ready.set(value);
    this.emitState();
  }

  private setHasContent(value: boolean): void {
    if (this.hasContent() === value) {
      return;
    }

    this.hasContent.set(value);
    this.emitState();
  }

  private emitState(): void {
    this.stateChange.emit({
      ready: this.ready(),
      hasContent: this.hasContent(),
    });
  }

  private failExport(file: File | null): void {
    if (!this.exportWaiter) {
      return;
    }

    clearTimeout(this.exportWaiter.timer);
    const { resolve } = this.exportWaiter;
    this.exportWaiter = null;
    resolve(file);
  }

  private parseMessage(data: unknown): DrawioMessage | null {
    if (typeof data !== 'string' || data.length === 0) {
      return null;
    }

    try {
      return JSON.parse(data) as DrawioMessage;
    } catch {
      return null;
    }
  }
}

function dataUriToFile(dataUri: string, filename: string): File {
  const comma = dataUri.indexOf(',');
  if (comma < 0) {
    throw new Error('Invalid data URI');
  }

  const header = dataUri.slice(0, comma);
  const payload = dataUri.slice(comma + 1);
  const mime = header.match(/data:([^;]+)/)?.[1] ?? 'image/png';
  const binary = atob(payload);
  const bytes = new Uint8Array(binary.length);
  for (let i = 0; i < binary.length; i++) {
    bytes[i] = binary.charCodeAt(i);
  }

  return new File([bytes], filename, { type: mime });
}
