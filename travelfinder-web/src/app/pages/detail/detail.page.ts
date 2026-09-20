import { Component, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterLink } from '@angular/router';
import { PlanningSessionStore } from '../../core/state/planning-session.store';
import { ItineraryStop } from '../../core/domain/models';

@Component({
  selector: 'sp-detail',
  standalone: true,
  imports: [CommonModule, RouterLink],
  templateUrl: './detail.page.html',
})
export class DetailPage {
  readonly store = inject(PlanningSessionStore);
  readonly state$ = this.store.state$;

  groupedStops(stops: ItineraryStop[]): { dayIndex: number; stops: ItineraryStop[] }[] {
    const byDay = new Map<number, ItineraryStop[]>();
    for (const stop of stops) {
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
