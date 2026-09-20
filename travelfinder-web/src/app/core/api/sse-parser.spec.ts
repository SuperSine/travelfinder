import { parseSseFrames } from './sse-parser';

describe('parseSseFrames', () => {
  it('parses named events into the union', () => {
    const frames = parseSseFrames(
      'event: plan_spec\ndata: {"language":"en-us","areaLabel":"Singapore","radiusMeters":5000,"categories":["park"],"pointOfInterests":[],"budgetLevel":"moderate","dayCount":1,"notes":""}\n\n' +
      'event: places\ndata: {"places":[]}\n\n'
    );

    expect(frames[0]).toEqual(jasmine.objectContaining({ type: 'plan_spec' }));
    expect(frames[1]).toEqual(jasmine.objectContaining({ type: 'places', places: [] }));
  });

  it('turns invalid json into error', () => {
    const frames = parseSseFrames('event: places\ndata: {not-json\n\n');
    expect(frames[0].type).toBe('error');
    expect((frames[0] as { type: 'error'; error: { code: string } }).error.code).toBe('internal');
  });
});
