import { Button } from "../components/Button";
import { PageMeta } from "../components/PageMeta";
import { ProductPreview } from "../components/ProductPreview";
import { useI18n } from "../i18n";

const layers = [
  { n: "01", title: "Park intelligence", text: "A living model of attractions, operating hours, live and historical waits, predicted queues, closures, locations, durations, and walking time.", tag: "KNOW WHAT IS HAPPENING" },
  { n: "02", title: "Optimization engine", text: "A deterministic, testable engine weighs your priorities and constraints to build the strongest realistic sequence—not just the shortest queue.", tag: "DECIDE WHAT COMES NEXT" },
  { n: "03", title: "AI Guide", text: "A conversational layer understands natural-language requests, turns them into constraints, answers questions, and explains the engine recommendation.", tag: "MAKE EVERY CHOICE CLEAR" },
];

export function ProductPage() {
  const { t } = useI18n();
  return <>
    <PageMeta title="How Park Pilot works" description="Explore the park intelligence, deterministic optimization, AI Guide, and live replanning behind Park Pilot." />
    <section className="page-hero product-hero"><div className="container narrow"><span className="eyebrow">{t("THE PARK-DAY DECISION ENGINE")}</span><h1>{t("A great itinerary is alive.")}</h1><p>{t("Park Pilot combines real park conditions with what matters to you, producing a plan that stays practical from rope drop to the last ride.")}</p><div className="hero-actions"><Button to="/pricing">{t("View draft pricing")}</Button><Button to="/#waitlist" variant="secondary">{t("Join early access")}</Button></div></div></section>
    <section className="section"><div className="container"><div className="section-heading"><span className="kicker">{t("THREE LAYERS, ONE CLEAR ANSWER")}</span><h2>{t("Built to answer: “What should I do next?”")}</h2></div><div className="layer-grid">{layers.map((layer) => <article key={layer.n}><span className="layer-number">{layer.n}</span><small>{t(layer.tag)}</small><h3>{t(layer.title)}</h3><p>{t(layer.text)}</p></article>)}</div></div></section>
    <section className="section section--sand"><div className="container product-detail"><div><span className="kicker">{t("DECISIONS YOU CAN TRUST")}</span><h2>{t("The AI explains. The engine decides.")}</h2><p>{t("Generative AI does not invent your route. It translates requests such as “the kids need a 30-minute break” into clear constraints. A deterministic optimization engine then recalculates the itinerary.")}</p><blockquote>{t("“Take a break near the north trail. River Rapids is next because its 18-minute wait is expected to reach 40 minutes by noon. Your three must-dos remain achievable.”")}</blockquote><p className="caption">{t("Example recommendation explanation")}</p></div><ProductPreview /></div></section>
    <section className="section"><div className="container"><div className="section-heading"><span className="kicker">{t("CHOOSE YOUR STRATEGY")}</span><h2>{t("There is no single perfect park day.")}</h2><p>{t("Select the tradeoff that fits your group. Change it whenever the day changes.")}</p></div><div className="mode-grid">
      <article><span className="mode-gauge">75%</span><h3>{t("Safe")}</h3><p>{t("Prioritize the probability of completing every must-do, with more timing buffer and lower risk.")}</p><small>{t("BEST FOR: FIRST VISITS & FAMILIES")}</small></article>
      <article className="mode-featured"><span className="mode-gauge">↔</span><h3>{t("Balanced")}</h3><p>{t("Balance attraction count, queue time, walking distance, breaks, and your stated priorities.")}</p><small>{t("BEST FOR: MOST PARK DAYS")}</small></article>
      <article><span className="mode-gauge">+3</span><h3>{t("Aggressive")}</h3><p>{t("Attempt more attractions with tighter timing, more movement, and a higher chance of changes.")}</p><small>{t("BEST FOR: EXPERIENCED VISITORS")}</small></article>
    </div></div></section>
    <section className="section section--ink"><div className="container live-grid"><div><span className="kicker kicker--light">{t("CONTINUOUSLY RECALCULATED")}</span><h2>{t("Change one thing. Keep the whole day useful.")}</h2><p>{t("Park Pilot replans after completed or skipped attractions, unexpected closures, timing drift, queue movement, breaks, and changed priorities.")}</p><Button to="/compare" variant="secondary">{t("Compare plan features")}</Button></div><ul className="event-list"><li><span>10:02</span><strong>{t("Skyline Gliders completed")}</strong><em>{t("On schedule")}</em></li><li><span>10:47</span><strong>{t("Lunch moved 30 min earlier")}</strong><em>{t("Plan recalculated")}</em></li><li><span>11:18</span><strong>{t("Summit Coaster closed")}</strong><em>{t("New route found")}</em></li><li><span>11:19</span><strong>{t("Must-dos still achievable")}</strong><em>{t("86% probability")}</em></li></ul></div></section>
  </>;
}
