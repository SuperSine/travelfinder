# Travelfinder React 19 + Astryx Frontend Migration

Date: 2026-11-09  
Status: Draft for review  
Repo: `SuperSine/travelfinder`  
Scope: in-place rewrite of `travelfinder-web` only; API and Place facade unchanged

## 1. Purpose and success criteria

Migrate Travelfinder’s shipped web UI from Ionic 7 + Angular 17 to a Vite + React 19 SPA built with Astryx components, without changing product behavior or the planning stream contract.

Success for this design:

- `travelfinder-web` runs as a React 19 SPA (Vite, React Router) with Astryx for layout and components.
- All user-facing flows still use only `POST /plan/stream` with the same six SSE event types defined in the 2026-09-20 architecture design (`clarification`, `plan_spec`, `places`, `plan_delta`, `error`, `done`).
- Map behavior matches the current planning-stream slice: `@arcgis/core`, `Place[]` inputs, incremental graphics, no browser-side Google Places calls.
- Angular, Ionic, UI5 shell markup, and Capacitor assumptions are removed from shipped pages by the end of the migration; the old stack is deleted in the final purge slice, not left as a parallel app.
- Backend (`travelfinder-api`), Place facade, agent shape, and `/Chat/*` freeze remain as locked in the 2026-09-20 design.

## 2. Locked decisions

| Topic | Decision |
|---|---|
| Migration scope | Full-site migrate `travelfinder-web` to React 19 + Astryx; product behavior and `POST /plan/stream` SSE contract stay unchanged. |
| Scaffold | Vite + React Router SPA (not Next.js). |
| Repository layout | In-place rewrite of `travelfinder-web`; delete Ionic/UI5 shell with Angular (no parallel package). |
| Theme | `@astryxdesign/theme-neutral` default; themes are swappable via Astryx CLI. |
| Styling | Plain CSS path + Astryx `xstyle` overrides; do NOT add a StyleX Vite/Rollup compiler for app code. |
| Map | Keep `@arcgis/core`; same behavior as current planning-stream slice. |
| Migration rhythm | Vertical slices home → plan → detail, each independently testable; delete Angular last. |
| API | Backend frozen for this work; `/Chat/*` stays frozen; new UI talks only to `/plan/stream`. |

## 3. Target stack and package layout under `travelfinder-web`

**Runtime and tooling**

- **Bundler / dev server:** Vite (current major aligned with repo standards).
- **UI library:** React 19.
- **Routing:** React Router (client-side SPA; no SSR framework).
- **Design system:** `@astryxdesign/core`, default theme `@astryxdesign/theme-neutral`.
- **Styling peer for Astryx `xstyle`:** `@stylexjs/stylex` as a dependency peer only; application styles use plain CSS imports plus `xstyle` overrides where Astryx documents them—no StyleX compile step in Vite for app source.
- **Map:** `@arcgis/core` (same major/integration pattern as today).

**Package root**

All frontend code remains under the existing `travelfinder-web` package name and npm workspace entry. There is no `travelfinder-web-react` sibling package.

Typical dependency additions (exact versions pinned at implementation time):

- `react`, `react-dom`, `react-router-dom`
- `@astryxdesign/core`, `@astryxdesign/theme-neutral`
- `@stylexjs/stylex` (for Astryx xstyle peer contract)
- `@arcgis/core`
- Vite + `@vitejs/plugin-react` and TypeScript tooling as required by the monorepo

Angular, Ionic, UI5, and Capacitor packages are removed in the final purge slice after React routes replace shipped pages.

**Build output**

Standard Vite `dist/` SPA assets; deployment wiring stays whatever the monorepo uses today for `travelfinder-web`.

## 4. Frontend module boundaries

Dependency direction: pages → features → core. Features do not import from each other except through core types/state or explicit page composition.

### 4.1 `core/api`

- **`PlanClient`:** Owns `POST /plan/stream` only. Parses SSE frames into the typed event union; invalid JSON surfaces as `error` per the 2026-09-20 contract. No calls to `/Chat/*`.

### 4.2 `core/domain`

TypeScript types aligned with backend and the 2026-09-20 spec: `PlanSpec`, `Place`, `PlanEvent` (discriminated union over the six event names), request body for the stream, session identifiers as needed.

### 4.3 `core/state`

One planning-session store (React context + reducer, or equivalent single-store pattern chosen at implementation). Responsibilities:

- Session phase machine (see §5).
- Accumulated messages, spec summary, `Place[]`, merged itinerary from `plan_delta`.
- Loading and error flags; retry affordances driven by `error` payloads.

### 4.4 `features/chat`

Messages list, clarification input, phase indicator. Renders user/assistant content from session state; does not parse raw SSE.

### 4.5 `features/map`

Evolution of the current ArcGIS map: inputs `places`, optional selected `id`, optional stop order. Incremental graphics on repeated `places` events. No Google Places from the browser.

### 4.6 `features/plan`

Itinerary cards and spec summary UI fed by merged `plan_delta` and `plan_spec`.

### 4.7 Pages

