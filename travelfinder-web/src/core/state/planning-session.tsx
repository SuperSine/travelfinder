import {
  createContext,
  useCallback,
  useContext,
  useMemo,
  useRef,
  useState,
  type ReactNode,
} from 'react';
import { PlanClient } from '../api/plan-client';
import type {
  ItineraryStop,
  Place,
  PlanError,
  PlanEvent,
  PlanRequest,
  PlanSpec,
} from '../domain/models';

export type SessionPhase =
  | 'idle'
  | 'planning'
  | 'clarifying'
  | 'retrieving'
  | 'rendering'
  | 'done'
  | 'error';

export interface PlanningSessionSnapshot {
  phase: SessionPhase;
  request?: PlanRequest;
  spec?: PlanSpec;
  clarification?: string;
  error?: PlanError;
  places: Place[];
  stops: ItineraryStop[];
}

interface PlanningSessionState {
  phase: SessionPhase;
  request?: PlanRequest;
  spec?: PlanSpec;
  clarification?: string;
  error?: PlanError;
  places: Place[];
  stopsByKey: Map<string, ItineraryStop>;
}

const initialState = (): PlanningSessionState => ({
  phase: 'idle',
  places: [],
  stopsByKey: new Map(),
});

function orderedStops(stopsByKey: Map<string, ItineraryStop>): ItineraryStop[] {
  return [...stopsByKey.values()].sort((a, b) =>
    a.dayIndex === b.dayIndex ? a.stopIndex - b.stopIndex : a.dayIndex - b.dayIndex,
  );
}

function snapshotFrom(state: PlanningSessionState): PlanningSessionSnapshot {
  return {
    phase: state.phase,
    request: state.request ? { ...state.request } : undefined,
    spec: state.spec ? { ...state.spec } : undefined,
    clarification: state.clarification,
    error: state.error ? { ...state.error } : undefined,
    places: [...state.places],
    stops: orderedStops(state.stopsByKey),
  };
}

export interface PlanningSessionController {
  snapshot: () => PlanningSessionSnapshot;
  abort: () => void;
  start: (request: PlanRequest) => void;
  reset: () => void;
  apply: (event: PlanEvent) => void;
  run: (client: PlanClient, request: PlanRequest, signal?: AbortSignal) => Promise<void>;
}

export function createPlanningSessionController(): PlanningSessionController {
  let state = initialState();
  let abortController: AbortController | undefined;
  let generation = 0;

  const applyToState = (event: PlanEvent): void => {
    switch (event.type) {
      case 'clarification':
        state.phase = 'clarifying';
        state.clarification = event.message;
        break;
      case 'plan_spec':
        state.phase = 'retrieving';
        state.spec = event.spec;
        break;
      case 'places':
        for (const place of event.places) {
          const existingIndex = state.places.findIndex(p => p.id === place.id);
          if (existingIndex >= 0) {
            state.places[existingIndex] = place;
          } else {
            state.places.push(place);
          }
        }
        break;
      case 'plan_delta':
        state.phase = 'rendering';
        state.stopsByKey.set(
          `${event.stop.dayIndex}:${event.stop.stopIndex}`,
          event.stop,
        );
        break;
      case 'error':
        state.phase = 'error';
        state.error = event.error;
        break;
      case 'done':
        if (state.phase !== 'error' && state.phase !== 'clarifying') {
          state.phase = 'done';
        }
        break;
    }
  };

  return {
    snapshot: () => snapshotFrom(state),

    abort: () => {
      abortController?.abort();
      abortController = undefined;
    },

    start: (request: PlanRequest) => {
      abortController?.abort();
      abortController = undefined;
      generation++;
      state = {
        phase: 'planning',
        request: { ...request },
        places: [],
        stopsByKey: new Map(),
      };
    },

    reset: () => {
      abortController?.abort();
      abortController = undefined;
      generation++;
      state = initialState();
    },

    apply: (event: PlanEvent) => {
      applyToState(event);
    },

    run: async (client: PlanClient, request: PlanRequest, signal?: AbortSignal) => {
      abortController?.abort();
      generation++;
      state = {
        phase: 'planning',
        request: { ...request },
        places: [],
        stopsByKey: new Map(),
      };
      const runGeneration = generation;
      const controller = new AbortController();
      abortController = controller;

      const onExternalAbort = (): void => controller.abort();
      signal?.addEventListener('abort', onExternalAbort);

      try {
        for await (const event of client.stream(request, controller.signal)) {
          if (runGeneration !== generation) {
            return;
          }
          applyToState(event);
        }
      } catch (error) {
        if (error instanceof DOMException && error.name === 'AbortError') {
          return;
        }
        throw error;
      } finally {
        signal?.removeEventListener('abort', onExternalAbort);
        if (abortController === controller) {
          abortController = undefined;
        }
      }
    },
  };
}

type PlanningSessionContextValue = PlanningSessionController & {
  readonly snapshotData: PlanningSessionSnapshot;
};

const PlanningSessionContext = createContext<PlanningSessionContextValue | null>(null);

export function PlanningSessionProvider({ children }: { children: ReactNode }) {
  const controllerRef = useRef<PlanningSessionController | null>(null);
  if (!controllerRef.current) {
    controllerRef.current = createPlanningSessionController();
  }
  const controller = controllerRef.current;

  const [snapshotData, setSnapshotData] = useState<PlanningSessionSnapshot>(() =>
    controller.snapshot(),
  );

  const bump = useCallback(() => {
    setSnapshotData(controller.snapshot());
  }, [controller]);

  const value = useMemo<PlanningSessionContextValue>(() => {
    const wrap =
      <Args extends unknown[]>(fn: (...args: Args) => void) =>
      (...args: Args) => {
        fn(...args);
        bump();
      };

    return {
      ...controller,
      snapshotData,
      snapshot: () => controller.snapshot(),
      start: wrap(controller.start),
      reset: wrap(controller.reset),
      apply: wrap(controller.apply),
      abort: () => {
        controller.abort();
        bump();
      },
      run: async (client: PlanClient, request: PlanRequest, signal?: AbortSignal) => {
        await controller.run(client, request, signal);
        bump();
      },
    };
  }, [controller, snapshotData, bump]);

  return (
    <PlanningSessionContext.Provider value={value}>
      {children}
    </PlanningSessionContext.Provider>
  );
}

export function usePlanningSession(): PlanningSessionContextValue {
  const ctx = useContext(PlanningSessionContext);
  if (!ctx) {
    throw new Error('usePlanningSession must be used within PlanningSessionProvider');
  }
  return ctx;
}
