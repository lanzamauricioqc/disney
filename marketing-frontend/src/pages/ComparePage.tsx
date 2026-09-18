import { Button } from "../components/Button";
import { PageMeta } from "../components/PageMeta";
import { useI18n } from "../i18n";

const rows: [string, string, string, string][] = [
  ["Attraction information", "Included", "Included", "Included"],
  ["Current queue times", "Included", "Included", "Included"],
  ["Optimized itinerary", "—", "1 park day", "Multiple park days"],
  ["Live replanning", "—", "Included", "Included"],
  ["Predicted queue times", "—", "Included", "Included"],
  ["Safe, Balanced & Aggressive modes", "—", "Included", "Included"],
  ["Completion probability", "—", "Included", "Included"],
  ["Recommendation explanations", "—", "Included", "Included"],
  ["AI Guide", "—", "Included", "Included"],
  ["Cross-day priority planning", "—", "—", "Included"],
];

export function ComparePage() {
  const { t, formatCurrency } = useI18n();
  return <>
    <PageMeta title="Compare plans" description="Compare Park Pilot Free, Visit Pass, and Trip Pass features in one accessible table." />
    <section className="page-hero"><div className="container narrow"><span className="eyebrow">{t("PLAN COMPARISON")}</span><h1>{t("Choose how far ahead to pilot.")}</h1><p>{t("Browse for free, optimize one big day, or keep an entire park trip moving.")}</p></div></section>
    <section className="section compare-section"><div className="container"><div className="comparison-wrap" tabIndex={0} role="region" aria-label={t("Scrollable plan comparison")}>
      <table className="comparison-table"><caption>{t("Features included in each Park Pilot plan. Prices are introductory drafts.")}</caption><thead><tr><th scope="col">{t("Feature")}</th><th scope="col"><strong>{t("Free")}</strong><span>{formatCurrency(0)}</span></th><th scope="col" className="recommended"><small>{t("RECOMMENDED")}</small><strong>{t("Visit Pass")}</strong><span>{formatCurrency(8)} / {t("per park day")}</span></th><th scope="col"><strong>{t("Trip Pass")}</strong><span>{formatCurrency(20)} / {t("per trip")}</span></th></tr></thead><tbody>{rows.map((row) => <tr key={row[0]}><th scope="row">{t(row[0])}</th>{row.slice(1).map((cell, index) => <td key={index} className={cell === "Included" ? "included" : undefined}>{cell === "Included" ? <><span aria-hidden="true">✓</span><span className="sr-only">{t("Included")}</span></> : t(cell)}</td>)}</tr>)}</tbody><tfoot><tr><th scope="row">{t("Choose a plan")}</th><td><Button to="/#waitlist" variant="secondary">{t("Join free")}</Button></td><td><Button to="/checkout?plan=visit-pass">{t("Choose Visit")}</Button></td><td><Button to="/checkout?plan=trip-pass" variant="secondary">{t("Choose Trip")}</Button></td></tr></tfoot></table>
    </div><p className="compare-note">{t("Draft prices and plan details are subject to change before general availability.")}</p></div></section>
  </>;
}
