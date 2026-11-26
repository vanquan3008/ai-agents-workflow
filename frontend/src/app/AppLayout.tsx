import { NavLink, Outlet } from 'react-router'
import { env } from '../shared/config/env'

export function AppLayout() {
  return (
    <div className="app">
      <a className="skip-link" href="#main">
        Bỏ qua tới nội dung
      </a>
      <header className="topbar">
        <span className="brand">{env.appName}</span>
        <nav aria-label="Điều hướng chính">
          <NavLink to="/" end>
            Task
          </NavLink>
          <NavLink to="/approvals">Phê duyệt</NavLink>
        </nav>
        {env.authMode === 'development' && <span className="badge badge--warning">Dev auth</span>}
      </header>
      <main id="main" className="container">
        <Outlet />
      </main>
    </div>
  )
}
