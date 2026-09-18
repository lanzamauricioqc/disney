import { Link } from "react-router-dom";
import { useI18n } from "../i18n";

export function Footer() {
  const { t } = useI18n();
  return <footer className="site-footer">
    <div className="container footer-grid">
      <div><Link className="brand brand--footer" to="/"><span className="brand-mark" aria-hidden="true"><span /></span><span>{t("Park Pilot")}</span></Link><p>{t("More park. Less guesswork.")}</p></div>
      <nav aria-label={t("Footer navigation")}><h2>{t("Explore")}</h2><Link to="/product">{t("Product")}</Link><Link to="/pricing">{t("Pricing")}</Link><Link to="/compare">{t("Compare plans")}</Link><Link to="/companies">{t("For companies")}</Link></nav>
      <div><h2>{t("Built for the day")}</h2><p>{t("Real-time decisions, clearly explained. No affiliation with any theme park operator.")}</p></div>
    </div>
    <div className="container footer-bottom"><span>© {new Date().getFullYear()} Park Pilot</span><span>{t("Independent park-day planning technology.")}</span></div>
  </footer>;
}
