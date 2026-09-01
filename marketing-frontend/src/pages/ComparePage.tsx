import { Button } from "../components/Button";
import { PageMeta } from "../components/PageMeta";

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
  return <>
    <PageMeta title="Compare plans" description="Compare Park Pilot Free, Visit Pass, and Trip Pass features in one accessible table." />
    <section className="page-hero"><div className="container narrow"><span className="eyebrow">PLAN COMPARISON</span><h1>Choose how far ahead to pilot.</h1><p>Browse for free, optimize one big day, or keep an entire park trip moving.</p></div></section>
    <section className="section compare-section"><div className="container"><div className="comparison-wrap" tabIndex={0} role="region" aria-label="Scrollable plan comparison">
      <table className="comparison-table"><caption>Features included in each Park Pilot plan. Prices are introductory drafts.</caption><thead><tr><th scope="col">Feature</th><th scope="col"><strong>Free</strong><span>$0</span></th><th scope="col" className="recommended"><small>RECOMMENDED</small><strong>Visit Pass</strong><span>$8 / park day</span></th><th scope="col"><strong>Trip Pass</strong><span>$20 / trip</span></th></tr></thead><tbody>{rows.map((row) => <tr key={row[0]}><th scope="row">{row[0]}</th>{row.slice(1).map((cell, index) => <td key={index} className={cell === "Included" ? "included" : undefined}>{cell === "Included" ? <><span aria-hidden="true">✓</span><span className="sr-only">Included</span></> : cell}</td>)}</tr>)}</tbody><tfoot><tr><th scope="row">Choose a plan</th><td><Button to="/#waitlist" variant="secondary">Join free</Button></td><td><Button to="/checkout?plan=visit-pass">Choose Visit</Button></td><td><Button to="/checkout?plan=trip-pass" variant="secondary">Choose Trip</Button></td></tr></tfoot></table>
    </div><p className="compare-note">Draft prices and plan details are subject to change before general availability.</p></div></section>
  </>;
}
