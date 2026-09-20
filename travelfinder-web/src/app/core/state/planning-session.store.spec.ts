import { PlanningSessionStore } from './planning-session.store';
import { Place, PlanSpec } from '../domain/models';

describe('PlanningSessionStore', () => {
  const spec: PlanSpec = {
    language: 'en-us',
    areaLabel: 'Singapore',
    radiusMeters: 5000,
    categories: ['park'],
    pointOfInterests: [],
    budgetLevel: 'moderate',
    dayCount: 1,
    notes: ''
  };

  const place = (id: string): Place => ({
    id,
    source: 'google',
    sourceId: id.split(':')[1],
    name: id,
    categories: ['park'],
    location: { latitude: 1.3, longitude: 103.8 },
    score: 0.5
  });

  it('merges plan_delta by dayIndex and stopIndex', () => {
    const store = new PlanningSessionStore();
    store.start({ messages: [], latitude: 1, longitude: 2, requestId: 'r1' });
    store.apply({ type: 'plan_delta', stop: { dayIndex: 0, stopIndex: 0, placeId: 'google:1', name: 'A', reason: 'old' } });
    store.apply({ type: 'plan_delta', stop: { dayIndex: 0, stopIndex: 0, placeId: 'google:1', name: 'A', reason: 'new' } });
    store.apply({ type: 'plan_delta', stop: { dayIndex: 0, stopIndex: 1, placeId: 'google:2', name: 'B' } });

    expect(store.snapshot().stops.length).toBe(2);
    expect(store.snapshot().stops[0].reason).toBe('new');
  });

  it('does not wipe places on a second places event', () => {
    const store = new PlanningSessionStore();
    store.start({ messages: [], latitude: 1, longitude: 2, requestId: 'r1' });
    store.apply({ type: 'places', places: [place('google:1')] });
    store.apply({ type: 'places', places: [place('google:2')] });

    expect(store.snapshot().places.map(p => p.id)).toEqual(['google:1', 'google:2']);
  });

  it('drops events from a superseded run when run() is called again', async () => {
    const store = new PlanningSessionStore();
    const park = place('google:park');
    const firstStream = async function* () {
      await new Promise(resolve => setTimeout(resolve, 20));
      yield { type: 'places' as const, places: [place('google:stale')] };
    };
    const secondStream = async function* () {
      yield { type: 'places' as const, places: [park] };
      yield { type: 'done' as const };
    };

    const firstRun = store.run(
      { stream: () => firstStream() } as never,
      { messages: [], latitude: 1, longitude: 2, requestId: 'r1' }
    );
    await store.run(
      { stream: () => secondStream() } as never,
      { messages: [], latitude: 1, longitude: 2, requestId: 'r2' }
    );
    await firstRun;

    expect(store.snapshot().places.map(p => p.id)).toEqual(['google:park']);
  });

  it('error keeps places and only a new start clears them', () => {
    const store = new PlanningSessionStore();
    store.start({ messages: [], latitude: 1, longitude: 2, requestId: 'r1' });
    store.apply({ type: 'plan_spec', spec });
    store.apply({ type: 'places', places: [place('google:1')] });
    store.apply({
      type: 'error',
      error: { code: 'timeout', message: 'timed out', requestId: 'r1', retryable: true }
    });

    expect(store.snapshot().phase).toBe('error');
    expect(store.snapshot().places.length).toBe(1);

    store.start({ messages: [], latitude: 1, longitude: 2, requestId: 'r2' });
    expect(store.snapshot().places.length).toBe(0);
    expect(store.snapshot().phase).toBe('planning');
  });
});
