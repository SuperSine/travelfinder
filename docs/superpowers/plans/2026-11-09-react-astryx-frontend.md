# React 19 + Astryx Frontend Migration Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace travelfinder-web Angular/Ionic/UI5 with a Vite + React 19 + React Router + Astryx SPA while preserving POST /plan/stream behavior.

**Architecture:** In-place rewrite of travelfinder-web; pages → features → core; Astryx Theme + plain CSS import order; ArcGIS map unchanged behaviorally.

**Tech Stack:** Vite, React 19, React Router, @astryxdesign/core, @astryxdesign/theme-neutral, @stylexjs/stylex (xstyle peer only, no StyleX Vite plugin), @arcgis/core, Vitest (or equivalent chosen in Task 1)

**Spec:** `docs/superpowers/specs/2026-11-09-react-astryx-frontend-design.md`  
**SSE contract reference:** `docs/superpowers/specs/2026-09-20-travelfinder-architecture-design.md` §5 (do not change API)

## Global Constraints

- Preserve POST /plan/stream and the six SSE events: clarification, plan_spec, places, plan_delta, error, done
- Do not call /Chat/*
- Do not add Next.js or StyleX Vite/Rollup compiler for app code
- Default theme: theme-neutral (swappable later)
- Keep @arcgis/core
- Vertical slices: scaffold → core → home → plan → detail → purge Angular
- Backend frozen; no API changes
- No secrets in repo; no force-push

---

## File Structure

React source lives under `travelfinder-web/src/`. Legacy Angular code stays under `travelfinder-web/src/app/` until Task 9 deletes it. After Task 1, `npm run dev` / `build` / `test` target Vite + Vitest only.

### Create (React)

| File | Responsibility |
|---|---|
| `travelfinder-web/index.html` | Vite entry HTML |
| `travelfinder-web/vite.config.ts` | React plugin, dev proxy optional, Vitest |
| `travelfinder-web/tsconfig.json` | TS for React (`jsx: react-jsx`) |
| `travelfinder-web/tsconfig.node.json` | Vite config TS |
| `travelfinder-web/src/main.tsx` | Bootstrap + router |
| `travelfinder-web/src/App.tsx` | Shell + route outlet |
| `travelfinder-web/src/app.css` | Reset → astryx → theme-neutral import order |
| `travelfinder-web/src/app/Providers.tsx` | `Theme` + `LinkProvider` |
| `travelfinder-web/src/config/environment.ts` | `import.meta.env.VITE_*` API URLs |
| `travelfinder-web/src/vite-env.d.ts` | Vite client types |
| `travelfinder-web/src/core/domain/models.ts` | Port of Angular `models.ts` |
| `travelfinder-web/src/core/api/sse-parser.ts` | SSE frame → `PlanEvent[]` |
| `travelfinder-web/src/core/api/plan-client.ts` | `POST /plan/stream` async generator |
| `travelfinder-web/src/core/api/sse-parser.test.ts` | Vitest |
| `travelfinder-web/src/core/api/plan-client.test.ts` | Vitest |
| `travelfinder-web/src/core/state/planning-session.tsx` | Context + reducer + `run()` |
| `travelfinder-web/src/core/state/planning-session.test.ts` | Vitest phase + merge |
| `travelfinder-web/src/features/chat/ChatPanel.tsx` | Messages, phase, send |
| `travelfinder-web/src/features/plan/ItineraryCards.tsx` | Grouped stops |
| `travelfinder-web/src/features/plan/SpecSummary.tsx` | Optional `plan_spec` summary |
| `travelfinder-web/src/features/map/place-graphics.ts` | Incremental place ids |
| `travelfinder-web/src/features/map/map-view-ready-gate.ts` | Defer sync until view ready |
| `travelfinder-web/src/features/map/TravelMap.tsx` | `@arcgis/core` map |
| `travelfinder-web/src/features/map/travel-map.css` | Map container height |
| `travelfinder-web/src/pages/HomePage.tsx` | Prompt + geolocation |
| `travelfinder-web/src/pages/PlanPage.tsx` | Workspace composition |
| `travelfinder-web/src/pages/DetailPage.tsx` | Read-only itinerary |
| `travelfinder-web/src/pages/plan-workspace.css` | Responsive grid (375 / 1280) |
| `travelfinder-web/.env.example` | Document `VITE_API_URL`, `VITE_ARCGIS_API_KEY` |

### Modify

| File | Change |
|---|---|
| `travelfinder-web/package.json` | React 19 + Vite scripts; drop Angular scripts after Task 9 |
| `travelfinder-web/src/environments/environment.ts` | Deprecate; secrets move to env (delete in Task 9) |

### Delete (Task 9 purge)

| Path | Reason |
|---|---|
| `travelfinder-web/angular.json` | Angular build |
| `travelfinder-web/karma.conf.js` | Karma |
| `travelfinder-web/src/app/**` | Angular app tree |
| `travelfinder-web/src/main.ts` (Angular) | Replaced by `src/main.tsx` |
| Ionic / Capacitor / UI5 / `@angular/*` deps | Purge slice |

### Port reference (read-only parity)

| Angular source | React target |
|---|---|
| `src/app/core/domain/models.ts` | `src/core/domain/models.ts` |
| `src/app/core/api/sse-parser.ts` | `src/core/api/sse-parser.ts` |
| `src/app/core/api/plan-client.ts` | `src/core/api/plan-client.ts` |
| `src/app/core/state/planning-session.store.ts` | `src/core/state/planning-session.tsx` |
| `src/app/features/map/*` | `src/features/map/*` |
| `src/app/features/chat/chat-panel.component.ts` | `src/features/chat/ChatPanel.tsx` |
| `src/app/features/plan/itinerary-cards.component.ts` | `src/features/plan/ItineraryCards.tsx` |
| `src/app/pages/home/*` | `src/pages/HomePage.tsx` |
| `src/app/pages/plan/*` | `src/pages/PlanPage.tsx` |
| `src/app/pages/detail/*` | `src/pages/DetailPage.tsx` |

---

### Task 1: Scaffold Vite + React 19 + Astryx

**Approach:** Replace `package.json` scripts immediately so `dev` / `build` / `test` run Vite + Vitest. Keep unused Angular sources under `src/app/` until Task 9; they are not imported by the React entry.

**Files:**
- Create: `travelfinder-web/index.html`, `vite.config.ts`, `tsconfig.json`, `tsconfig.node.json`
- Create: `src/main.tsx`, `src/App.tsx`, `src/app.css`, `src/app/Providers.tsx`, `src/vite-env.d.ts`
- Create: `src/pages/HomePage.tsx`, `src/pages/PlanPage.tsx`, `src/pages/DetailPage.tsx` (stubs)
- Create: `.env.example`
- Modify: `travelfinder-web/package.json`

**Interfaces:**
- Consumes: nothing
- Produces: `Providers`, routes `/home`, `/plan`, `/detail`, CSS import order per Astryx Vite docs

- [ ] **Step 1: Install dependencies**

Run from `travelfinder-web`:

```bash
npm install react@^19 react-dom@^19 react-router-dom@^7 \
  @astryxdesign/core@^0.7.11 @astryxdesign/theme-neutral@^0.7.11 @stylexjs/stylex@^0.11.1 \
  @arcgis/core@^4.29.10
npm install -D vite@^6 @vitejs/plugin-react@^4 typescript@~5.6 vitest@^3 @testing-library/react@^16 \
  @testing-library/jest-dom@^6 jsdom@^25 @types/react @types/react-dom
```

Expected: PASS — `package-lock.json` updates without peer dependency errors.

- [ ] **Step 2: Replace `package.json` scripts**

```json
{
  "name": "smart-planning-v1",
  "private": true,
  "type": "module",
  "scripts": {
    "dev": "vite --host 0.0.0.0",
    "build": "tsc -b && vite build",
    "preview": "vite preview",
    "test": "vitest run",
    "test:watch": "vitest"
  }
}
```

Keep existing Angular dependencies in `dependencies` / `devDependencies` until Task 9 (they are unused by Vite).

- [ ] **Step 3: Add Vite config**

`travelfinder-web/vite.config.ts`:

```ts
import { defineConfig } from 'vite';
import react from '@vitejs/plugin-react';

export default defineConfig({
  plugins: [react()],
  server: { port: 8100 },
  test: {
    environment: 'jsdom',
    globals: true,
    include: ['src/**/*.test.ts', 'src/**/*.test.tsx'],
  },
});
```

- [ ] **Step 4: Global CSS import order**

`travelfinder-web/src/app.css`:

```css
@import '@astryxdesign/core/reset.css';
@import '@astryxdesign/core/astryx.css';
@import '@astryxdesign/theme-neutral/theme.css';

