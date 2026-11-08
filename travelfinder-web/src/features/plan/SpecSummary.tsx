import { Stack, Text } from '@astryxdesign/core';
import type { PlanSpec } from '../../core/domain/models';
import './itinerary-cards.css';

export function SpecSummary({ spec }: { spec: PlanSpec }) {
  return (
    <Stack gap={1} className="spec-summary">
      <Text type="label">{spec.areaLabel}</Text>
      <Text type="supporting" color="secondary">
        {spec.dayCount} day{spec.dayCount === 1 ? '' : 's'} · {spec.budgetLevel} budget
      </Text>
    </Stack>
  );
}