- **`home`:** Short prompt, geolocation (browser first; fallback point pick), navigate into planning.
- **`plan`:** Workspace—chat/events + map/places; desktop side-by-side, mobile stacked (product behavior unchanged).
- **`detail`:** Read-only itinerary or place detail.

### 4.8 Theme provider and CSS import order

At application bootstrap:

1. Global reset / normalize (plain CSS).
2. Astryx base styles from `@astryxdesign/core` (per Astryx install docs).
3. `@astryxdesign/theme-neutral` theme CSS.

Wrap the app tree in Astryx theme provider (or documented equivalent) so components resolve tokens. Swapping themes later is a documented CLI + import swap, not a runtime plugin in this slice.

## 5. Product, routes, session phases, and SSE event handling

### 5.1 Routes

Keep the same three routes as today:

- `/home`
- `/plan`
- `/detail`

React Router defines these; deep links and back/forward behave as a standard SPA.

### 5.2 Session phases

`idle → planning → clarifying | retrieving → rendering → done | error`

| Server event | Phase / behavior |
|---|---|
| `clarification` | `clarifying`; user answers; next turn sends full `messages` |
| `plan_spec` | `retrieving`; optional spec summary |
| `places` | Incremental map graphics; do not wipe existing points |
| `plan_delta` | `rendering`; merge patches by `dayIndex` + `stopIndex` |
| `error` | `error`; do not discard places or plan already shown |
| `done` | `done` or return toward `idle` for a new turn; clear loading |

Only a user-started new plan resets session state. Invalid event JSON becomes client-side `error` handling; no regex over raw SSE text.

### 5.3 Stream contract (unchanged)

Reuse the 2026-09-20 architecture design in full for:

- Request body (`messages`, `latitude`, `longitude`, optional `language`, `requestId`).
- Event names and frontend responsibilities (§5.3 of that document).
- `PlanSpec` and `Place` shapes.
- Legacy `/Chat/*` not used by new UI.

Do not introduce new SSE event types or alternate endpoints for this migration.

## 6. Migration slices

Each slice is independently demonstrable before the next begins. Angular code may coexist temporarily only until the purge slice; no long-lived dual UI.

| Slice | Work | Acceptance |
|---|---|---|
| **Scaffold** | Vite + React 19 + React Router + Astryx theme wiring; empty shell with routes | App serves `/home`, `/plan`, `/detail`; CSS order §4.8; build succeeds |
| **Core port** | `core/domain`, `PlanClient`, session store; wire one dev-only stream test or mock | Unit tests parse all six events; bad JSON → `error`; store merges `plan_delta` |
| **Home** | React `home` page replaces Angular home for the start flow | From `/home`, user can set location and navigate to plan; no `ion-`/`ui5-` on this page |
| **Plan** | Full workspace: chat + map + itinerary on typed events | Manual: send → see points → see itinerary at 375px and 1280px on this page |
| **Detail** | React `detail` read-only view | Deep link or navigation from plan works; no legacy shell markup |
| **Purge** | Remove Angular, Ionic, UI5, Capacitor from dependencies and source; delete unused Angular folders | `travelfinder-web` builds and runs without `@angular/*`, `@ionic/*`, UI5; shipped routes are React-only |

Delete Angular last: do not remove the old build until plan and detail slices pass acceptance.

## 7. Testing

**Automated (this migration slice)**

- Unit tests for `PlanClient` (frame parsing, event union, error on malformed `data`).
- Unit tests for session store (`plan_delta` merge, new plan reset, phase transitions on each event type).

**Manual (required gate)**

- Script: from home, send a planning message, observe map points, observe streamed itinerary.
- Viewports: 375px and 1280px minimum.
- Map: pan, zoom, select without dropping graphics on a second `places` event.

Cypress/Playwright is not a delivery gate for this slice (consistent with 2026-09-20 §8.3).

## 8. Out of scope

- Auth / identity
- Saved itineraries, persistence, reopen
- Itinerary sharing
- Basemap vendor change (stay on ArcGIS)
- Capacitor native packaging as a deliverable
- StyleX Vite/Rollup compiler plugin for app code
- Backend changes, new API endpoints, or `/Chat/*` behavior changes
- Next.js or SSR framework adoption
- Changes to Place facade, agents, or GIS merge rules

## 9. Definition of done

- `travelfinder-web` is a React 19 + Astryx SPA with Vite and React Router.
- All shipped routes use only `POST /plan/stream` and the six locked SSE events.
- No `ion-`, `ui5-`, or Angular components in DOM for home, plan, or detail.
- `@arcgis/core` map behavior matches the planning-stream acceptance criteria.
- `PlanClient` and session store unit tests pass in CI where the monorepo runs frontend tests.
- Manual send → points → itinerary passes at 375px and 1280px.
- Angular/Ionic/UI5/Capacitor removed from `travelfinder-web` in the purge slice.

## 10. Next step

After this spec is approved, author the detailed implementation plan via the `writing-plans` skill to:

`docs/superpowers/plans/YYYY-MM-DD-react-astryx-frontend.md`

That plan will break slices into tasks, files, and verification steps without revisiting locked API or SSE decisions.
