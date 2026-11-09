import { BrowserRouter, Navigate, Route, Routes } from 'react-router-dom';
import { AppShell, TopNav, Heading } from '@astryxdesign/core';
import { Providers } from './providers/Providers';
import { HomePage } from './pages/HomePage';
import { PlanPage } from './pages/PlanPage';
import { DetailPage } from './pages/DetailPage';
import { PlanningSessionProvider } from './core/state/planning-session';

export function App() {
  return (
    <Providers>
      <PlanningSessionProvider>
        <BrowserRouter>
          <AppShell
            topNav={
              <TopNav heading={<Heading level={4}>Travelfinder</Heading>} />
            }
          >
            <Routes>
              <Route path="/" element={<Navigate to="/home" replace />} />
              <Route path="/home" element={<HomePage />} />
              <Route path="/plan" element={<PlanPage />} />
              <Route path="/detail" element={<DetailPage />} />
            </Routes>
          </AppShell>
        </BrowserRouter>
      </PlanningSessionProvider>
    </Providers>
  );
}
