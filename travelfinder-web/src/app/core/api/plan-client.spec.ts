import { PlanClient } from './plan-client';
import { environment } from '../../../environments/environment';

describe('PlanClient', () => {
  it('posts only to /plan/stream and yields parsed events', async () => {
    const body =
      'event: clarification\ndata: {"message":"How many days?"}\n\n' +
      'event: done\ndata: {}\n\n';
    spyOn(window, 'fetch').and.resolveTo(new Response(body, {
      headers: { 'Content-Type': 'text/event-stream' }
    }));

    const client = new PlanClient();
    const events = [];
    for await (const event of client.stream({
      messages: [{ role: 'user', content: 'hi' }],
      latitude: 1.35,
      longitude: 103.82,
      requestId: 'r1'
    })) {
      events.push(event);
    }

    expect(events.map(e => e.type)).toEqual(['clarification', 'done']);
    const [url, init] = (window.fetch as jasmine.Spy).calls.mostRecent().args;
    expect(url).toBe(`${environment.API_URL}/plan/stream`);
    expect(init.method).toBe('POST');
    expect(JSON.parse(init.body).systemId).toBeUndefined();
  });

  it('yields error on HTTP 400 with JSON body', async () => {
    spyOn(window, 'fetch').and.resolveTo(
      new Response('{"title":"Bad Request"}', {
        status: 400,
        headers: { 'Content-Type': 'application/json' },
      })
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

    expect(events).toEqual([
      {
        type: 'error',
        error: {
          code: 'internal',
          message: '{"title":"Bad Request"}',
          requestId: 'r1',
          retryable: true,
        },
      },
    ]);
  });

  it('yields error when 200 response is not event-stream', async () => {
    spyOn(window, 'fetch').and.resolveTo(
      new Response('{"message":"oops"}', {
        status: 200,
        headers: { 'Content-Type': 'application/json' },
      })
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

    expect(events[0]).toEqual({
      type: 'error',
      error: {
        code: 'internal',
        message: '{"message":"oops"}',
        requestId: 'r1',
        retryable: true,
      },
    });
  });
});
