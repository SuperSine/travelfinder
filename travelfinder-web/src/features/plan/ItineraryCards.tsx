import { ClickableCard, Heading, Stack, Text } from '@astryxdesign/core';
import type { ItineraryStop } from '../../core/domain/models';
import { groupedStops } from './grouped-stops';
import './itinerary-cards.css';

export interface ItineraryCardsProps {
  stops: ItineraryStop[];
  onSelect: (placeId: string) => void;
}

export function ItineraryCards({ stops, onSelect }: ItineraryCardsProps) {
  const groups = groupedStops(stops);

  return (
    <Stack gap={4} className="itinerary-cards">
      {groups.map(group => (
        <Stack key={group.dayIndex} gap={2}>
          <Heading level={5}>Day {group.dayIndex + 1}</Heading>
          <Stack gap={2}>
            {group.stops.map(stop => (
              <ClickableCard
                key={`${group.dayIndex}-${stop.stopIndex}-${stop.placeId}`}
                label={stop.name}
                onClick={() => onSelect(stop.placeId)}
              >
                <Stack gap={1}>
                  <Text type="label">{stop.name}</Text>
                  {stop.reason ? <Text type="supporting">{stop.reason}</Text> : null}
                  {stop.durationMinutes != null ? (
                    <Text type="supporting" color="secondary" size="sm">
                      {stop.durationMinutes} min
                    </Text>
                  ) : null}
                </Stack>
              </ClickableCard>
            ))}
          </Stack>
        </Stack>
      ))}
    </Stack>
  );
}