body {
  margin: 0;
  font-family: var(--font-family-body);
}
```

- [ ] **Step 5: Theme providers**

`travelfinder-web/src/app/Providers.tsx`:

```tsx
import { type ReactNode } from 'react';
import { Link } from 'react-router-dom';
import { Theme } from '@astryxdesign/core/theme';
import { LinkProvider } from '@astryxdesign/core/Link';
import { neutralTheme } from '@astryxdesign/theme-neutral/built';

export function Providers({ children }: { children: ReactNode }) {
  return (
    <Theme theme={neutralTheme}>
      <LinkProvider component={Link}>{children}</LinkProvider>
    </Theme>
  );
}
```

- [ ] **Step 6: Router shell and stub pages**

`travelfinder-web/src/App.tsx`:

```tsx
import { BrowserRouter, Navigate, Route, Routes } from 'react-router-dom';
import { AppShell, TopNav, Heading } from '@astryxdesign/core';
import { Providers } from './app/Providers';
import { HomePage } from './pages/HomePage';
import { PlanPage } from './pages/PlanPage';
import { DetailPage } from './pages/DetailPage';

export function App() {
  return (
    <Providers>
      <BrowserRouter>
        <AppShell header={<TopNav><Heading size="sm">Travelfinder</Heading></TopNav>}>
          <Routes>
            <Route path="/" element={<Navigate to="/home" replace />} />
            <Route path="/home" element={<HomePage />} />
            <Route path="/plan" element={<PlanPage />} />
            <Route path="/detail" element={<DetailPage />} />
          </Routes>
        </AppShell>
      </BrowserRouter>
    </Providers>
  );
}
```

Each stub page exports a one-line placeholder (`export function HomePage() { return <p>Home</p>; }`).

`travelfinder-web/src/main.tsx`:

```tsx
import { StrictMode } from 'react';
import { createRoot } from 'react-dom/client';
import './app.css';
import { App } from './App';

