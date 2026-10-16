import { useState } from 'react';
import { LeadsPage } from '../features/leads';
import { SettingsPage } from '../features/settings';

// Two pages, switched with useState: not enough for a router.
type Page = 'leads' | 'settings';

export default function App() {
  const [page, setPage] = useState<Page>('leads');

  const navItem = (target: Page, label: string) => (
    <button
      className={`nav-item${page === target ? ' active' : ''}`}
      aria-current={page === target ? 'page' : undefined}
      onClick={() => setPage(target)}
    >
      {label}
    </button>
  );

  return (
    <div className="layout">
      <aside className="sidebar">
        <div className="logo">Mini CRM</div>
        <nav>
          {navItem('leads', 'Leads')}
          {navItem('settings', 'Tracker settings')}
        </nav>
      </aside>

      {page === 'leads' ? <LeadsPage /> : <SettingsPage />}
    </div>
  );
}
