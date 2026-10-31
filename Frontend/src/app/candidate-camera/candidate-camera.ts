import { Component, inject } from '@angular/core';
import { CandidateRecordingService } from '../candidate-recording.service';

@Component({
  selector: 'app-candidate-camera',
  templateUrl: './candidate-camera.html',
  styleUrl: './candidate-camera.scss',
})
export class CandidateCamera {
  protected readonly recording = inject(CandidateRecordingService);

  protected playPreview(event: Event): void {
    const video = event.target;
    if (video instanceof HTMLVideoElement) {
      void video.play().catch(() => undefined);
    }
  }
}
