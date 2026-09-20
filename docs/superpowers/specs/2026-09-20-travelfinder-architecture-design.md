# Travelfinder Architecture Reconstruction

Date: 2026-09-20  
Status: Draft for review  
Repo: `SuperSine/travelfinder`  
Path: in-place strangler of the existing monorepo

## 1. Purpose and success criteria

Reconstruct Travelfinder as a platform without turning it into a new product.

Keep the core loop: the user describes a trip, the system produces a structured intent, nearby places appear on the map, and an itinerary streams back.

Allow the API shape, agent tool boundary, and frontend information architecture to change.

Success for this slice:

- New UI talks only to the new planning stream. No Ionic or UI5 components remain in pages that ship.
- LLM interaction runs on Microsoft Agent Framework. Business code does not call Azure / xAI HTTP SDKs directly.
- Google Places and ArcGIS Feature Layer are hidden behind one place contract, with merge, dedupe, scoring, and cache in the facade.
- Old chat endpoints still compile but are frozen. New pages do not call them.

Out of scope for this slice:

- Auth / identity
- Saved itineraries and reopen
- Itinerary sharing
- User-contributed point writes (`ApplyEdits`)
- Capacitor packaging as a deliverable
- Replacing the ArcGIS basemap with Google Maps
- Multi-agent workflows beyond the two-stage split
- .NET 9/10 upgrade

## 2. Current system (constraints we are changing)

Monorepo with `travelfinder-web` and `travelfinder-api`.

Frontend today is Ionic 7 + Angular 17 + Capacitor, with `@ui5/webcomponents-ngx` and Tailwind already in `devDependencies`. Routes: `home`, `plan`, `detail`. Map uses `@arcgis/core`.

Backend today is ASP.NET Core 8. LLM access is hand-rolled HTTP (`AzureAIApiClient` against Azure OpenAI `gpt-4-2`, plus unused/parallel OpenAI and xAI clients). `ChatController.StreamCommand` is a procedural script: extract `PlanInfo` → Google text/nearby search → ArcGIS near query → inject place JSON into the prompt → SSE raw fragments.

GIS problems that this design addresses:

- Two place models unioned in the controller
- New `HttpClient` per call, proxy addresses hard-coded
- Instance-field cache that treats a 999 km move as a hit
- Retrieval tightly coupled to prompt assembly

## 3. Decisions already locked

| Topic | Decision |
|---|---|
| Reconstruction goal | Platform refactor; product shape may change slightly |
| Frontend stack | Keep Angular; remove Ionic and UI5; Tailwind + a few tiny primitives |
| Agent shape | Two-stage: Planner then Retriever/Renderer |
| GIS | Unified Place facade; Google + ArcGIS are providers |
| Conversation protocol | One SSE connection with typed events |
| Models | Azure OpenAI primary, xAI failover |
| Slice scope | Chat planning loop + Place facade only |

Approach: in-place strangler. Keep the two runnable units. Freeze old clients and `ChatController`. Put new traffic on a new planning endpoint.

## 4. Target architecture and module boundaries

One repository, two processes. No extra microservices.

### 4.1 Backend layers (same API process)

Dependency direction is Host → Agents → Facade → Providers.

**Host / HTTP**  
CORS, request validation, SSE framing, cancellation. New planning controller (name may be `PlanController`). Does not contain GIS merge logic or prompt templates beyond wiring.

**Agents**  
Microsoft Agent Framework (`Microsoft.Agents.AI` and the Azure OpenAI / chat-client packages required to host two agents).

- Planner: messages + user coordinates → `PlanSpec` or `clarification`.
- Renderer: confirmed `PlanSpec` + `Place[]` → streamed itinerary.

Agents depend on tool interfaces implemented by the facade. They do not reference Google or ArcGIS SDKs. They do not reference ASP.NET types.

Chat client resolution lives in Host: Azure is default; xAI is the backup `IChatClient`. Agent code does not branch on vendor.

**Gis facade**  
Single port: `IPlaceService`. Methods required this slice: `SearchText`, `Nearby`, `ReverseGeocode`, `MergeAndRank` (exposed to Host as `get_merged_places`).

**Providers**

- `GooglePlaceProvider`: Places Text Search, Nearby, Geocode
- `ArcGisPlaceProvider`: existing point Feature Layer, read-only query, mapped to `Place`

Merge, dedupe, scoring, and cache live in the facade, not in each provider.

