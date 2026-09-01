import { plans } from "../lib/plans";
import { Button } from "./Button";

export function PricingCards({ concise = false }: { concise?: boolean }) {
  return <div className="pricing-grid">{plans.map((plan) => <article className={`price-card ${plan.featured ? "price-card--featured" : ""}`} key={plan.code}>
    {plan.featured && <span className="popular-label">Best for one day</span>}
    <h3>{plan.name}</h3><p>{plan.description}</p>
    <div className="price"><strong>{plan.price}</strong><span>{plan.cadence}</span></div>
    {!concise && <ul className="check-list">{plan.features.map((feature) => <li key={feature}>{feature}</li>)}</ul>}
    {plan.code === "free" ? <Button to="/#waitlist" variant="secondary">Join free</Button> : <Button to={`/checkout?plan=${plan.code}`}>Choose {plan.name}</Button>}
  </article>)}</div>;
}