createRoot(document.getElementById('root')!).render(
  <StrictMode>
    <App />
  </StrictMode>,
);
```

`travelfinder-web/index.html`:

```html
<!doctype html>
<html lang="en">
  <head>
    <meta charset="UTF-8" />
    <meta name="viewport" content="width=device-width, initial-scale=1.0" />
    <title>Travelfinder</title>
  </head>
  <body>
    <div id="root"></div>
    <script type="module" src="/src/main.tsx"></script>
  </body>
</html>
```

- [ ] **Step 7: Verify dev server and build**

Run:

```bash
cd travelfinder-web && npm run build
```

Expected: PASS — `dist/index.html` exists.

Run:

```bash
npm run dev
```

Manually open `/home`, `/plan`, `/detail`.

Expected: PASS — stub text renders; no `ion-` / `ui5-` in DOM.

- [ ] **Step 8: Commit**

```bash
git add travelfinder-web/package.json travelfinder-web/package-lock.json \
  travelfinder-web/index.html travelfinder-web/vite.config.ts \
  travelfinder-web/tsconfig.json travelfinder-web/tsconfig.node.json \
  travelfinder-web/src travelfinder-web/.env.example
git commit -m "feat(web): scaffold Vite React 19 Astryx SPA"
```

---

### Task 2: Port domain models, sse-parser, and PlanClient

**Files:**
- Create: `src/core/domain/models.ts`
- Create: `src/core/api/sse-parser.ts`, `src/core/api/sse-parser.test.ts`
- Create: `src/core/api/plan-client.ts`, `src/core/api/plan-client.test.ts`
- Create: `src/config/environment.ts`

**Interfaces:**
- Consumes: Task 1 Vite test runner
- Produces: `PlanEvent`, `PlanRequest`, `parseSseFrames(chunk, carry?)`, `PlanClient.stream(request, signal?)`

- [ ] **Step 1: Write failing sse-parser test**

`src/core/api/sse-parser.test.ts` (port expectations from `sse-parser.spec.ts`):

```ts
import { describe, expect, it } from 'vitest';
import { parseSseFrames } from './sse-parser';

