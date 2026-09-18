import { NavLink } from "react-router-dom";
import { useI18n, type Locale } from "../i18n";
import { Button } from "./Button";

const links = [{ to: "/product", label: "Product" }, { to: "/pricing", label: "Pricing" }, { to: "/compare", label: "Compare" }, { to: "/companies", label: "Companies" }];
const languageNames: Record<Locale, string> = { en: "English", "pt-BR": "Português (Brasil)", es: "Español" };

export function Header() {
  const { locale, setLocale, t } = useI18n();
  return <header className="site-header">
    <div className="container header-inner">
      <NavLink className="brand" to="/" aria-label={t("Park Pilot home")}>
        <span className="brand-mark" aria-hidden="true"><span /></span><span>Park Pilot</span>
      </NavLink>
      <nav className="main-nav" aria-label={t("Main navigation")}>{links.map((link) =>
        <NavLink key={link.to} to={link.to} className={({ isActive }) => isActive ? "active" : undefined}>{t(link.label)}</NavLink>
      )}</nav>
      <div className="language-picker"><label htmlFor="site-language">{t("Language")}</label><select id="site-language" value={locale} onChange={(event) => setLocale(event.target.value as Locale)}>{Object.entries(languageNames).map(([value, label]) => <option key={value} value={value}>{label}</option>)}</select></div>
      <Button to="/pricing" className="header-cta">{t("Get a pass")}</Button>
    </div>
  </header>;
}