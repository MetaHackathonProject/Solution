import { Component, ElementRef, OnDestroy, afterRenderEffect, inject, viewChild } from '@angular/core';
import { CandidateRecordingService } from '../candidate-recording.service';

@Component({
  selector: 'app-candidate-camera',
  templateUrl: './candidate-camera.html',
  styleUrl: './candidate-camera.scss',
})
export class CandidateCamera implements OnDestroy {
  protected readonly recording = inject(CandidateRecordingService);
  private readonly preview = viewChild<ElementRef<HTMLVideoElement>>('preview');
  private stillTimer: number | null = null;

  constructor() {
    afterRenderEffect((onCleanup) => {
      const video = this.preview()?.nativeElement;
      const stream = this.recording.previewStream();
      if (!video) {
        return;
      }

      if (video.srcObject !== stream) {
        video.srcObject = stream;
      }

      if (!stream) {
        this.clearStillTimer();
        return;
      }

      const onPlaying = () => this.recording.beginRecording();
      video.addEventListener('playing', onPlaying, { once: true });
      void video.play().catch(() => undefined);
      const fallback = window.setTimeout(() => this.recording.beginRecording(), 2000);
      this.startStillCapture(video);

      onCleanup(() => {
        video.removeEventListener('playing', onPlaying);
        window.clearTimeout(fallback);
        this.clearStillTimer();
      });
    });
  }

  ngOnDestroy(): void {
    this.clearStillTimer();
  }

  private startStillCapture(video: HTMLVideoElement): void {
    this.clearStillTimer();
    window.setTimeout(() => this.captureStill(video), 2500);
    this.stillTimer = window.setInterval(() => this.captureStill(video), 12000) as unknown as number;
  }

  private captureStill(video: HTMLVideoElement): void {
    if (video.readyState < HTMLMediaElement.HAVE_CURRENT_DATA || video.videoWidth < 8) {
      return;
    }

    const canvas = document.createElement('canvas');
    canvas.width = video.videoWidth;
    canvas.height = video.videoHeight;
    const ctx = canvas.getContext('2d');
    if (!ctx) {
      return;
    }

    ctx.drawImage(video, 0, 0);
    canvas.toBlob((blob) => {
      if (blob) {
        this.recording.addStill(blob);
      }
    }, 'image/jpeg', 0.72);
  }

  private clearStillTimer(): void {
    if (this.stillTimer) {
      clearInterval(this.stillTimer);
      this.stillTimer = null;
    }
  }

  protected onCameraChange(event: Event): void {
    const select = event.target;
    if (select instanceof HTMLSelectElement && select.value) {
      void this.recording.switchTo(select.value);
    }
  }
}
