import { Component, EventEmitter, Input, Output } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ItineraryStop } from '../../core/domain/models';

@Component({
  selector: 'app-itinerary-cards',
  standalone: true,
  imports: [CommonModule],
  template: `
    <section class="flex-1 space-y-4 overflow-y-auto p-4">
      @for (group of groupedStops(); track group.dayIndex) {
        <div>
          <h2 class="mb-2 text-sm font-semibold text-slate-700">Day {{ group.dayIndex + 1 }}</h2>
          <div class="space-y-2">
            @for (stop of group.stops; track stop.placeId) {
              <button
                type="button"
                class="w-full rounded-md border border-slate-200 p-3 text-left hover:border-slate-400"
                (click)="select.emit(stop.placeId)">
                <p class="font-medium text-slate-900">{{ stop.name }}</p>
                @if (stop.reason) {
                  <p class="mt-1 text-sm text-slate-600">{{ stop.reason }}</p>
                }
                @if (stop.durationMinutes) {
                  <p class="mt-1 text-xs text-slate-500">{{ stop.durationMinutes }} min</p>
                }
              </button>
            }
          </div>
        </div>
      }
    </section>
  `,
})
export class ItineraryCardsComponent {
  @Input() stops: ItineraryStop[] = [];
  @Output() select = new EventEmitter<string>();

  groupedStops(): { dayIndex: number; stops: ItineraryStop[] }[] {
    const byDay = new Map<number, ItineraryStop[]>();
    for (const stop of this.stops) {
      const dayStops = byDay.get(stop.dayIndex) ?? [];
      dayStops.push(stop);
      byDay.set(stop.dayIndex, dayStops);
    }

    return [...byDay.entries()]
      .sort(([a], [b]) => a - b)
      .map(([dayIndex, dayStops]) => ({
        dayIndex,
        stops: [...dayStops].sort((a, b) => a.stopIndex - b.stopIndex),
      }));
  }
}
