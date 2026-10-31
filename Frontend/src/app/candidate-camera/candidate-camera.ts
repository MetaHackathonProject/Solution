import { Component, ElementRef, effect, inject, viewChild } from '@angular/core';
import { CandidateRecordingService } from '../candidate-recording.service';

@Component({
  selector: 'app-candidate-camera',
  templateUrl: './candidate-camera.html',
  styleUrl: './candidate-camera.scss',
})
export class CandidateCamera {
  protected readonly recording = inject(CandidateRecordingService);
  private readonly preview = viewChild<ElementRef<HTMLVideoElement>>('preview');

  constructor() {
    effect(() => {
      const video = this.preview()?.nativeElement;
      if (video) {
        video.srcObject = this.recording.previewStream();
      }
    });
  }
}