describe('parseSseFrames', () => {
  it('parses named events into the union', () => {
    const frames = parseSseFrames(
      'event: plan_spec\ndata: {"language":"en-us","areaLabel":"Singapore","radiusMeters":5000,"categories":["park"],"pointOfInterests":[],"budgetLevel":"moderate","dayCount":1,"notes":""}\n\n' +
        'event: places\ndata: {"places":[]}\n\n',
    );
    expect(frames[0]).toMatchObject({ type: 'plan_spec' });
    expect(frames[1]).toMatchObject({ type: 'places', places: [] });
  });

  it('parses all six event types', () => {
    const chunk =
      'event: clarification\ndata: {"message":"How many days?"}\n\n' +
      'event: plan_delta\ndata: {"dayIndex":0,"stopIndex":0,"placeId":"g:1","name":"Park"}\n\n' +
      'event: error\ndata: {"code":"timeout","message":"slow","requestId":"r1","retryable":true}\n\n' +
      'event: done\ndata: {"providerUsed":"azure"}\n\n';
    const types = parseSseFrames(chunk).map(e => e.type);
    expect(types).toEqual(['clarification', 'plan_delta', 'error', 'done']);
  });

  it('turns invalid json into error', () => {
    const frames = parseSseFrames('event: places\ndata: {not-json\n\n');
    expect(frames[0].type).toBe('error');
    if (frames[0].type === 'error') {
      expect(frames[0].error.code).toBe('internal');
    }
  });
});
```

- [ ] **Step 2: Run test — expect FAIL**

```bash
cd travelfinder-web && npm test -- src/core/api/sse-parser.test.ts
```

Expected: FAIL — cannot resolve `./sse-parser`.

- [ ] **Step 3: Copy domain models**

Copy verbatim from `src/app/core/domain/models.ts` into `src/core/domain/models.ts` (same `PlanEvent` union).

- [ ] **Step 4: Implement sse-parser**

Copy logic from `src/app/core/api/sse-parser.ts` into `src/core/api/sse-parser.ts` (export `parseSseFrames` only).

- [ ] **Step 5: Run sse-parser tests**

```bash
npm test -- src/core/api/sse-parser.test.ts
```

Expected: PASS

- [ ] **Step 6: Write failing PlanClient tests**

`src/config/environment.ts`:

```ts
export const environment = {
  API_URL: import.meta.env.VITE_API_URL ?? 'http://localhost:1294',
};
```

`src/core/api/plan-client.test.ts`:

```ts
import { afterEach, describe, expect, it, vi } from 'vitest';
import { PlanClient } from './plan-client';
import { environment } from '../../config/environment';

describe('PlanClient', () => {
  afterEach(() => vi.restoreAllMocks());

  it('posts only to /plan/stream and yields parsed events', async () => {
    const body =
      'event: clarification\ndata: {"message":"How many days?"}\n\n' +
      'event: done\ndata: {}\n\n';
    vi.spyOn(globalThis, 'fetch').mockResolvedValue(
      new Response(body, { headers: { 'Content-Type': 'text/event-stream' } }),
    );

    const client = new PlanClient();
    const events = [];
    for await (const event of client.stream({
      messages: [{ role: 'user', content: 'hi' }],
      latitude: 1.35,
      longitude: 103.82,
      requestId: 'r1',
    })) {
      events.push(event);
    }

    expect(events.map(e => e.type)).toEqual(['clarification', 'done']);
    const [url, init] = vi.mocked(fetch).mock.calls.at(-1)!;
    expect(url).toBe(`${environment.API_URL}/plan/stream`);
    expect(init?.method).toBe('POST');
    expect(JSON.parse(init?.body as string).systemId).toBeUndefined();
  });

  it('yields error on HTTP 400 with JSON body', async () => {
    vi.spyOn(globalThis, 'fetch').mockResolvedValue(
      new Response('{"title":"Bad Request"}', {
        status: 400,
        headers: { 'Content-Type': 'application/json' },
      }),
    );
    const client = new PlanClient();
    const events = [];
    for await (const event of client.stream({
      messages: [{ role: 'user', content: 'hi' }],
      latitude: 1.35,
      longitude: 103.82,
      requestId: 'r1',
    })) {
      events.push(event);
    }
    expect(events[0]?.type).toBe('error');
  });
});
```

- [ ] **Step 7: Implement PlanClient**

Port `src/app/core/api/plan-client.ts`; change import to `../../config/environment`.

- [ ] **Step 8: Run PlanClient tests**

```bash
npm test -- src/core/api/plan-client.test.ts
```

Expected: PASS

- [ ] **Step 9: Commit**

```bash
git add travelfinder-web/src/core travelfinder-web/src/config
git commit -m "feat(web): port PlanClient and SSE parser with Vitest"
```

---

### Task 3: Planning session store (React context + reducer)

**Files:**
- Create: `src/core/state/planning-session.tsx`, `src/core/state/planning-session.test.ts`

**Interfaces:**
- Consumes: `PlanClient`, `PlanEvent`, `PlanRequest` from Task 2
- Produces: `PlanningSessionProvider`, `usePlanningSession()`, `SessionPhase`, `PlanningSessionSnapshot`, methods `start`, `reset`, `apply`, `run`, `abort`

- [ ] **Step 1: Write failing store tests**

Port cases from `planning-session.store.spec.ts`:

```ts
import { describe, expect, it, vi } from 'vitest';
import { createPlanningSessionController } from './planning-session';
import type { PlanSpec } from '../domain/models';

