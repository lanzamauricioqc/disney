import { NavLink } from "react-router-dom";
import { Button } from "./Button";

const links = [{ to: "/product", label: "Product" }, { to: "/pricing", label: "Pricing" }, { to: "/compare", label: "Compare" }, { to: "/companies", label: "Companies" }];

export function Header() {
  return <header className="site-header">
    <div className="container header-inner">
      <NavLink className="brand" to="/" aria-label="Park Pilot home">
        <span className="brand-mark" aria-hidden="true"><span /></span><span>Park Pilot</span>
      </NavLink>
      <nav className="main-nav" aria-label="Main navigation">{links.map((link) =>
        <NavLink key={link.to} to={link.to} className={({ isActive }) => isActive ? "active" : undefined}>{link.label}</NavLink>
      )}</nav>
      <Button to="/pricing" className="header-cta">Get a pass</Button>
    </div>
  </header>;
}
