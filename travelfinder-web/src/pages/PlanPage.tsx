import { useCallback, useEffect, useRef, useState } from 'react';
import { Link } from 'react-router-dom';
import { PlanClient } from '../core/api/plan-client';
import type { ChatMessageDto, GeoPoint, PlanRequest } from '../core/domain/models';
import { usePlanningSession } from '../core/state/planning-session';
import { ChatPanel } from '../features/chat/ChatPanel';
import { TravelMap } from '../features/map/TravelMap';
import { ItineraryCards } from '../features/plan/ItineraryCards';
import { SpecSummary } from '../features/plan/SpecSummary';
import './plan-workspace.css';

interface PendingPlan {
  requestId: string;
  prompt: string;
  latitude: number | null;
  longitude: number | null;
}

const planClient = new PlanClient();

function stopIds(stops: { placeId: string }[]): string[] {
  return stops.map(stop => stop.placeId);
}

export function PlanPage() {
  const session = usePlanningSession();
  const state = session.snapshotData;

  const [messages, setMessages] = useState<ChatMessageDto[]>([]);
  const [requestId, setRequestId] = useState('');
  const [latitude, setLatitude] = useState<number | null>(null);
  const [longitude, setLongitude] = useState<number | null>(null);
  const [needsMapPick, setNeedsMapPick] = useState(false);
  const [selectedId, setSelectedId] = useState<string | null>(null);

  const lastClarificationRef = useRef<string | undefined>(undefined);
  const messagesRef = useRef(messages);
  messagesRef.current = messages;

  const hasValidCoordinates = latitude != null && longitude != null;

  const runPlanning = useCallback(async () => {
    if (needsMapPick && !hasValidCoordinates) {
      return;
    }
    const request: PlanRequest = {
      requestId,
      messages: messagesRef.current,
      latitude: latitude as number,
      longitude: longitude as number,
    };
    await session.run(planClient, request);
  }, [hasValidCoordinates, latitude, longitude, needsMapPick, requestId, session]);

  useEffect(() => {
    if (
      state.phase === 'clarifying' &&
      state.clarification &&
      state.clarification !== lastClarificationRef.current
    ) {
      lastClarificationRef.current = state.clarification;
      setMessages(prev => [
        ...prev,
        { role: 'assistant', content: state.clarification! },
      ]);
    }
  }, [state.phase, state.clarification]);

  useEffect(() => {
    const raw = sessionStorage.getItem('tf.pending');
    if (!raw) {
      return;
    }

    const pending = JSON.parse(raw) as PendingPlan;
    setRequestId(pending.requestId);
    const pickNeeded = pending.latitude == null || pending.longitude == null;
    setNeedsMapPick(pickNeeded);

    if (!pickNeeded) {
      setLatitude(pending.latitude as number);
      setLongitude(pending.longitude as number);
    }

    const initialMessages: ChatMessageDto[] = [
      { role: 'user', content: pending.prompt },
    ];
    messagesRef.current = initialMessages;
    setMessages(initialMessages);

    if (!pickNeeded) {
      void runPlanning();
    }
    // eslint-disable-next-line react-hooks/exhaustive-deps -- hydrate once from sessionStorage
  }, []);

  useEffect(() => () => {
    session.abort();
  }, [session]);

  const onSend = (text: string) => {
    setMessages(prev => {
      const next = [...prev, { role: 'user' as const, content: text }];
      messagesRef.current = next;
      return next;
    });
    if (needsMapPick && !hasValidCoordinates) {
      return;
    }
    void runPlanning();
  };

  const onMapPick = (point: GeoPoint) => {
    setLatitude(point.latitude);
    setLongitude(point.longitude);
    setNeedsMapPick(false);
    void runPlanning();
  };

  return (
    <section className="plan-workspace">
      <aside className="plan-workspace__aside">
        {state.stops.length > 0 ? (
          <div className="plan-workspace__nav">
            <Link to="/detail">View itinerary detail →</Link>
          </div>
        ) : null}
        {state.spec ? <SpecSummary spec={state.spec} /> : null}
        <ChatPanel
          messages={messages}
          phase={state.phase}
          clarification={state.clarification}
          errorMessage={state.error?.message}
          onSend={onSend}
        />
        <ItineraryCards
          stops={state.stops}
          onSelect={placeId => setSelectedId(placeId)}
        />
      </aside>
      <div className="plan-workspace__map">
        <TravelMap
          places={state.places}
          selectedId={selectedId}
          stopOrder={stopIds(state.stops)}
          needsMapPick={needsMapPick}
          onPlaceSelect={placeId => setSelectedId(placeId)}
          onMapPick={onMapPick}
        />
      </div>
    </section>
  );
}
