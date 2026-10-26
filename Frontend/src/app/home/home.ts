import { Component } from '@angular/core';

@Component({
  selector: 'app-home',
  template: '<main class="page"></main>',
  styles: `
    :host {
      display: block;
      color: #111;
      background: #fff;
    }

    .page {
      min-height: calc(100vh - 4.25rem);
    }
  `,
})
export class Home {}