const spec: PlanSpec = {
  language: 'en-us',
  areaLabel: 'Singapore',
  radiusMeters: 5000,
  categories: ['park'],
  pointOfInterests: [],
  budgetLevel: 'moderate',
  dayCount: 1,
  notes: '',
};

describe('planning session reducer', () => {
  it('merges plan_delta by dayIndex and stopIndex', () => {
    const ctrl = createPlanningSessionController();
    ctrl.start({ messages: [], latitude: 1, longitude: 2, requestId: 'r1' });
    ctrl.apply({ type: 'plan_delta', stop: { dayIndex: 0, stopIndex: 0, placeId: 'google:1', name: 'A', reason: 'old' } });
    ctrl.apply({ type: 'plan_delta', stop: { dayIndex: 0, stopIndex: 0, placeId: 'google:1', name: 'A', reason: 'new' } });
    ctrl.apply({ type: 'plan_delta', stop: { dayIndex: 0, stopIndex: 1, placeId: 'google:2', name: 'B' } });
    expect(ctrl.snapshot().stops).toHaveLength(2);
    expect(ctrl.snapshot().stops[0].reason).toBe('new');
  });

  it('error keeps places; start clears them', () => {
    const ctrl = createPlanningSessionController();
    ctrl.start({ messages: [], latitude: 1, longitude: 2, requestId: 'r1' });
    ctrl.apply({ type: 'plan_spec', spec });
    ctrl.apply({
      type: 'places',
      places: [{
        id: 'google:1', source: 'google', sourceId: '1', name: 'A',
        categories: [], location: { latitude: 1, longitude: 2 }, score: 0.5,
      }],
    });
    ctrl.apply({
      type: 'error',
      error: { code: 'timeout', message: 'timed out', requestId: 'r1', retryable: true },
    });
    expect(ctrl.snapshot().phase).toBe('error');
    expect(ctrl.snapshot().places).toHaveLength(1);
    ctrl.start({ messages: [], latitude: 1, longitude: 2, requestId: 'r2' });
    expect(ctrl.snapshot().places).toHaveLength(0);
    expect(ctrl.snapshot().phase).toBe('planning');
  });
});
```

Export a testable `createPlanningSessionController()` that mirrors Angular `PlanningSessionStore` logic without React in unit tests.

- [ ] **Step 2: Run tests — expect FAIL**

```bash
npm test -- src/core/state/planning-session.test.ts
```

Expected: FAIL — module missing.

- [ ] **Step 3: Implement controller + provider**

Port phase transitions from `planning-session.store.ts`:

- `clarification` → phase `clarifying`
- `plan_spec` → `retrieving`
- `plan_delta` → `rendering`; key `${dayIndex}:${stopIndex}`
- `places` → merge by `id` (update in place or append)
- `error` → `error`; keep places
- `done` → `done` unless phase is `error` or `clarifying`
- `run()` increments `generation`; stale stream events ignored

Wrap controller in `PlanningSessionProvider` + `usePlanningSession()` for pages.

- [ ] **Step 4: Run tests**

```bash
npm test -- src/core/state/planning-session.test.ts
```

Expected: PASS

- [ ] **Step 5: Wire provider in `App.tsx`**

```tsx
import { PlanningSessionProvider } from './core/state/planning-session';
// inside Providers, wrap BrowserRouter children:
<PlanningSessionProvider>{/* routes */}</PlanningSessionProvider>
```

- [ ] **Step 6: Commit**

```bash
git add travelfinder-web/src/core/state travelfinder-web/src/App.tsx
git commit -m "feat(web): add planning session store with Vitest"
```

---

### Task 4: Home page (geolocation → plan)

**Files:**
- Create: `src/pages/HomePage.tsx`, `src/pages/HomePage.test.tsx`
- Modify: `src/pages/HomePage.tsx` (replace stub)

**Interfaces:**
- Consumes: React Router `useNavigate`, Astryx `Button`, `TextArea`, `Stack`
- Produces: `sessionStorage` key `tf.pending` JSON `{ requestId, prompt, latitude, longitude }`

- [ ] **Step 1: Write failing DOM test**

```tsx
import { render, screen } from '@testing-library/react';
import { MemoryRouter } from 'react-router-dom';
import { describe, expect, it } from 'vitest';
import { HomePage } from './HomePage';
import { Providers } from '../app/Providers';