### 4.2 Frontend layers (same Angular app)

**Shell**  
Keep `home` / `plan` / `detail`. Replace Ionic menu/tabs/`ion-*` layout with a Tailwind shell: top bar + main pane. Plan workspace is chat + map side by side on desktop, stacked on mobile.

**Domain + client**  
TypeScript types mirror backend: `PlanSpec`, `Place`, `PlanEvent`. `PlanClient` understands only `POST /plan/stream` and the six event types.

**Widgets**  
Map is an evolution of the current `@arcgis/core` component. Chat, place list, and itinerary cards are Angular + Tailwind. No Angular Material, no re-adding Ionic, no UI5.

### 4.3 Non-boundaries

- No standalone agent service and no GIS microservice
- No login, persistence, sharing, or feature-layer writes
- No basemap vendor change
- No framework upgrade unless separately approved

## 5. Data flow and event contract

### 5.1 Endpoint

One user-visible stream:

`POST /plan/stream`  
`Content-Type: application/json`  
Response: `text/event-stream`

Internally two stages; externally one connection. The frontend renders by event type.

### 5.2 Request

```json
{
  "messages": [{ "role": "user", "content": "..." }],
  "latitude": 1.3521,
  "longitude": 103.8198,
  "language": "en-us",
  "requestId": "client-generated-id"
}
```

`language` is optional; infer from messages or a server default.  
`systemId` is not accepted. System prompts and tools stay on the server.  
Clients do not send place JSON.

### 5.3 SSE events

Each frame uses an `event:` name and a closed JSON `data:` payload. Raw model `tool_calls` fragments are never forwarded.

| Event | When | Frontend |
|---|---|---|
| `clarification` | Planner lacks a constraint | Ask in chat; do not draw a route |
| `plan_spec` | Structured intent is ready | Show a short summary |
| `places` | Facade emitted merged candidates (may repeat) | Plot points; itinerary may still be empty |
| `plan_delta` | Renderer streamed an itinerary patch | Merge by `dayIndex` + `stopIndex` |
| `error` | Validation, timeout, total model failure, empty places | Retry UI; do not treat stack traces as chat |
| `done` | Stream finished, including clarification-only turns | Clear loading |

`plan_delta` is a patch, not a full itinerary replay on every token.

### 5.4 Server sequence

1. Host opens SSE, validates coordinates and `requestId`, binds a cancellation token to the disconnected client.
2. Planner reads `messages` plus `ReverseGeocode` area label (fallback: `"current location"`).
   - If clarification: emit `clarification`, then `done`, stop. No nearby search.
   - If spec: emit `plan_spec`.
3. Host calls facade `get_merged_places(planSpec)`. Emit one or more `places` events.
4. Renderer sees only `PlanSpec` + the merged `Place[]`. Emit `plan_delta` as structured stops become available.
5. Emit `done`.

If GIS is partially down but some places exist, continue rendering and record a partial failure in logs. If every provider returns nothing, emit `error(code=no_places)` rather than inventing coordinates.

### 5.5 `PlanSpec`

```ts
{
  language: string;
  areaLabel: string;
  radiusMeters: number;
  categories: string[];
  pointOfInterests: string[];
  budgetLevel: "low" | "moderate" | "high";
  dayCount: number;
  notes: string;
}
```

`dayCount` defaults to 1.  
`radiusMeters` is clamped by the facade to `[500, 20000]`. Default search radius is 5000.

Planner must not put a concrete place list or invented lat/lng into the spec. Places come only from the facade.

### 5.6 Failover and time budget

- Model calls: on Azure timeout, 429, or 5xx, retry once on xAI. Frontend does not see the switch. `done` may include `providerUsed` for debugging.
- GIS: a single provider failure is skipped. There is no "map vendor failover".
- Whole-stream deadline: 45 seconds, then `error(code=timeout)` + `done`. Keep any spec/places already sent.
- Cache hits count as GIS success.

### 5.7 Legacy protocol

`/Chat/StreamCommand`, `/Chat/Command`, `/Chat/Hint`, `/Chat/Post`, `/Chat/GetPlanInfo` stay in the tree this slice. New UI does not call them. Compatibility with the old SSE text format is not required.

## 6. Place facade

### 6.1 Unified `Place`

