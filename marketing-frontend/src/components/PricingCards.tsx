import { useI18n } from "../i18n";
import { plans } from "../lib/plans";
import { Button } from "./Button";

export function PricingCards({ concise = false }: { concise?: boolean }) {
  const { t, formatCurrency } = useI18n();
  return <div className="pricing-grid">{plans.map((plan) => <article className={`price-card ${plan.featured ? "price-card--featured" : ""}`} key={plan.code}>
    {plan.featured && <span className="popular-label">{t("Best for one day")}</span>}
    <h3>{t(plan.name)}</h3><p>{t(plan.description)}</p>
    <div className="price"><strong>{formatCurrency(plan.amount)}</strong><span>{t(plan.cadence)}</span></div>
    {!concise && <ul className="check-list">{plan.features.map((feature) => <li key={feature}>{t(feature)}</li>)}</ul>}
    {plan.code === "free" ? <Button to="/#waitlist" variant="secondary">{t("Join free")}</Button> : <Button to={`/checkout?plan=${plan.code}`}>{t(`Choose ${plan.name}`)}</Button>}
  </article>)}</div>;
}