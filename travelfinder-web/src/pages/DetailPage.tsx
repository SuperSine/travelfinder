import { Link } from 'react-router-dom';
import { Heading, Stack, Text } from '@astryxdesign/core';
import { usePlanningSession } from '../core/state/planning-session';
import { groupedStops } from '../features/plan/grouped-stops';
import './detail-page.css';

export function DetailPage() {
  const { snapshotData: state } = usePlanningSession();
  const groups = groupedStops(state.stops);

  return (
    <Stack gap={6} className="detail-page">
      <Link to="/plan">← Back to plan</Link>

      {state.spec ? (
        <header>
          <Stack gap={1}>
            <Heading level={2}>{state.spec.areaLabel}</Heading>
            <Text type="supporting" color="secondary">
              {state.spec.dayCount} day{state.spec.dayCount === 1 ? '' : 's'} ·{' '}
              {state.spec.budgetLevel} budget
            </Text>
          </Stack>
        </header>
      ) : null}

      <Stack gap={4}>
        {groups.map(group => (
          <Stack key={group.dayIndex} gap={2}>
            <Heading level={4}>Day {group.dayIndex + 1}</Heading>
            <Stack gap={2} as="ul" className="detail-stops">
              {group.stops.map(stop => (
                <Stack
                  key={`${group.dayIndex}-${stop.stopIndex}-${stop.placeId}`}
                  gap={1}
                  className="detail-stop"
                  as="li"
                >
                  <Text type="label">{stop.name}</Text>
                  {stop.reason ? <Text type="supporting">{stop.reason}</Text> : null}
                  {stop.durationMinutes != null ? (
                    <Text type="supporting" color="secondary" size="sm">
                      {stop.durationMinutes} min
                    </Text>
                  ) : null}
                </Stack>
              ))}
            </Stack>
          </Stack>
        ))}
      </Stack>
    </Stack>
  );
}
