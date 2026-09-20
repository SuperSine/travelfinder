import { Component, Input } from '@angular/core';
import { CommonModule } from '@angular/common';

@Component({
  selector: 'ui-button',
  standalone: true,
  imports: [CommonModule],
  template: `
    <button
      [attr.data-role]="role"
      [disabled]="disabled"
      class="inline-flex items-center justify-center rounded-md bg-slate-900 px-4 py-2 text-sm font-medium text-white hover:bg-slate-700 disabled:cursor-not-allowed disabled:opacity-50">
      <ng-content></ng-content>
    </button>
  `
})
export class UiButtonComponent {
  @Input() disabled = false;
  @Input() role = '';
}
