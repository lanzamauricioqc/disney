import { Button } from "../components/Button";
import { PageMeta } from "../components/PageMeta";
import { useI18n } from "../i18n";

export function NotFoundPage() {
  const { t } = useI18n();
  return <section className="not-found"><PageMeta title="Page not found" description="The requested Park Pilot page could not be found." noIndex /><div className="container narrow"><span className="compass" aria-hidden="true">↗</span><span className="kicker">{t("404 · OFF ROUTE")}</span><h1>{t("This stop is not on the plan.")}</h1><p>{t("The page may have moved, but your next best action is easy.")}</p><Button to="/">{t("Return home")}</Button></div></section>;
}
