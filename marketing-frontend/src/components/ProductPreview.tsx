import { useI18n } from "../i18n";
export function ProductPreview() {
  const { t } = useI18n();
  return <div className="phone-preview" aria-label={t("Illustration of a live Park Pilot itinerary")}>
    <div className="phone-top"><span>10:42</span><span className="signal">● ● ●</span></div>
    <div className="preview-heading"><div><small>{t("YOUR NEXT MOVE")}</small><h3>{t("River Rapids")}</h3></div><span className="walk-chip">{t("8 min walk")}</span></div>
    <div className="reason-card"><span className="reason-icon" aria-hidden="true">↗</span><div><strong>{t("Go now and save about 25 min")}</strong><p>{t("The queue is 18 min and likely to rise after 11:15.")}</p></div></div>
    <ol className="timeline">
      <li className="done"><span className="timeline-dot">✓</span><div><small>{t("COMPLETED · 9:35")}</small><strong>{t("Skyline Gliders")}</strong></div></li>
      <li className="current"><span className="timeline-dot">2</span><div><small>{t("NEXT · 10:50")}</small><strong>{t("River Rapids")}</strong><em>{t("18 min wait")}</em></div></li>
      <li><span className="timeline-dot">3</span><div><small>12:05</small><strong>{t("Trailside lunch")}</strong><em>{t("Break · 40 min")}</em></div></li>
      <li><span className="timeline-dot">4</span><div><small>1:10</small><strong>{t("Summit Coaster")}</strong><em>{t("Predicted 32 min")}</em></div></li>
    </ol>
    <div className="replan-toast"><span className="pulse" aria-hidden="true" /> <div><strong>{t("Plan updated")}</strong><small>{t("14 minutes saved")}</small></div></div>
  </div>;
}
