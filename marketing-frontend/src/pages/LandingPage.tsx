import { Link } from "react-router-dom";
import { Button } from "../components/Button";
import { PageMeta } from "../components/PageMeta";
import { PricingCards } from "../components/PricingCards";
import { ProductPreview } from "../components/ProductPreview";
import { WaitlistForm } from "../components/WaitlistForm";
import { useI18n } from "../i18n";

const faqs = [
  ["Is Park Pilot another queue-time app?", "No. Queue times are one input. Park Pilot turns live conditions, predicted waits, walking time, priorities, breaks, and closures into a clear next move."],
  ["Does the AI decide my itinerary?", "No. A deterministic optimization engine builds the plan. The AI Guide helps understand requests and explains why each recommendation makes sense."],
  ["Will it work when plans change?", "That is the point. Mark an attraction complete, take a longer lunch, skip a stop, or react to a closure and Park Pilot recalculates what remains."],
  ["Is pricing final?", "Not yet. The displayed Visit and Trip Pass prices are introductory draft pricing while we learn what delivers the most value."],
  ["Is Park Pilot connected to a park operator?", "No. Park Pilot is an independent planning product and is not affiliated with any theme park operator."],
];

export function LandingPage() {
  const { t } = useI18n();
  return <>
    <PageMeta title="Your smarter park day" description="Park Pilot answers what should I do next with a live theme park itinerary built around your must-dos." />
    <section className="hero"><div className="hero-shape hero-shape--one" /><div className="container hero-grid">
      <div className="hero-copy"><span className="eyebrow"><span className="status-dot" />{t("Early access opening soon")}</span><h1>{t("Stop planning the park.")}<br /><em>{t("Start enjoying it.")}</em></h1><p className="hero-lede">{t("Park Pilot tells you what to do next—then quietly rebuilds your day when queues, closures, or real life change the plan.")}</p><WaitlistForm /><p className="microcopy">{t("Free to join. Product updates only.")}</p></div>
      <div className="hero-visual"><div className="sun-disc" /><ProductPreview /><div className="floating-note floating-note--top"><strong>{t("Protected")}</strong><span>{t("must-do priorities")}</span></div><div className="floating-note floating-note--bottom"><strong>{t("Adaptive")}</strong><span>{t("wait-aware routing")}</span></div></div>
    </div></section>
    <section className="trust-strip"><div className="container"><p>{t("Built for days with")}</p><div><span>{t("Live queues")}</span><span>{t("Walking time")}</span><span>{t("Must-do priorities")}</span><span>{t("Unexpected changes")}</span></div></div></section>

    <section className="section" id="how-it-works"><div className="container"><div className="section-heading"><span className="kicker">{t("HOW IT WORKS")}</span><h2>{t("One less decision at every turn.")}</h2><p>{t("Set your intent once. Park Pilot keeps the plan useful all day.")}</p></div><div className="steps">
      <article><span>01</span><div className="step-icon">◎</div><h3>{t("Tell us your day")}</h3><p>{t("Choose must-dos, nice-to-haves, arrival time, breaks, party needs, and pace.")}</p></article>
      <article><span>02</span><div className="step-icon">⌁</div><h3>{t("Get a realistic route")}</h3><p>{t("The engine weighs queues, predictions, distance, timing, and completion risk.")}</p></article>
      <article><span>03</span><div className="step-icon">↻</div><h3>{t("Move with confidence")}</h3><p>{t("See the next best move and why. When conditions change, your route follows.")}</p></article>
    </div></div></section>

    <section className="section section--ink"><div className="container split-feature"><div><span className="kicker kicker--light">{t("LIVE REPLANNING")}</span><h2>{t("A plan that knows the day will not go to plan.")}</h2><p>{t("Waits spike. Rides pause. Lunch takes longer. Park Pilot recalculates the remaining day instead of making you start over.")}</p><ul className="plain-checks"><li>{t("Uses current and predicted queue conditions")}</li><li>{t("Protects the attractions you marked must-do")}</li><li>{t("Accounts for your location and walking time")}</li><li>{t("Explains what changed and why")}</li></ul><Button to="/product" variant="secondary">{t("Explore the product")}</Button></div><div className="replan-demo"><div className="demo-map" aria-hidden="true"><span className="route route--a" /><span className="route route--b" /><i className="pin pin--one">1</i><i className="pin pin--two">2</i><i className="pin pin--three">3</i></div><div className="change-card"><small>{t("11:18 · CONDITIONS CHANGED")}</small><strong>{t("Summit Coaster temporarily closed")}</strong><p>{t("Moving River Rapids earlier protects 3 of 3 must-dos and saves an estimated 22 minutes.")}</p><div><span>{t("New plan ready")}</span><button type="button">{t("Review change →")}</button></div></div></div></div></section>

    <section className="section audience"><div className="container"><div className="section-heading"><span className="kicker">{t("MADE FOR REAL PARK DAYS")}</span><h2>{t("Your group. Your pace. Your priorities.")}</h2></div><div className="audience-grid">
      <article><span aria-hidden="true">◒</span><h3>{t("Families")}</h3><p>{t("Plan breaks, reduce cross-park backtracking, and protect the experiences everyone cares about.")}</p></article>
      <article><span aria-hidden="true">◇</span><h3>{t("First-time visitors")}</h3><p>{t("Make strong choices without becoming an expert in park maps and crowd patterns.")}</p></article>
      <article><span aria-hidden="true">△</span><h3>{t("Couples & friends")}</h3><p>{t("Choose a pace from relaxed to ambitious and spend less time debating the next stop.")}</p></article>
      <article><span aria-hidden="true">◉</span><h3>{t("Time-limited trips")}</h3><p>{t("Know what is realistic and see the probability of completing the day you imagined.")}</p></article>
    </div></div></section>

    <section className="company-teaser">
      <div className="container company-teaser-grid"><div><span className="teaser-badge">{t("PLANNED FOR COMPANIES")}</span><span className="kicker kicker--light">{t("FOR TRAVEL AGENCIES & TOUR OPERATORS")}</span><h2>{t("A personalized park guide for every customer.")}</h2><p>{t("Prepare a branded visitor experience, share it with one unique link, and support active visits without manually planning and monitoring every park day.")}</p><Button to="/companies" variant="secondary">{t("Explore the company vision")}</Button></div><div className="teaser-steps" aria-label={t("Planned company workflow")}><span><strong>01</strong>{t("Prepare the visit")}</span><span><strong>02</strong>{t("Assign access")}</span><span><strong>03</strong>{t("Share with the traveler")}</span><span><strong>04</strong>{t("Support when needed")}</span></div></div>
    </section>
    <section className="section pricing-teaser"><div className="container"><div className="section-heading"><span className="kicker">{t("SIMPLE VISIT-BASED PRICING")}</span><h2>{t("Pay for the park days you need.")}</h2><p>{t("No subscription required. Prices shown are introductory drafts and may change.")}</p></div><PricingCards concise /><p className="center-link"><Link to="/pricing">{t("See full plan details and draft pricing →")}</Link></p></div></section>

    <section className="section faq"><div className="container faq-grid"><div><span className="kicker">{t("QUESTIONS, ANSWERED")}</span><h2>{t("Good planning starts with clarity.")}</h2><p>{t("Still curious? Join the waitlist and follow the product as early access takes shape.")}</p></div><div>{faqs.map(([question, answer], index) => <details key={question} open={index === 0}><summary>{t(question)}<span aria-hidden="true">+</span></summary><p>{t(answer)}</p></details>)}</div></div></section>

    <section className="final-cta" id="waitlist"><div className="container"><span className="kicker kicker--light">{t("YOUR NEXT GREAT PARK DAY")}</span><h2>{t("Know what to do next.")}</h2><p>{t("Join early access updates for Park Pilot.")}</p><WaitlistForm compact /></div></section>
  </>;
}
