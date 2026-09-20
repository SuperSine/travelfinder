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
});
