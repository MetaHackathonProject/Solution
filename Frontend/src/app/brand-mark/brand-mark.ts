import { Component } from '@angular/core';

@Component({
  selector: 'app-brand-mark',
  template: `
    <svg viewBox="0 0 32 32" fill="none" aria-hidden="true">
      <rect width="32" height="32" rx="8" fill="#1B64F2"/>
      <path
        fill="#fff"
        fill-rule="evenodd"
        d="M10.8 6.5h10a2.6 2.6 0 0 1 2.6 2.6v9.2a2.6 2.6 0 0 1-2.6 2.6h-6.9l-5.7 4.9V9.1a2.6 2.6 0 0 1 2.6-2.6Zm4.4 3.7c.6 2.6 1.6 3.6 4.2 4.2-2.6.6-3.6 1.6-4.2 4.2-.6-2.6-1.6-3.6-4.2-4.2 2.6-.6 3.6-1.6 4.2-4.2Zm5.2-1.9c.27 1.17.73 1.63 1.9 1.9-1.17.27-1.63.73-1.9 1.9-.27-1.17-.73-1.63-1.9-1.9 1.17-.27 1.63-.73 1.9-1.9Z"
      />
    </svg>
  `,
  styles: `
    :host {
      display: block;
      width: 2.15rem;
      height: 2.15rem;
      flex-shrink: 0;
    }

    svg {
      display: block;
      width: 100%;
      height: 100%;
    }

    @media (forced-colors: active) {
      svg rect:first-child {
        stroke: CanvasText;
        stroke-width: 1.5;
      }
    }
  `,
})
export class BrandMark {}
