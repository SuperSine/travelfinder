import { mergePlaceGraphics } from './place-graphics';
import { Place } from '../../core/domain/models';

const p = (id: string): Place => ({
  id, source: 'google', sourceId: id, name: id, categories: [], location: { latitude: 1, longitude: 2 }, score: 0
});

describe('mergePlaceGraphics', () => {
  it('keeps existing ids and adds new ones', () => {
    const result = mergePlaceGraphics(['google:1'], [p('google:1'), p('google:2')]);
    expect(result.keep).toEqual(['google:1']);
    expect(result.add.map(x => x.id)).toEqual(['google:2']);
  });
});
