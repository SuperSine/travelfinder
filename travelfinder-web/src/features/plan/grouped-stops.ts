import type { ItineraryStop } from '../../core/domain/models';

export interface StopDayGroup {
  dayIndex: number;
  stops: ItineraryStop[];
}

export function groupedStops(stops: ItineraryStop[]): StopDayGroup[] {
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
