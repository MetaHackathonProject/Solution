import { Injectable, signal } from '@angular/core';

@Injectable({ providedIn: 'root' })
export class InterviewerTalkingHead {
  readonly mouth = signal(0);
  private lastAudioAt = 0;

  feedAudio(payload: unknown): void {
    const pcm = toPcm16(payload);
    if (!pcm || pcm.byteLength < 4) {
      return;
    }

    this.mouth.set(levelToMouth(rms(pcm)));
    this.lastAudioAt = Date.now();
  }

  feedAlignment(alignment: { chars?: string[] }): void {
    const letters = (alignment.chars ?? []).filter((char) => /[a-z]/i.test(char));
    if (!letters.length) {
      return;
    }

    const vowels = letters.filter((char) => /[aeiouy]/i.test(char)).length;
    this.mouth.set(clamp(0.28 + (vowels / letters.length) * 0.7, 0.2, 1));
    this.lastAudioAt = Date.now();
  }

  speakFallback(): void {
    if (Date.now() - this.lastAudioAt < 160) {
      return;
    }

    const beat = Date.now() / 150;
    this.mouth.set(0.2 + 0.55 * (0.5 + 0.5 * Math.sin(beat) * Math.sin(beat * 1.65)));
  }

  notifySpeechEnd(): void {
    this.mouth.set(0);
  }
}

function clamp(value: number, min: number, max: number): number {
  return Math.min(max, Math.max(min, value));
}

function levelToMouth(rmsLevel: number): number {
  return clamp(0.16 + rmsLevel * 3.4, 0.12, 1);
}

function rms(pcm: ArrayBuffer): number {
  const samples = new Int16Array(pcm);
  if (!samples.length) {
    return 0;
  }

  let sum = 0;
  for (let i = 0; i < samples.length; i++) {
    const sample = (samples[i] ?? 0) / 32768;
    sum += sample * sample;
  }

  return Math.sqrt(sum / samples.length);
}

function toPcm16(payload: unknown): ArrayBuffer | null {
  if (payload instanceof ArrayBuffer) {
    return payload.byteLength >= 4 ? payload : null;
  }

  if (ArrayBuffer.isView(payload)) {
    const view = payload as ArrayBufferView;
    return view.byteLength >= 4
      ? view.buffer.slice(view.byteOffset, view.byteOffset + view.byteLength)
      : null;
  }

  const raw =
    typeof payload === 'string'
      ? payload
      : payload && typeof payload === 'object' && 'audio_base_64' in payload
        ? String((payload as { audio_base_64?: unknown }).audio_base_64 ?? '')
        : payload && typeof payload === 'object' && 'audio' in payload
          ? String((payload as { audio?: unknown }).audio ?? '')
          : '';

  if (!raw) {
    return null;
  }

  const binary = atob(raw);
  const bytes = new Uint8Array(binary.length);
  for (let i = 0; i < binary.length; i++) {
    bytes[i] = binary.charCodeAt(i);
  }

  return bytes.buffer;
}
