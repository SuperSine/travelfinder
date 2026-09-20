import { Component, EventEmitter, Input, Output } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ChatMessageDto } from '../../core/domain/models';
import { SessionPhase } from '../../core/state/planning-session.store';
import { UiButtonComponent } from '../../ui/ui-button.component';

@Component({
  selector: 'app-chat-panel',
  standalone: true,
  imports: [CommonModule, FormsModule, UiButtonComponent],
  template: `
    <section class="flex flex-col gap-3 border-b border-slate-200 p-4">
      <p class="text-xs font-medium uppercase tracking-wide text-slate-500">{{ phase }}</p>

      <div class="max-h-48 space-y-2 overflow-y-auto text-sm">
        @for (message of messages; track $index) {
          <p [class.text-right]="message.role === 'user'" class="text-slate-700">
            <span class="font-medium">{{ message.role === 'user' ? 'You' : 'Assistant' }}:</span>
            {{ message.content }}
          </p>
        }
      </div>

      @if (clarification) {
        <p class="rounded-md bg-amber-50 p-2 text-sm text-amber-900">{{ clarification }}</p>
      }

      @if (errorMessage) {
        <p class="rounded-md bg-red-50 p-2 text-sm text-red-900">{{ errorMessage }}</p>
      }

      <div class="flex gap-2">
        <textarea
          [(ngModel)]="draft"
          rows="3"
          placeholder="Reply or refine your trip…"
          class="min-h-[4rem] flex-1 rounded-md border border-slate-300 p-2 text-sm focus:border-slate-500 focus:outline-none"></textarea>
        <ui-button role="send" [disabled]="!draft.trim()" (click)="submit()">Send</ui-button>
      </div>
    </section>
  `,
})
export class ChatPanelComponent {
  @Input() messages: ChatMessageDto[] = [];
  @Input() clarification?: string;
  @Input() errorMessage?: string;
  @Input() phase: SessionPhase = 'idle';
  @Output() send = new EventEmitter<string>();

  draft = '';

  submit(): void {
    const text = this.draft.trim();
    if (!text) {
      return;
    }
    this.send.emit(text);
    this.draft = '';
  }
}
