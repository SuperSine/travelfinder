import { describe, expect, it } from 'vitest';
import { groupedStops } from '../features/plan/grouped-stops';
import type { ItineraryStop } from '../core/domain/models';

describe('groupedStops', () => {
  it('sorts days and stop indices', () => {
    const stops: ItineraryStop[] = [
      { dayIndex: 1, stopIndex: 1, placeId: 'b', name: 'B' },
      { dayIndex: 0, stopIndex: 1, placeId: 'a2', name: 'A2' },
      { dayIndex: 1, stopIndex: 0, placeId: 'b0', name: 'B0' },
      { dayIndex: 0, stopIndex: 0, placeId: 'a1', name: 'A1' },
    ];

    const groups = groupedStops(stops);
    expect(groups.map(g => g.dayIndex)).toEqual([0, 1]);
    expect(groups[0].stops.map(s => s.placeId)).toEqual(['a1', 'a2']);
    expect(groups[1].stops.map(s => s.placeId)).toEqual(['b0', 'b']);
  });
});
