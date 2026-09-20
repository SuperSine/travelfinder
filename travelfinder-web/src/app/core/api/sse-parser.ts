import {
  ItineraryStop,
  PlanError,
  PlanEvent,
  PlanSpec,
  Place,
} from '../domain/models';

interface SseCarry {
  event?: string;
  data: string[];
}

interface ErrorPayload {
  code: PlanError['code'];
  message: string;
  requestId: string;
  retryable: boolean;
}

function internalError(message: string): PlanEvent {
  return {
    type: 'error',
    error: {
      code: 'internal',
      message,
      requestId: '',
      retryable: false,
    },
  };
}

function parseEventPayload(eventName: string, data: string): PlanEvent {
  let parsed: unknown;
  try {
    parsed = JSON.parse(data);
  } catch {
    return internalError('Invalid JSON');
  }

  switch (eventName) {
    case 'clarification': {
      const payload = parsed as { message?: string };
      return { type: 'clarification', message: payload.message ?? '' };
    }
    case 'plan_spec':
      return { type: 'plan_spec', spec: parsed as PlanSpec };
    case 'places': {
      const payload = parsed as { places?: Place[] };
      return { type: 'places', places: payload.places ?? [] };
    }
    case 'plan_delta':
      return { type: 'plan_delta', stop: parsed as ItineraryStop };
    case 'error':
      return { type: 'error', error: parsed as ErrorPayload };
    case 'done': {
      const payload = parsed as { providerUsed?: string };
      return { type: 'done', providerUsed: payload.providerUsed };
    }
    default:
      return internalError('Unknown event');
  }
}

export function parseSseFrames(
  chunk: string,
  carry: SseCarry = { data: [] },
): PlanEvent[] {
  const events: PlanEvent[] = [];
  const lines = chunk.split('\n');

  for (const line of lines) {
    if (line.startsWith('event:')) {
      carry.event = line.slice('event:'.length).trim();
      continue;
    }

    if (line.startsWith('data:')) {
      carry.data.push(line.slice('data:'.length).trim());
      continue;
    }

    if (line.trim() === '' && (carry.event || carry.data.length > 0)) {
      const data = carry.data.join('\n');
      const eventName = carry.event ?? '';
      events.push(parseEventPayload(eventName, data));
      carry.event = undefined;
      carry.data = [];
    }
  }

  return events;
}
