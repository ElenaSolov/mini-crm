import { LeadsPage } from '../features/leads';

export default function App() {
  return (
    <div className="layout">
      <aside className="sidebar">
        <div className="logo">Mini CRM</div>
        <nav>
          <span className="nav-item active">Leads</span>
        </nav>
      </aside>

      <LeadsPage />
    </div>
  );
}
