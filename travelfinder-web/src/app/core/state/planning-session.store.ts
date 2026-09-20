import { Injectable } from '@angular/core';
import { BehaviorSubject } from 'rxjs';
import { PlanClient } from '../api/plan-client';
import {
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

@Injectable({ providedIn: 'root' })
export class PlanningSessionStore {
  private state: PlanningSessionState = initialState();
  private abortController?: AbortController;
  private generation = 0;

  private readonly stateSubject = new BehaviorSubject<PlanningSessionSnapshot>(
    this.snapshot()
  );

  readonly state$ = this.stateSubject.asObservable();
  readonly phase$ = new BehaviorSubject<SessionPhase>(this.state.phase);

  snapshot(): PlanningSessionSnapshot {
    return {
      phase: this.state.phase,
      request: this.state.request ? { ...this.state.request } : undefined,
      spec: this.state.spec ? { ...this.state.spec } : undefined,
      clarification: this.state.clarification,
      error: this.state.error ? { ...this.state.error } : undefined,
      places: [...this.state.places],
      stops: this.orderedStops(),
    };
  }

  orderedStops(): ItineraryStop[] {
    return [...this.state.stopsByKey.values()].sort((a, b) =>
      a.dayIndex === b.dayIndex ? a.stopIndex - b.stopIndex : a.dayIndex - b.dayIndex
    );
  }

  abort(): void {
    this.abortController?.abort();
    this.abortController = undefined;
  }

  start(request: PlanRequest): void {
    this.abort();
    this.generation++;
    this.state = {
      phase: 'planning',
      request: { ...request },
      places: [],
      stopsByKey: new Map(),
    };
    this.emit();
  }

  reset(): void {
    this.abort();
    this.generation++;
    this.state = initialState();
    this.emit();
  }

  apply(event: PlanEvent): void {
    switch (event.type) {
      case 'clarification':
        this.state.phase = 'clarifying';
        this.state.clarification = event.message;
        break;
      case 'plan_spec':
        this.state.phase = 'retrieving';
        this.state.spec = event.spec;
        break;
      case 'places':
        for (const place of event.places) {
          const existingIndex = this.state.places.findIndex(p => p.id === place.id);
          if (existingIndex >= 0) {
            this.state.places[existingIndex] = place;
          } else {
            this.state.places.push(place);
          }
        }
        break;
      case 'plan_delta':
        this.state.phase = 'rendering';
        this.state.stopsByKey.set(
          `${event.stop.dayIndex}:${event.stop.stopIndex}`,
          event.stop
        );
        break;
      case 'error':
        this.state.phase = 'error';
        this.state.error = event.error;
        break;
      case 'done':
        if (this.state.phase !== 'error' && this.state.phase !== 'clarifying') {
          this.state.phase = 'done';
        }
        break;
    }
    this.emit();
  }

  async run(client: PlanClient, request: PlanRequest, signal?: AbortSignal): Promise<void> {
    this.start(request);
    const runGeneration = this.generation;
    const controller = new AbortController();
    this.abortController = controller;

    const onExternalAbort = (): void => controller.abort();
    signal?.addEventListener('abort', onExternalAbort);

    try {
      for await (const event of client.stream(request, controller.signal)) {
        if (runGeneration !== this.generation) {
          return;
        }
        this.apply(event);
      }
    } catch (error) {
      if (this.isAbortError(error)) {
        return;
      }
      throw error;
    } finally {
      signal?.removeEventListener('abort', onExternalAbort);
      if (this.abortController === controller) {
        this.abortController = undefined;
      }
    }
  }

  private isAbortError(error: unknown): boolean {
    return error instanceof DOMException && error.name === 'AbortError';
  }

  private emit(): void {
    this.phase$.next(this.state.phase);
    this.stateSubject.next(this.snapshot());
  }
}
