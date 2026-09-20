# Travelfinder

Chat-based trip planner: describe a day, get nearby places on a map and a streamed itinerary.

The 2026 reconstruction (PR [#1](https://github.com/SuperSine/travelfinder/pull/1)) keeps that loop and replaces the old Ionic/UI5 shell and hand-rolled LLM HTTP clients.

## What it does now

1. User writes a prompt on `/home` and shares a location (browser geolocation, or a map pick if that fails).
2. The UI opens one SSE connection to `POST /plan/stream`.
3. A **Planner** agent returns either a structured `PlanSpec` or a clarification question.
4. A **Place facade** searches Google Places and a read-only ArcGIS feature layer in parallel, then merges, dedupes, scores, and caches.
5. A **Renderer** agent writes itinerary stops only from those places. The UI applies `plan_delta` patches and plots `Place[]` on an ArcGIS basemap.

New pages do not call the frozen `/Chat/*` endpoints.

## Stack

| Piece | After PR #1 |
|---|---|
| Repo | Monorepo: `travelfinder-api` + `travelfinder-web` |
| API | ASP.NET Core 8, Microsoft Agent Framework |
| Models | Azure OpenAI (`Planning:AzureDeployment`) primary, xAI failover |
| GIS | `IPlaceService` → Google Places + ArcGIS point layer |
| Web | Angular 17 standalone, Tailwind, no Ionic/UI5 in shipped pages |
| Map | `@arcgis/core` |

Design: [`docs/superpowers/specs/2026-09-20-travelfinder-architecture-design.md`](docs/superpowers/specs/2026-09-20-travelfinder-architecture-design.md)  
Task plan: [`docs/superpowers/plans/2026-09-20-planning-stream-and-place-facade.md`](docs/superpowers/plans/2026-09-20-planning-stream-and-place-facade.md)

## Layout

```
travelfinder-api/
  Controllers/PlanController.cs   POST /plan/stream
  Host/                           SSE orchestration
  Agents/                         Planner + Renderer + Azure→xAI failover
  Gis/                            IPlaceService, providers, cache, score
  Controllers/ChatController.cs   frozen legacy protocol
  TravelfinderAPI.Tests/
travelfinder-web/
  src/app/pages/{home,plan,detail}
  src/app/core/{api,domain,state} PlanClient, types, session store
  src/app/features/{chat,map,plan}
```

## Run locally

Needs .NET 8, Node 20+, and the keys below.

```bash
# API — http://localhost:1294 (Swagger in Development)
cd travelfinder-api
dotnet run --launch-profile TravelfinderAPI

# Web — http://localhost:4200
cd travelfinder-web
npm install
npm start
```

`travelfinder-web/src/environments/environment.ts` already points `API_URL` at `http://localhost:1294`.

CORS allows `http://localhost:4200`. If `ENABLE_PROXY` is true, outbound Google/Azure/xAI calls go through `socks5://127.0.0.1:2085`. Set `ENABLE_PROXY=false` when you do not have that proxy.

### Configuration

Prefer environment variables or user secrets. Do not commit keys.

| Key | Role |
|---|---|
| `AZUREAI_API_KEY` | Primary chat client |
| `XAI_API_KEY` | Failover when Azure returns 429 / 5xx / timeout |
| `GMPGIS_API_KEY` | Google Places |
| `ARCGIS_API_KEY` | ArcGIS (legacy clients + web basemap) |
| `FEATURE_LAYER` | Array; index `1` is the read-only point layer |
| `ENABLE_PROXY` | `true` / `false` |
| `Planning:StreamDeadlineSeconds` | Whole-stream budget (default 45) |

Azure endpoint and deployment live under `Planning` in `appsettings.json`. Empty Azure key + a valid xAI key is a supported local mode.

## Planning stream

`POST /plan/stream`  
Request `application/json`, response `text/event-stream`.

```json
{
  "messages": [{ "role": "user", "content": "one park day near me" }],
  "latitude": 1.283,
  "longitude": 103.851,
  "language": "en-us",
  "requestId": "client-generated-id"
}
```

Do not send `systemId` or client-side place JSON.

| SSE `event` | Meaning |
|---|---|
| `clarification` | Planner needs a constraint; no nearby search |
| `plan_spec` | Structured intent |
| `places` | Merged candidates (may repeat; UI must not wipe earlier points) |
| `plan_delta` | Itinerary patch keyed by `dayIndex` + `stopIndex` |
| `error` | `{ code, message, requestId, retryable }` |
| `done` | Stream finished, including clarification-only turns |

## Tests

```bash
cd travelfinder-api
dotnet test TravelfinderAPI.Tests/TravelfinderAPI.Tests.csproj

cd travelfinder-web
npx ng test --no-watch --browsers=ChromeHeadless
```

API tests cover facade dedupe / score / cache / partial GIS failure, SSE event order, planner clarification vs spec, renderer stops, and Azure→xAI failover. Web tests cover `PlanClient`, the session store, and the absence of `ion-` / `ui5-` on the new pages. Cypress/Playwright is not a delivery gate.

Manual loop: [`docs/superpowers/plans/2026-09-20-planning-stream-manual.md`](docs/superpowers/plans/2026-09-20-planning-stream-manual.md).

## Out of scope (this slice)

Auth, saved itineraries, sharing, ArcGIS `ApplyEdits`, Capacitor packaging, replacing the ArcGIS basemap with Google Maps, and a .NET 9/10 upgrade.

Legacy `/Chat/StreamCommand`, `/Chat/Command`, `/Chat/Hint` still compile. New UI does not use them.

## Legacy screenshots

Pre-refactor Ionic/UI5 captures (no longer the shipped shell):

![legacy map](https://github.com/sine-zhang/travelfinder/assets/175175559/b0bd19c8-2bd4-4522-b49b-84f288070202)
![legacy chat](https://github.com/sine-zhang/travelfinder/assets/175175559/68f81f10-83f5-4741-9d73-ce58a2fde4f7)
