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
    </Providers>
  );
}
