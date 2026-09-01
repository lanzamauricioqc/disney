import { useState } from "react";
import { NavLink, Outlet } from "react-router-dom";
import { useAuth, userLabel } from "../AuthContext";

const navigation = [
  ["/", "⌂", "Overview", "member"],
  ["/customers", "◎", "Customers", "member"],
  ["/visits", "↗", "Visits", "member"],
  ["/team", "♙", "Team", "administrator"],
  ["/branding", "◇", "Branding", "administrator"],
  ["/billing", "◫", "Credits & billing", "administrator"],
  ["/reports", "▥", "Reports", "member"],
  ["/integrations", "⌁", "Integrations", "administrator"],
  ["/settings", "⚙", "Settings", "administrator"],
];

export function AppShell() {
  const { session, logout } = useAuth(); const [open, setOpen] = useState(false); const user = session?.user;
  const canAdminister = ["Owner", "Administrator"].includes(user?.role ?? "");
  const visibleNavigation = navigation.filter(([, , , permission]) =>
    permission === "member" || canAdminister);
  return <div className="portal-shell">
    <aside className={`sidebar ${open ? "sidebar--open" : ""}`}><div className="sidebar-brand"><span className="brand-mark"><i /></span><div><strong>Park Pilot</strong><small>COMPANY</small></div><button onClick={() => setOpen(false)} aria-label="Close navigation">×</button></div><div className="org-card"><span>{(user?.organizationName || "O").charAt(0)}</span><div><small>ORGANIZATION</small><strong>{user?.organizationName || "Company workspace"}</strong></div></div>    <nav aria-label="Portal navigation">{visibleNavigation.map(([to, icon, label]) => <NavLink key={to} to={to} end={to === "/"} onClick={() => setOpen(false)}><span aria-hidden="true">{icon}</span>{label}</NavLink>)}</nav><div className="sidebar-foot"><div className="user-avatar">{userLabel(user).charAt(0).toUpperCase()}</div><div><strong>{userLabel(user)}</strong><small>{user?.role || "Staff"}</small></div><button onClick={logout} aria-label="Log out" title="Log out">↪</button></div></aside>
    <div className="portal-main"><header className="mobile-header"><button onClick={() => setOpen(true)} aria-label="Open navigation">☰</button><strong>Park Pilot <span>Company</span></strong><div className="user-avatar">{userLabel(user).charAt(0).toUpperCase()}</div></header><main id="main-content"><Outlet /></main></div>
    {open && <button className="nav-scrim" onClick={() => setOpen(false)} aria-label="Close navigation overlay" />}
  </div>;
}