describe('HomePage', () => {
  it('renders prompt and start without ion/ui5', () => {
    const { container } = render(
      <Providers>
        <MemoryRouter>
          <HomePage />
        </MemoryRouter>
      </Providers>,
    );
    expect(screen.getByRole('textbox')).toBeTruthy();
    expect(screen.getByRole('button', { name: /start planning/i })).toBeTruthy();
    expect(container.querySelector('ion-content')).toBeNull();
    expect(container.querySelector('ui5-button')).toBeNull();
  });
});
```

- [ ] **Step 2: Run test — expect FAIL**

```bash
npm test -- src/pages/HomePage.test.tsx
```

- [ ] **Step 3: Implement HomePage**

Mirror `home.page.ts`:

- State: `prompt`, `geoStatus`
- `startPlanning()` → `crypto.randomUUID()`, geolocation with fallback (`needsMapPick` stored indirectly via `latitude`/`longitude` null)
- `sessionStorage.setItem('tf.pending', JSON.stringify({ requestId, prompt, latitude, longitude }))`
- `navigate('/plan')`

Use Astryx `TextArea` + `Button` instead of Tailwind-only primitives.

- [ ] **Step 4: Run test**

```bash
npm test -- src/pages/HomePage.test.tsx
```

Expected: PASS

- [ ] **Step 5: Commit**

```bash
git add travelfinder-web/src/pages/HomePage.tsx travelfinder-web/src/pages/HomePage.test.tsx
git commit -m "feat(web): React home page with geolocation handoff"
```

---

### Task 5: Chat feature + plan page shell

**Files:**
- Create: `src/features/chat/ChatPanel.tsx`
- Modify: `src/pages/PlanPage.tsx`

**Interfaces:**
- Consumes: `usePlanningSession()`, `PlanClient`, `ChatMessageDto`
- Produces: `ChatPanel` props: `messages`, `phase`, `clarification`, `errorMessage`, `onSend(text)`

- [ ] **Step 1: Implement ChatPanel**

Port template from `chat-panel.component.ts` using Astryx `Stack`, `TextArea`, `Button`, status `Text`.

- [ ] **Step 2: Plan page shell**

`PlanPage.tsx` behavior from `plan.page.ts`:

- Read `tf.pending` from `sessionStorage` on mount
- Initialize `messages` with user prompt
- Set `needsMapPick` when lat/lng null
- Subscribe to session snapshot; append assistant message on new `clarification`
- `onSend` → append user message → `session.run(planClient, request)` when coordinates valid
- `onMapPick` → set lat/lng → `runPlanning()`
- Render `ChatPanel` only (map/itinerary in Task 6–7)

- [ ] **Step 3: Smoke test**

```bash
npm run dev
```

From `/home`, enter text, start planning → `/plan` shows messages and phase label.

Expected: PASS (API optional; phase moves to `planning` when stream starts).

- [ ] **Step 4: Commit**

```bash
git add travelfinder-web/src/features/chat travelfinder-web/src/pages/PlanPage.tsx
git commit -m "feat(web): chat panel and plan page shell"
```

---

### Task 6: Map feature (@arcgis/core)

**Files:**
- Create: `src/features/map/place-graphics.ts`, `map-view-ready-gate.ts`, `TravelMap.tsx`, `travel-map.css`
- Modify: `src/pages/PlanPage.tsx`

**Interfaces:**
- Consumes: `Place`, `GeoPoint` from `models.ts`
- Produces: `TravelMap` props: `places`, `selectedId`, `stopOrder`, `needsMapPick`, `onPlaceSelect`, `onMapPick`, `onReady`

- [ ] **Step 1: Copy pure helpers**

Copy `place-graphics.ts` and `map-view-ready-gate.ts` from Angular `features/map/` (paths unchanged).

- [ ] **Step 2: Implement TravelMap**

Port `map.component.ts` to a `useEffect` + `useRef` React component:

- `WebMap` basemap `topo-vector`, graphics layers `places` + `route`
- Incremental `syncPlaces` via `mergePlaceGraphics`
- Route polyline from `stopOrder`
- Selection symbol toggle
- `needsMapPick` click → `webMercatorToGeographic` → `onMapPick`
- Otherwise `hitTest` → `onPlaceSelect`

Register ArcGIS API key in `main.tsx` or map module:

```ts
import esriConfig from '@arcgis/core/config';
esriConfig.apiKey = import.meta.env.VITE_ARCGIS_API_KEY ?? '';
```

Document key in `.env.example` (no committed secrets).

- [ ] **Step 3: Mount map on PlanPage**

Pass `state.places`, `selectedId`, `stopIds(state.stops)`, `needsMapPick`, handlers.

- [ ] **Step 4: Manual smoke**

With API running, stream `places` events → markers appear; second `places` does not drop first markers.

- [ ] **Step 5: Commit**

```bash
git add travelfinder-web/src/features/map travelfinder-web/src/pages/PlanPage.tsx travelfinder-web/.env.example
git commit -m "feat(web): ArcGIS TravelMap with incremental places"
```

---

### Task 7: Itinerary feature + plan workspace layout

**Files:**
- Create: `src/features/plan/ItineraryCards.tsx`, `SpecSummary.tsx`, `src/pages/plan-workspace.css`
- Modify: `src/pages/PlanPage.tsx`

**Interfaces:**
- Consumes: `ItineraryStop`, `PlanSpec`
- Produces: `ItineraryCards({ stops, onSelect })`, responsive workspace layout

- [ ] **Step 1: Port ItineraryCards**

Port grouping logic from `itinerary-cards.component.ts`; use Astryx `Card` / `Button` for stop rows.

- [ ] **Step 2: SpecSummary (optional row)**

When `state.spec` is set, show `areaLabel`, `dayCount`, `budgetLevel` (matches detail header fields).

- [ ] **Step 3: Workspace layout CSS**

`plan-workspace.css`:

```css
.plan-workspace {
  display: grid;
  min-height: calc(100vh - 3.5rem);
  max-width: 72rem;
  margin: 0 auto;
  grid-template-columns: 1fr;
}
@media (min-width: 1024px) {
  .plan-workspace {
    grid-template-columns: 1fr 1fr;
  }
  .plan-workspace__aside {
    border-right: 1px solid var(--color-border);
  }
}
.plan-workspace__map {
  min-height: 20rem;
}
```

Mirror Angular `plan.page.html`: aside = chat + itinerary; second column = map.

- [ ] **Step 4: Wire selection**

`onStopSelect` / `onPlaceSelect` update `selectedId` (shared state on `PlanPage`).

- [ ] **Step 5: Commit**

```bash
git add travelfinder-web/src/features/plan travelfinder-web/src/pages
git commit -m "feat(web): itinerary cards and responsive plan workspace"
```

---

### Task 8: Detail page

**Files:**
- Modify: `src/pages/DetailPage.tsx`, create `src/pages/DetailPage.test.tsx`

**Interfaces:**
- Consumes: `usePlanningSession()`, React Router `Link`
- Produces: read-only grouped stops + spec header

- [ ] **Step 1: Write test for grouped stops helper**

Extract `groupedStops(stops: ItineraryStop[])` (same logic as Angular `detail.page.ts`) and unit-test sort order.

- [ ] **Step 2: Implement DetailPage**

Port `detail.page.html`: back link to `/plan`, spec header, per-day lists (no edit controls).

- [ ] **Step 3: Add navigation from plan**

Link in `PlanPage` or `AppShell` to `/detail` when `state.stops.length > 0`.

- [ ] **Step 4: Run tests**

```bash
npm test -- src/pages/DetailPage.test.tsx
```

Expected: PASS

- [ ] **Step 5: Commit**

```bash
git add travelfinder-web/src/pages/DetailPage.tsx travelfinder-web/src/pages/DetailPage.test.tsx
git commit -m "feat(web): read-only detail itinerary page"
```

---

### Task 9: Purge Angular, Ionic, UI5, Capacitor

**Files:**
- Delete: `src/app/**`, `angular.json`, `karma.conf.js`, old `src/main.ts`, Ionic/Tailwind Angular-only configs not used by Vite
- Modify: `package.json` — remove `@angular/*`, `@ionic/*`, `@ui5/*`, `@capacitor/*`, Karma/Jasmine/Angular-eslint

**Interfaces:**
- Consumes: Tasks 4–8 acceptance complete
- Produces: React-only `travelfinder-web`

- [ ] **Step 1: Remove Angular source tree**

```bash
cd travelfinder-web
rm -rf src/app angular.json karma.conf.js
rm -f src/main.ts src/polyfills.ts src/test.ts
```

- [ ] **Step 2: Uninstall legacy packages**

```bash
npm uninstall @angular/animations @angular/common @angular/compiler @angular/core \
  @angular/forms @angular/platform-browser @angular/platform-browser-dynamic @angular/router \
  @angular-devkit/build-angular @angular/cli @angular/compiler-cli @angular/language-service \
  @angular-eslint/builder @angular-eslint/eslint-plugin @angular-eslint/eslint-plugin-template \
  @angular-eslint/schematics @angular-eslint/template-parser \
  @ionic/angular @ionic/angular-toolkit ionicons \
  @ui5/webcomponents-icons @ui5/webcomponents-ngx \
  @capacitor/app @capacitor/core @capacitor/geolocation @capacitor/haptics \
  @capacitor/keyboard @capacitor/status-bar @capacitor/cli \
  rxjs zone.js karma jasmine-core jasmine-spec-reporter \
  karma-chrome-launcher karma-coverage karma-jasmine karma-jasmine-html-reporter @types/jasmine
```

- [ ] **Step 3: Verify build and tests**

```bash
npm run build && npm test
```

Expected: PASS

- [ ] **Step 4: Grep guard**

```bash
rg -l '@angular/|@ionic/|ui5-' travelfinder-web/src travelfinder-web/package.json || true
```

Expected: no matches under `src/`.

- [ ] **Step 5: Commit**

```bash
git add -A travelfinder-web
git commit -m "chore(web): remove Angular Ionic UI5 Capacitor stack"
```

---

### Task 10: Manual acceptance checklist (375px + 1280px)

No code changes unless fixing defects found during the gate. Record results in the PR description before merge.

**Prerequisites:** `travelfinder-api` running locally; `VITE_API_URL` points at API; valid `VITE_ARCGIS_API_KEY` in local `.env` (not committed).

- [ ] **Step 1: Home → plan flow**

1. Open `http://localhost:8100/home` at **375px** width.
2. Enter trip prompt; click **Start planning**.
3. Allow geolocation or verify map-pick path sets coordinates.

Expected: PASS — lands on `/plan` with user message visible; no `ion-` / `ui5-` in DOM.

- [ ] **Step 2: Stream planning at mobile width**

1. Stay at **375px** on `/plan`.
2. Send planning message (or auto-run from home).
3. Wait for `places` then `plan_delta` events.

Expected: PASS — map markers appear; itinerary cards populate; chat shows clarification if server asks.

- [ ] **Step 3: Desktop layout at 1280px**

1. Resize to **1280px**.
2. Confirm side-by-side layout: chat + itinerary left, map right.

Expected: PASS — matches spec §4.7 workspace behavior.

- [ ] **Step 4: Map interaction**

1. Pan and zoom map.
2. Click a place marker → selection highlights.
3. If server sends a second `places` event, prior markers remain.

Expected: PASS — incremental graphics (no full wipe).

- [ ] **Step 5: Detail view**

1. Navigate to `/detail`.
2. Verify grouped days and back link to `/plan`.

Expected: PASS — read-only content from session store.

- [ ] **Step 6: Network contract audit**

1. DevTools → Network filter `stream`.
2. Confirm only `POST /plan/stream`; no `/Chat/*`.

Expected: PASS

- [ ] **Step 7: Sign-off**

Document pass/fail for each step in the migration PR. No commit unless a checklist artifact is required by team policy.

---

## Self-review (plan author)

**Spec coverage**

| Spec requirement | Task |
|---|---|
| Vite + React 19 + React Router + Astryx | 1 |
| CSS import order reset → astryx → theme-neutral | 1 |
| Six SSE events, PlanClient only `/plan/stream` | 2 |
| Session phases + `plan_delta` merge | 3 |
| Home geolocation + `tf.pending` | 4 |
| Plan workspace chat | 5, 7 |
| Map `@arcgis/core` incremental places | 6 |
| Detail read-only | 8 |
| Purge Angular/Ionic/UI5 | 9 |
| Manual 375 + 1280 gate | 10 |

**Placeholder scan:** No TBD/TODO in task steps.

**Type consistency:** `PlanEvent`, `PlanRequest`, `SessionPhase`, and `PlanningSessionSnapshot` match ported `models.ts` and Angular store behavior.

---

## Execution Handoff

Plan complete and saved to `docs/superpowers/plans/2026-11-09-react-astryx-frontend.md`. Two execution options:

**1. Subagent-Driven (recommended)** — dispatch a fresh subagent per task, review between tasks, fast iteration

**2. Inline Execution** — execute tasks in one session using superpowers:executing-plans with checkpoints

Which approach?