```ts
{
  id: string;                 // `${source}:${sourceId}`
  source: "google" | "arcgis";
  sourceId: string;
  name: string;
  address?: string;
  primaryType?: string;
  categories: string[];
  rating?: number;
  priceLevel?: string;
  location: { latitude: number; longitude: number };
  score: number;              // 0–1
}
```

When two records collapse to one place, keep the higher-scoring row. Do not expose aliases this slice.

### 6.2 Provider rules

Google: Text Search, Nearby, Reverse Geocode. Keep field masks tight: display name, address, type, rating, location, id, price level.

ArcGIS: current point Feature Layer spatial query only. No ArcGIS Places near-point expansion. No `ApplyEdits`.

Each provider times out independently (4–6 seconds) and may return empty. The facade does not fail the whole request because one provider threw.

`HttpClient` is a singleton per provider. Proxy settings come from configuration (`ENABLE_PROXY` remains an env switch). No `new HttpClient()` per call.

### 6.3 Retrieval plan

Host, not the model, decides the search fan-out after `PlanSpec` exists.

Parallel tasks:

- Each `pointOfInterests` term → Google Text Search, biased to user point + `radiusMeters`
- If `categories` is non-empty → Google Nearby with types clipped to the allowed Google set
- Always → ArcGIS radius query (read-only user points)

Per-source cap: 20. Merged hard cap: 40.

### 6.4 Dedupe

1. Same `source + sourceId` collapses.
2. Normalized name (lower case, collapsed whitespace, trivial suffix strip) and distance ≤ 80 meters collapses.
3. Keep the row with a rating over one without; Google discovery wins over an ArcGIS row with the same name. Distinct ArcGIS names are kept so user points remain visible.

No hours, photos, or admin-boundary matching this slice.

### 6.5 Score

```
score = 0.45 * typeMatch
      + 0.25 * distance
      + 0.20 * rating
      + 0.10 * sourcePrior
```

- `typeMatch`: overlap with `PlanSpec.categories` or POI terms
- `distance`: linear decay from the user point (or POI centroid) to 0 at radius
- `rating`: Google rating / 5, default 0.5 when missing
- `sourcePrior`: Google 0.6, ArcGIS 0.8 so sparse user points are not buried under 20 Google rows

Scoring is for sort and truncation only. The renderer receives an already ordered list.

### 6.6 Cache

Replace the instance-field 999 km cache with `IMemoryCache` keyed on a grid.

- Nearby: `nearby|{provider}|{geohashPrecision7}|{radiusBucket}|{typesHash}|{lang}`
- Text: `text|{provider}|{normalizedQuery}|{geohash7}|{radiusBucket}|{lang}`
- Reverse geocode: `rev|{geohash8}`

TTL: 10 minutes for Nearby/Text, 24 hours for reverse geocode.

Cache stores per-provider mapped results. Merge and score run on every request so a different `PlanSpec` cannot reuse a dirty ranking.

Process memory only. No Redis this slice.

### 6.7 Tools visible to agents

- `reverse_geocode(lat, lng)` — Planner default
- `search_places(query, lat, lng, radius)`
- `nearby_places(lat, lng, radius, categories)`
- `get_merged_places(planSpec)` — Host/Renderer main path

Planner should not loop Nearby itself. Retrieval is Host-triggered after `plan_spec`. That is the main GIS cost control.

## 7. Frontend structure and Tailwind migration

### 7.1 Pages

Keep routes. Rebuild the shell.

- `/home` — short prompt, geolocation, start planning
- `/plan` — workspace: chat/events + map/places
- `/detail` — read-only itinerary or place detail

Ionic menu/tabs go away. Capacitor packages may remain in `package.json` but pages must not assume a native shell. Geolocation uses the browser first; on failure, ask the user to pick a point.

### 7.2 Folders

- `core/api` — `PlanClient`
- `core/domain` — shared types
- `core/state` — one planning-session store
- `features/chat` — messages, clarification input, phase indicator
- `features/map` — current map component, inputs are `Place[]`
- `features/plan` — itinerary cards fed by merged `plan_delta`

Existing `plan-detail` / `plan-item` may move in after UI5/Ionic markup is removed.

Shared UI is limited to tiny primitives (`ui-button`, `ui-panel`, `ui-spinner`) plus Tailwind utility classes.

### 7.3 Session phase

`idle → planning → clarifying | retrieving → rendering → done | error`

