import { Button } from "../components/Button";
import { PageMeta } from "../components/PageMeta";
import { ProductPreview } from "../components/ProductPreview";

const layers = [
  { n: "01", title: "Park intelligence", text: "A living model of attractions, operating hours, live and historical waits, predicted queues, closures, locations, durations, and walking time.", tag: "KNOW WHAT IS HAPPENING" },
  { n: "02", title: "Optimization engine", text: "A deterministic, testable engine weighs your priorities and constraints to build the strongest realistic sequence—not just the shortest queue.", tag: "DECIDE WHAT COMES NEXT" },
  { n: "03", title: "AI Guide", text: "A conversational layer understands natural-language requests, turns them into constraints, answers questions, and explains the engine recommendation.", tag: "MAKE EVERY CHOICE CLEAR" },
];

export function ProductPage() {
  return <>
    <PageMeta title="How Park Pilot works" description="Explore the park intelligence, deterministic optimization, AI Guide, and live replanning behind Park Pilot." />
    <section className="page-hero product-hero"><div className="container narrow"><span className="eyebrow">THE PARK-DAY DECISION ENGINE</span><h1>A great itinerary is alive.</h1><p>Park Pilot combines real park conditions with what matters to you, producing a plan that stays practical from rope drop to the last ride.</p><div className="hero-actions"><Button to="/pricing">View draft pricing</Button><Button to="/#waitlist" variant="secondary">Join early access</Button></div></div></section>
    <section className="section"><div className="container"><div className="section-heading"><span className="kicker">THREE LAYERS, ONE CLEAR ANSWER</span><h2>Built to answer: “What should I do next?”</h2></div><div className="layer-grid">{layers.map((layer) => <article key={layer.n}><span className="layer-number">{layer.n}</span><small>{layer.tag}</small><h3>{layer.title}</h3><p>{layer.text}</p></article>)}</div></div></section>
    <section className="section section--sand"><div className="container product-detail"><div><span className="kicker">DECISIONS YOU CAN TRUST</span><h2>The AI explains. The engine decides.</h2><p>Generative AI does not invent your route. It translates requests such as “the kids need a 30-minute break” into clear constraints. A deterministic optimization engine then recalculates the itinerary.</p><blockquote>“Take a break near the north trail. River Rapids is next because its 18-minute wait is expected to reach 40 minutes by noon. Your three must-dos remain achievable.”</blockquote><p className="caption">Example recommendation explanation</p></div><ProductPreview /></div></section>
    <section className="section"><div className="container"><div className="section-heading"><span className="kicker">CHOOSE YOUR STRATEGY</span><h2>There is no single perfect park day.</h2><p>Select the tradeoff that fits your group. Change it whenever the day changes.</p></div><div className="mode-grid">
      <article><span className="mode-gauge">75%</span><h3>Safe</h3><p>Prioritize the probability of completing every must-do, with more timing buffer and lower risk.</p><small>BEST FOR: FIRST VISITS & FAMILIES</small></article>
      <article className="mode-featured"><span className="mode-gauge">↔</span><h3>Balanced</h3><p>Balance attraction count, queue time, walking distance, breaks, and your stated priorities.</p><small>BEST FOR: MOST PARK DAYS</small></article>
      <article><span className="mode-gauge">+3</span><h3>Aggressive</h3><p>Attempt more attractions with tighter timing, more movement, and a higher chance of changes.</p><small>BEST FOR: EXPERIENCED VISITORS</small></article>
    </div></div></section>
    <section className="section section--ink"><div className="container live-grid"><div><span className="kicker kicker--light">CONTINUOUSLY RECALCULATED</span><h2>Change one thing. Keep the whole day useful.</h2><p>Park Pilot replans after completed or skipped attractions, unexpected closures, timing drift, queue movement, breaks, and changed priorities.</p><Button to="/compare" variant="secondary">Compare plan features</Button></div><ul className="event-list"><li><span>10:02</span><strong>Skyline Gliders completed</strong><em>On schedule</em></li><li><span>10:47</span><strong>Lunch moved 30 min earlier</strong><em>Plan recalculated</em></li><li><span>11:18</span><strong>Summit Coaster closed</strong><em>New route found</em></li><li><span>11:19</span><strong>Must-dos still achievable</strong><em>86% probability</em></li></ul></div></section>
  </>;
}
