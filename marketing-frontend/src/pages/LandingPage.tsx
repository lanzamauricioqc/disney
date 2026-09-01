import { Link } from "react-router-dom";
import { Button } from "../components/Button";
import { PageMeta } from "../components/PageMeta";
import { PricingCards } from "../components/PricingCards";
import { ProductPreview } from "../components/ProductPreview";
import { WaitlistForm } from "../components/WaitlistForm";

const faqs = [
  ["Is Park Pilot another queue-time app?", "No. Queue times are one input. Park Pilot turns live conditions, predicted waits, walking time, priorities, breaks, and closures into a clear next move."],
  ["Does the AI decide my itinerary?", "No. A deterministic optimization engine builds the plan. The AI Guide helps understand requests and explains why each recommendation makes sense."],
  ["Will it work when plans change?", "That is the point. Mark an attraction complete, take a longer lunch, skip a stop, or react to a closure and Park Pilot recalculates what remains."],
  ["Is pricing final?", "Not yet. The displayed Visit and Trip Pass prices are introductory draft pricing while we learn what delivers the most value."],
  ["Is Park Pilot connected to a park operator?", "No. Park Pilot is an independent planning product and is not affiliated with any theme park operator."],
];

export function LandingPage() {
  return <>
    <PageMeta title="Your smarter park day" description="Park Pilot answers what should I do next with a live theme park itinerary built around your must-dos." />
    <section className="hero"><div className="hero-shape hero-shape--one" /><div className="container hero-grid">
      <div className="hero-copy"><span className="eyebrow"><span className="status-dot" /> Early access opening soon</span><h1>Stop planning the park.<br /><em>Start enjoying it.</em></h1><p className="hero-lede">Park Pilot tells you what to do next—then quietly rebuilds your day when queues, closures, or real life change the plan.</p><WaitlistForm /><p className="microcopy">Free to join. Product updates only.</p></div>
      <div className="hero-visual"><div className="sun-disc" /><ProductPreview /><div className="floating-note floating-note--top"><strong>Protected</strong><span>must-do priorities</span></div><div className="floating-note floating-note--bottom"><strong>Adaptive</strong><span>wait-aware routing</span></div></div>
    </div></section>
    <section className="trust-strip"><div className="container"><p>Built for days with</p><div><span>Live queues</span><span>Walking time</span><span>Must-do priorities</span><span>Unexpected changes</span></div></div></section>

    <section className="section" id="how-it-works"><div className="container"><div className="section-heading"><span className="kicker">HOW IT WORKS</span><h2>One less decision at every turn.</h2><p>Set your intent once. Park Pilot keeps the plan useful all day.</p></div><div className="steps">
      <article><span>01</span><div className="step-icon">◎</div><h3>Tell us your day</h3><p>Choose must-dos, nice-to-haves, arrival time, breaks, party needs, and pace.</p></article>
      <article><span>02</span><div className="step-icon">⌁</div><h3>Get a realistic route</h3><p>The engine weighs queues, predictions, distance, timing, and completion risk.</p></article>
      <article><span>03</span><div className="step-icon">↻</div><h3>Move with confidence</h3><p>See the next best move and why. When conditions change, your route follows.</p></article>
    </div></div></section>

    <section className="section section--ink"><div className="container split-feature"><div><span className="kicker kicker--light">LIVE REPLANNING</span><h2>A plan that knows the day will not go to plan.</h2><p>Waits spike. Rides pause. Lunch takes longer. Park Pilot recalculates the remaining day instead of making you start over.</p><ul className="plain-checks"><li>Uses current and predicted queue conditions</li><li>Protects the attractions you marked must-do</li><li>Accounts for your location and walking time</li><li>Explains what changed and why</li></ul><Button to="/product" variant="secondary">Explore the product</Button></div><div className="replan-demo"><div className="demo-map" aria-hidden="true"><span className="route route--a" /><span className="route route--b" /><i className="pin pin--one">1</i><i className="pin pin--two">2</i><i className="pin pin--three">3</i></div><div className="change-card"><small>11:18 · CONDITIONS CHANGED</small><strong>Summit Coaster temporarily closed</strong><p>Moving River Rapids earlier protects 3 of 3 must-dos and saves an estimated 22 minutes.</p><div><span>New plan ready</span><button type="button">Review change →</button></div></div></div></div></section>

    <section className="section audience"><div className="container"><div className="section-heading"><span className="kicker">MADE FOR REAL PARK DAYS</span><h2>Your group. Your pace. Your priorities.</h2></div><div className="audience-grid">
      <article><span aria-hidden="true">◒</span><h3>Families</h3><p>Plan breaks, reduce cross-park backtracking, and protect the experiences everyone cares about.</p></article>
      <article><span aria-hidden="true">◇</span><h3>First-time visitors</h3><p>Make strong choices without becoming an expert in park maps and crowd patterns.</p></article>
      <article><span aria-hidden="true">△</span><h3>Couples & friends</h3><p>Choose a pace from relaxed to ambitious and spend less time debating the next stop.</p></article>
      <article><span aria-hidden="true">◉</span><h3>Time-limited trips</h3><p>Know what is realistic and see the probability of completing the day you imagined.</p></article>
    </div></div></section>

    <section className="company-teaser">
      <div className="container company-teaser-grid"><div><span className="teaser-badge">PLANNED FOR COMPANIES</span><span className="kicker kicker--light">FOR TRAVEL AGENCIES & TOUR OPERATORS</span><h2>A personalized park guide for every customer.</h2><p>Prepare a branded visitor experience, share it with one unique link, and support active visits without manually planning and monitoring every park day.</p><Button to="/companies" variant="secondary">Explore the company vision</Button></div><div className="teaser-steps" aria-label="Planned company workflow"><span><strong>01</strong> Prepare the visit</span><span><strong>02</strong> Assign access</span><span><strong>03</strong> Share with the traveler</span><span><strong>04</strong> Support when needed</span></div></div>
    </section>
    <section className="section pricing-teaser"><div className="container"><div className="section-heading"><span className="kicker">SIMPLE VISIT-BASED PRICING</span><h2>Pay for the park days you need.</h2><p>No subscription required. Prices shown are introductory drafts and may change.</p></div><PricingCards concise /><p className="center-link"><Link to="/pricing">See full plan details and draft pricing →</Link></p></div></section>

    <section className="section faq"><div className="container faq-grid"><div><span className="kicker">QUESTIONS, ANSWERED</span><h2>Good planning starts with clarity.</h2><p>Still curious? Join the waitlist and follow the product as early access takes shape.</p></div><div>{faqs.map(([question, answer], index) => <details key={question} open={index === 0}><summary>{question}<span aria-hidden="true">+</span></summary><p>{answer}</p></details>)}</div></div></section>

    <section className="final-cta" id="waitlist"><div className="container"><span className="kicker kicker--light">YOUR NEXT GREAT PARK DAY</span><h2>Know what to do next.</h2><p>Join early access updates for Park Pilot.</p><WaitlistForm compact /></div></section>
  </>;
}
