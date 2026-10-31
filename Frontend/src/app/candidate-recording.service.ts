import { Injectable, inject, signal } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import { InterviewSessionsService } from './interview-sessions.service';

const RECORDER_TYPES = [
  'video/mp4',
  'video/webm;codecs=vp9,opus',
  'video/webm;codecs=vp8,opus',
  'video/webm',
];

const VIDEO_MODES: MediaTrackConstraints[] = [
  { width: { ideal: 640 }, height: { ideal: 480 }, frameRate: { ideal: 30 } },
  { width: { ideal: 1280 }, height: { ideal: 720 }, frameRate: { ideal: 30 } },
  {},
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
  readonly cameraLabel = signal('');
  readonly selectedDeviceId = signal('');
  readonly cameras = signal<MediaDeviceInfo[]>([]);
  readonly feedHint = signal<string | null>(null);

  private recorder: MediaRecorder | null = null;
  private chunks: Blob[] = [];
  private stills: Blob[] = [];
  private recorderMime = 'video/webm';
  private startInFlight: Promise<void> | null = null;
  private uploaded = false;

  async start(deviceId?: string): Promise<void> {
    if (this.previewStream() && !deviceId) {
      return;
    }

    if (this.startInFlight) {
      return this.startInFlight;
    }

    this.startInFlight = this.startInternal(deviceId);
    try {
      await this.startInFlight;
    } finally {
      this.startInFlight = null;
    }
  }

  async switchTo(deviceId: string): Promise<void> {
    if (!deviceId || deviceId === this.selectedDeviceId()) {
      return;
    }

    await this.stopRecorderKeepChunks();
    this.stopTracks();
    this.uploaded = false;
    await this.start(deviceId);
  }

  addStill(blob: Blob): void {
    if (this.stills.length >= 8 || blob.size < 800) {
      return;
    }

    this.stills.push(blob);
  }

  async stopPreview(): Promise<void> {
    await this.stopRecorderKeepChunks();
    this.chunks = [];
    this.stills = [];
    this.stopTracks();
    this.uploaded = false;
  }

  beginRecording(): void {
    const stream = this.previewStream();
    if (!stream || this.recorder) {
      return;
    }

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

  async stopAndUpload(sessionId: string): Promise<void> {
    if (this.uploaded || this.uploading()) {
      return;
    }

    const blob = await this.stopToBlob();
    const frames = [...this.stills];
    this.stopTracks();
    if ((!blob || blob.size === 0) && frames.length === 0) {
      return;
    }

    this.uploading.set(true);
    try {
      if (blob && blob.size > 0) {
        await this.putObject(sessionId, blob, blob.type || this.recorderMime || 'video/webm', 'segment');
      }

      for (const frame of frames) {
        await this.putObject(sessionId, frame, frame.type || 'image/jpeg', 'frame');
      }

      this.uploaded = true;
      this.stills = [];
    } finally {
      this.uploading.set(false);
    }
  }

  private async putObject(
    sessionId: string,
    body: Blob,
    contentType: string,
    kind: 'segment' | 'frame',
  ): Promise<void> {
    const signed = await firstValueFrom(this.sessions.getRecordingUploadUrl(sessionId, contentType, kind));
    const response = await fetch(signed.uploadUrl, {
      method: 'PUT',
      headers: { 'Content-Type': signed.contentType },
      body,
    });
    if (!response.ok) {
      throw new Error(`Recording upload failed (${response.status}).`);
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

  private async startInternal(deviceId?: string): Promise<void> {
    this.cameraError.set(null);
    this.feedHint.set(null);
    this.uploaded = false;

    const opened = await this.openWorkingVideo(deviceId);
    if (!opened) {
      return;
    }

    const videoTrack = opened.stream.getVideoTracks()[0];
    this.previewStream.set(opened.stream);
    this.cameraLabel.set(videoTrack?.label ?? '');
    this.selectedDeviceId.set(videoTrack?.getSettings().deviceId || deviceId || '');
    if (opened.dead) {
      this.feedHint.set(
        'This camera is sending a blank frame. If you are on DroidCam, use File → Exit in the client, reopen it, then pick the camera again. A phone reconnect or PC reboot usually resyncs the feed.',
      );
    }

    await this.refreshDevices();
  }

  private async openWorkingVideo(preferredId?: string): Promise<{ stream: MediaStream; dead: boolean } | null> {
    const probe = await this.getUserMedia({ video: true });
    if (!probe) {
      return null;
    }

    await this.refreshDevices();
    const cameras = this.cameras();
    const probeId = probe.getVideoTracks()[0]?.getSettings().deviceId;
    const rest = cameras.filter((camera) => camera.deviceId !== preferredId);
    const virtual = (label: string) => /droidcam|obs virtual/i.test(label);
    const order = [
      ...cameras.filter((camera) => camera.deviceId === preferredId),
      ...rest.filter((camera) => !virtual(camera.label)),
      ...rest.filter((camera) => virtual(camera.label)),
    ];
    const deviceIds = order.map((camera) => camera.deviceId);
    if (!deviceIds.length && probeId) {
      deviceIds.push(probeId);
    }

    this.stopStream(probe);

    let fallback: MediaStream | null = null;
    for (const id of deviceIds) {
      for (const mode of VIDEO_MODES) {
        const stream = await this.getUserMedia({
          video: { deviceId: { exact: id }, ...mode },
        });
        if (!stream) {
          continue;
        }

        const dead = await isUnusableStream(stream);
        if (!dead) {
          if (fallback) {
            this.stopStream(fallback);
          }

          return { stream, dead: false };
        }

        if (fallback) {
          this.stopStream(fallback);
        }

        fallback = stream;
      }
    }

    return fallback ? { stream: fallback, dead: true } : null;
  }

  private async refreshDevices(): Promise<void> {
    try {
      const devices = await navigator.mediaDevices.enumerateDevices();
      this.cameras.set(devices.filter((device) => device.kind === 'videoinput' && device.deviceId));
    } catch {
      this.cameras.set([]);
    }
  }

  private async getUserMedia(constraints: MediaStreamConstraints): Promise<MediaStream | null> {
    try {
      return await navigator.mediaDevices.getUserMedia(constraints);
    } catch {
      return null;
    }
  }

  private async stopRecorderKeepChunks(): Promise<void> {
    const recorder = this.recorder;
    this.recorder = null;
    this.recording.set(false);
    if (!recorder || recorder.state === 'inactive') {
      return;
    }

    await new Promise<void>((resolve) => {
      recorder.addEventListener('stop', () => resolve(), { once: true });
      recorder.stop();
    });
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
    this.stopStream(this.previewStream());
    this.previewStream.set(null);
    this.cameraLabel.set('');
    this.feedHint.set(null);
  }

  private stopStream(stream: MediaStream | null): void {
    stream?.getTracks().forEach((track) => track.stop());
  }
}

function isUnusableStream(stream: MediaStream): Promise<boolean> {
  const video = document.createElement('video');
  video.muted = true;
  video.autoplay = true;
  video.playsInline = true;
  video.srcObject = stream;
  video.setAttribute(
    'style',
    'position:fixed;bottom:0;left:0;width:160px;height:120px;opacity:0.02;pointer-events:none;z-index:-1',
  );
  document.body.appendChild(video);

  return new Promise((resolve) => {
    const finish = (dead: boolean) => {
      window.clearTimeout(timer);
      video.pause();
      video.srcObject = null;
      video.remove();
      resolve(dead);
    };

    const sample = () => {
      if (video.videoWidth < 2) {
        return true;
      }

      const canvas = document.createElement('canvas');
      canvas.width = 16;
      canvas.height = 16;
      const ctx = canvas.getContext('2d', { willReadFrequently: true });
      if (!ctx) {
        return true;
      }

      ctx.drawImage(video, 0, 0, 16, 16);
      const pixels = ctx.getImageData(0, 0, 16, 16).data;
      let green = 0;
      let black = 0;
      for (let i = 0; i < pixels.length; i += 4) {
        const r = pixels[i];
        const g = pixels[i + 1];
        const b = pixels[i + 2];
        if (g > 70 && g > r + 25 && g > b + 25) {
          green += 1;
        }

        if (r < 22 && g < 22 && b < 22) {
          black += 1;
        }
      }

      return green > 180 || black > 200;
    };

    const timer = window.setTimeout(() => finish(sample()), 2200);
    void video.play().then(async () => {
      await wait(800);
      if (!sample()) {
        finish(false);
        return;
      }

      await wait(900);
      finish(sample());
    }).catch(() => finish(true));
  });
}

function wait(ms: number): Promise<void> {
  return new Promise((resolve) => window.setTimeout(resolve, ms));
}
