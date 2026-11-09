import { describe, expect, it } from 'vitest';
import { createPlanningSessionController } from './planning-session';
import type { Place, PlanSpec } from '../domain/models';

const spec: PlanSpec = {
  language: 'en-us',
  areaLabel: 'Singapore',
  radiusMeters: 5000,
  categories: ['park'],
  pointOfInterests: [],
  budgetLevel: 'moderate',
  dayCount: 1,
  notes: '',
};

const place = (id: string): Place => ({
  id,
  source: 'google',
  sourceId: id.split(':')[1] ?? id,
  name: id,
  categories: ['park'],
  location: { latitude: 1.3, longitude: 103.8 },
  score: 0.5,
});

describe('planning session reducer', () => {
  it('merges plan_delta by dayIndex and stopIndex', () => {
    const ctrl = createPlanningSessionController();
    ctrl.start({ messages: [], latitude: 1, longitude: 2, requestId: 'r1' });
    ctrl.apply({ type: 'plan_delta', stop: { dayIndex: 0, stopIndex: 0, placeId: 'google:1', name: 'A', reason: 'old' } });
    ctrl.apply({ type: 'plan_delta', stop: { dayIndex: 0, stopIndex: 0, placeId: 'google:1', name: 'A', reason: 'new' } });
    ctrl.apply({ type: 'plan_delta', stop: { dayIndex: 0, stopIndex: 1, placeId: 'google:2', name: 'B' } });
    expect(ctrl.snapshot().stops).toHaveLength(2);
    expect(ctrl.snapshot().stops[0].reason).toBe('new');
  });

  it('does not wipe places on a second places event', () => {
    const ctrl = createPlanningSessionController();
    ctrl.start({ messages: [], latitude: 1, longitude: 2, requestId: 'r1' });
    ctrl.apply({ type: 'places', places: [place('google:1')] });
    ctrl.apply({ type: 'places', places: [place('google:2')] });

    expect(ctrl.snapshot().places.map(p => p.id)).toEqual(['google:1', 'google:2']);
  });

  it('drops events from a superseded run when run() is called again', async () => {
    const ctrl = createPlanningSessionController();
    const park = place('google:park');
    const firstStream = async function* () {
      await new Promise(resolve => setTimeout(resolve, 20));
      yield { type: 'places' as const, places: [place('google:stale')] };
    };
    const secondStream = async function* () {
      yield { type: 'places' as const, places: [park] };
      yield { type: 'done' as const };
    };

    const firstRun = ctrl.run(
      { stream: () => firstStream() } as never,
      { messages: [], latitude: 1, longitude: 2, requestId: 'r1' },
    );
    await ctrl.run(
      { stream: () => secondStream() } as never,
      { messages: [], latitude: 1, longitude: 2, requestId: 'r2' },
    );
    await firstRun;

    expect(ctrl.snapshot().places.map(p => p.id)).toEqual(['google:park']);
  });

  it('error keeps places; start clears them', () => {
    const ctrl = createPlanningSessionController();
    ctrl.start({ messages: [], latitude: 1, longitude: 2, requestId: 'r1' });
    ctrl.apply({ type: 'plan_spec', spec });
    ctrl.apply({
      type: 'places',
      places: [{
        id: 'google:1', source: 'google', sourceId: '1', name: 'A',
        categories: [], location: { latitude: 1, longitude: 2 }, score: 0.5,
      }],
    });
    ctrl.apply({
      type: 'error',
      error: { code: 'timeout', message: 'timed out', requestId: 'r1', retryable: true },
    });
    expect(ctrl.snapshot().phase).toBe('error');
    expect(ctrl.snapshot().places).toHaveLength(1);
    ctrl.start({ messages: [], latitude: 1, longitude: 2, requestId: 'r2' });
    expect(ctrl.snapshot().places).toHaveLength(0);
    expect(ctrl.snapshot().phase).toBe('planning');
  });
});
