import { afterEach, describe, expect, it, vi } from 'vitest';
import { PlanClient } from './plan-client';
import { environment } from '../../config/environment';

describe('PlanClient', () => {
  afterEach(() => vi.restoreAllMocks());

  it('posts only to /plan/stream and yields parsed events', async () => {
    const body =
      'event: clarification\ndata: {"message":"How many days?"}\n\n' +
      'event: done\ndata: {}\n\n';
    vi.spyOn(globalThis, 'fetch').mockResolvedValue(
      new Response(body, { headers: { 'Content-Type': 'text/event-stream' } }),
    );

    const client = new PlanClient();
    const events = [];
    for await (const event of client.stream({
      messages: [{ role: 'user', content: 'hi' }],
      latitude: 1.35,
      longitude: 103.82,
      requestId: 'r1',
    })) {
      events.push(event);
    }

    expect(events.map(e => e.type)).toEqual(['clarification', 'done']);
    const [url, init] = vi.mocked(fetch).mock.calls.at(-1)!;
    expect(url).toBe(`${environment.API_URL}/plan/stream`);
    expect(init?.method).toBe('POST');
    expect(JSON.parse(init?.body as string).systemId).toBeUndefined();
  });

  it('yields error on HTTP 400 with JSON body', async () => {
    vi.spyOn(globalThis, 'fetch').mockResolvedValue(
      new Response('{"title":"Bad Request"}', {
        status: 400,
        headers: { 'Content-Type': 'application/json' },
      }),
    );
    const client = new PlanClient();
    const events = [];
    for await (const event of client.stream({
      messages: [{ role: 'user', content: 'hi' }],
      latitude: 1.35,
      longitude: 103.82,
      requestId: 'r1',
    })) {
      events.push(event);
    }
    expect(events[0]?.type).toBe('error');
  });
});
