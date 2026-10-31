import { Injectable, inject, signal } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import { InterviewSessionsService } from './interview-sessions.service';

const RECORDER_TYPES = [
  'video/webm;codecs=vp9,opus',
  'video/webm;codecs=vp8,opus',
  'video/webm',
  'video/mp4',
];

@Injectable({
  providedIn: 'root',
})
export class CandidateRecordingService {
  private readonly sessions = inject(InterviewSessionsService);

  readonly previewStream = signal<MediaStream | null>(null);
  readonly recording = signal(false);
  readonly uploading = signal(false);
  readonly cameraError = signal<string | null>(null);

  private recorder: MediaRecorder | null = null;
  private chunks: Blob[] = [];
  private recorderMime = 'video/webm';
  private startInFlight: Promise<void> | null = null;
  private uploaded = false;

  async start(): Promise<void> {
    if (this.recording() && this.previewStream()) {
      return;
    }

    if (this.startInFlight) {
      return this.startInFlight;
    }

    this.startInFlight = this.startInternal();
    try {
      await this.startInFlight;
    } finally {
      this.startInFlight = null;
    }
  }

  async stopAndUpload(sessionId: string): Promise<void> {
    if (this.uploaded || this.uploading()) {
      return;
    }

    const blob = await this.stopToBlob();
    this.stopTracks();
    if (!blob || blob.size === 0) {
      return;
    }

    this.uploading.set(true);
    try {
      const contentType = blob.type || this.recorderMime || 'video/webm';
      const signed = await firstValueFrom(this.sessions.getRecordingUploadUrl(sessionId, contentType));
      // Raw fetch — Angular HttpClient would attach the JWT and break the S3 signature.
      const response = await fetch(signed.uploadUrl, {
        method: 'PUT',
        headers: { 'Content-Type': signed.contentType },
        body: blob,
      });
      if (!response.ok) {
        throw new Error(`Recording upload failed (${response.status}).`);
      }

      this.uploaded = true;
    } finally {
      this.uploading.set(false);
    }
  }

  describeError(err: unknown): string {
    if (err instanceof DOMException) {
      if (err.name === 'NotAllowedError' || err.name === 'SecurityError') {
        return 'Camera access was not granted.';
      }

      if (err.name === 'NotFoundError') {
        return 'No camera was found. Connect a camera and try again.';
      }

      if (err.name === 'NotReadableError') {
        return 'The camera is already in use by another application.';
      }
    }

    if (err instanceof Error && err.message) {
      return err.message;
    }

    return this.cameraError() || 'Could not start the camera.';
  }

  private async startInternal(): Promise<void> {
    this.cameraError.set(null);
    this.uploaded = false;

    const stream = await this.requestCameraStream();
    if (!stream) {
      return;
    }

    this.previewStream.set(stream);

    try {
      const mimeType = RECORDER_TYPES.find((type) => MediaRecorder.isTypeSupported(type));
      const recorder = mimeType
        ? new MediaRecorder(stream, {
            mimeType,
            videoBitsPerSecond: 1_200_000,
            audioBitsPerSecond: 64_000,
          })
        : new MediaRecorder(stream, {
            videoBitsPerSecond: 1_200_000,
            audioBitsPerSecond: 64_000,
          });

      this.recorderMime = recorder.mimeType || mimeType || 'video/webm';
      this.chunks = [];
      recorder.ondataavailable = (event) => {
        if (event.data.size > 0) {
          this.chunks.push(event.data);
        }
      };
      recorder.start(2000);
      this.recorder = recorder;
      this.recording.set(true);
    } catch {
      this.stopTracks();
    }
  }

  private async requestCameraStream(): Promise<MediaStream | null> {
    const video = {
      facingMode: 'user' as const,
      width: { ideal: 1280 },
      height: { ideal: 720 },
    };

    try {
      return await navigator.mediaDevices.getUserMedia({ video, audio: true });
    } catch {
      try {
        return await navigator.mediaDevices.getUserMedia({ video, audio: false });
      } catch {
        return null;
      }
    }
  }

  private stopToBlob(): Promise<Blob | null> {
    const recorder = this.recorder;
    this.recorder = null;
    this.recording.set(false);

    if (!recorder || recorder.state === 'inactive') {
      return Promise.resolve(this.assemble());
    }

    return new Promise((resolve) => {
      recorder.addEventListener('stop', () => resolve(this.assemble()), { once: true });
      recorder.stop();
    });
  }

  private assemble(): Blob | null {
    if (!this.chunks.length) {
      return null;
    }

    const type = this.chunks[0]?.type || this.recorderMime;
    const blob = new Blob(this.chunks, { type });
    this.chunks = [];
    return blob;
  }

  private stopTracks(): void {
    const stream = this.previewStream();
    stream?.getTracks().forEach((track) => track.stop());
    this.previewStream.set(null);
  }
}
