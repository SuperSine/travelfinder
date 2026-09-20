import { Component } from '@angular/core';
import { CommonModule } from '@angular/common';

@Component({
  selector: 'ui-panel',
  standalone: true,
  imports: [CommonModule],
  template: `
    <section class="rounded-lg border border-slate-200 bg-white p-4 shadow-sm">
      <ng-content></ng-content>
    </section>
  `
})
export class UiPanelComponent {}
