export type PlaceSource = 'google' | 'arcgis';
export type BudgetLevel = 'low' | 'moderate' | 'high';

export interface GeoPoint { latitude: number; longitude: number; }

export interface Place {
  id: string;
  source: PlaceSource;
  sourceId: string;
  name: string;
  address?: string;
  primaryType?: string;
  categories: string[];
  rating?: number;
  priceLevel?: string;
  location: GeoPoint;
  score: number;
}

export interface PlanSpec {
  language: string;
  areaLabel: string;
  radiusMeters: number;
  categories: string[];
  pointOfInterests: string[];
  budgetLevel: BudgetLevel;
  dayCount: number;
  notes: string;
}

export interface ChatMessageDto { role: 'user' | 'assistant'; content: string; }

export interface PlanRequest {
  messages: ChatMessageDto[];
  latitude: number;
  longitude: number;
  language?: string;
  requestId: string;
}

export interface ItineraryStop {
  dayIndex: number;
  stopIndex: number;
  placeId: string;
  name: string;
  reason?: string;
  durationMinutes?: number;
}

export interface PlanError {
  code: 'validation' | 'no_places' | 'model_unavailable' | 'timeout' | 'internal';
  message: string;
  requestId: string;
  retryable: boolean;
}

export type PlanEvent =
  | { type: 'clarification'; message: string }
  | { type: 'plan_spec'; spec: PlanSpec }
  | { type: 'places'; places: Place[] }
  | { type: 'plan_delta'; stop: ItineraryStop }
  | { type: 'error'; error: PlanError }
  | { type: 'done'; providerUsed?: string };
