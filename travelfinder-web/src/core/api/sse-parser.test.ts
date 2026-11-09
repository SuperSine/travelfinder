import { describe, expect, it } from 'vitest';
import { parseSseFrames } from './sse-parser';

describe('parseSseFrames', () => {
  it('parses named events into the union', () => {
    const frames = parseSseFrames(
      'event: plan_spec\ndata: {"language":"en-us","areaLabel":"Singapore","radiusMeters":5000,"categories":["park"],"pointOfInterests":[],"budgetLevel":"moderate","dayCount":1,"notes":""}\n\n' +
        'event: places\ndata: {"places":[]}\n\n',
    );
    expect(frames[0]).toMatchObject({ type: 'plan_spec' });
    expect(frames[1]).toMatchObject({ type: 'places', places: [] });
  });

  it('parses all six event types', () => {
    const chunk =
      'event: clarification\ndata: {"message":"How many days?"}\n\n' +
      'event: plan_delta\ndata: {"dayIndex":0,"stopIndex":0,"placeId":"g:1","name":"Park"}\n\n' +
      'event: error\ndata: {"code":"timeout","message":"slow","requestId":"r1","retryable":true}\n\n' +
      'event: done\ndata: {"providerUsed":"azure"}\n\n';
    const types = parseSseFrames(chunk).map(e => e.type);
    expect(types).toEqual(['clarification', 'plan_delta', 'error', 'done']);
  });

  it('turns invalid json into error', () => {
    const frames = parseSseFrames('event: places\ndata: {not-json\n\n');
    expect(frames[0].type).toBe('error');
    if (frames[0].type === 'error') {
      expect(frames[0].error.code).toBe('internal');
    }
  });
});