- `clarification` → `clarifying`; next turn sends the full `messages` list
- `plan_spec` → `retrieving`; optional spec summary in a side panel
- `places` → incremental map graphics; do not wipe points already drawn
- `plan_delta` → merge by `dayIndex` / `stopIndex`
- `error` does not discard places or plan already shown
- Only a new user-started plan resets the session

Invalid event JSON becomes `error`. No regex over raw SSE text.

### 7.4 Map

Basemap, graphics, and click popups stay on `@arcgis/core`.

The map component accepts `Place[]`, optional selected `id`, and optional stop order. It does not call Google Places from the browser.

If a working routing service is not already in the page, this slice draws a polyline in stop order. Do not add Network Analyst or Google Directions here.

### 7.5 Migration order

1. Wire Tailwind into global styles; remove global Ionic CSS and UI5 theme imports.
2. Convert shell + `home`. A page is done only when its DOM has no `ion-` or `ui5-`.
3. Convert the `plan` workspace (highest risk).
4. Convert `detail`.

Acceptance widths: 375px and 1280px must both complete send → see points → see itinerary. Map pan/zoom/select must not drop graphics.

## 8. Errors, observability, tests

### 8.1 Error taxonomy

| Code / class | Example | Stream | User |
|---|---|---|---|
| `validation` | Missing coordinates, empty messages, radius out of range | Immediate `error` + `done` | Fix input |
| `clarification` | Not an error | `clarification` + `done` | Answer and resend |
| `provider_partial` | One GIS provider timed out | Log; still emit places | Do not abort |
| `no_places` | Merge produced zero places | `error` | Suggest wider radius or fewer filters |
| `model_primary_failed` | Azure 429/5xx/timeout | Fail over to xAI | Invisible |
| `model_unavailable` | Both models failed | `error` | Retry |
| `timeout` | Stream exceeded 45s | `error` + `done` | Keep partial spec/places |
| `internal` | Unexpected exception | `error`; log `requestId` | Generic copy |

`error` payload: `{ code, message, requestId, retryable }`.  
`message` is short and user-facing. Details stay in logs.

### 8.2 Observability

One structured log line per `/plan/stream` with: `requestId`, stage timings (plan / gis / render), `providerUsed`, per-source place counts, cache hits, event sequence, error code.

No extra APM product this slice. Cache hits and Azure→xAI failover must be searchable in logs.

### 8.3 Tests

**Gis (write first)**

- Dedupe: same id; near-name within 80 m; same name beyond 80 m stays two rows
- Score: type match, distance decay, ArcGIS point not fully buried
- Cache key: same geohash7 + radius bucket hits; category change misses
- Partial failure: Google throws, ArcGIS rows still returned

Providers use fake HTTP. No live Google or ArcGIS in CI.

**Agents**

- Sparse input → clarification only, `get_merged_places` not called
- Complete input → `PlanSpec` with clamped radius and allowed categories
- Renderer writes stops only from provided `Place[]`
- Azure failure → exactly one xAI attempt

Fake `IChatClient` and fake tools. Do not assert prose quality.

**Host**

- Happy event order: `plan_spec` → `places` → `plan_delta*` → `done`
- Clarification-only turn emits no `places`
- Client abort stops further events

**Frontend**

- `PlanClient` parses frames into the event union; bad JSON → `error`
- Store merges `plan_delta`; only a new plan clears state
- Map updates incrementally on a second `places` event

One manual script is enough for E2E this slice: from home, send a line, see points and an itinerary. Cypress/Playwright is not a delivery gate.

## 9. Definition of done

- New UI uses only `/plan/stream`
- Shipped pages contain no `ion-` or `ui5-` elements
- Business / agent code does not reference the frozen `*ApiClient` types
- Facade unit tests cover dedupe, cache, and partial failure
- With Azure disabled locally, planning still completes through xAI when configured
- `ChatController` still builds; new pages do not call it

## 10. Implementation order (design only)

Not an implementation plan. Suggested vertical slices after this spec is approved:

1. Contracts + facade + provider adapters + GIS tests
2. Planning host + SSE events with fake agents
3. Real Planner/Renderer on MAF + Azure/xAI failover
4. Angular shell + Tailwind + `PlanClient` + session store
5. Map and itinerary wired to typed events
6. Strip remaining Ionic/UI5 from `plan` and `detail`

Writing the detailed implementation plan is a separate step (`writing-plans`) after this document is accepted.
