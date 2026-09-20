import { Place } from '../../core/domain/models';

export function mergePlaceGraphics(
  existingIds: string[],
  incoming: Place[]
): { add: Place[]; keep: string[] } {
  const incomingIds = new Set(incoming.map(place => place.id));
  const keep = existingIds.filter(id => incomingIds.has(id));
  const existingSet = new Set(existingIds);
  const add = incoming.filter(place => !existingSet.has(place.id));
  return { add, keep };
}
