import { Link } from "react-router-dom";

export function Footer() {
  return <footer className="site-footer">
    <div className="container footer-grid">
      <div><Link className="brand brand--footer" to="/"><span className="brand-mark" aria-hidden="true"><span /></span><span>Park Pilot</span></Link><p>More park. Less guesswork.</p></div>
      <nav aria-label="Footer navigation"><h2>Explore</h2><Link to="/product">Product</Link><Link to="/pricing">Pricing</Link><Link to="/compare">Compare plans</Link><Link to="/companies">For companies</Link></nav>
      <div><h2>Built for the day</h2><p>Real-time decisions, clearly explained. No affiliation with any theme park operator.</p></div>
    </div>
    <div className="container footer-bottom"><span>© {new Date().getFullYear()} Park Pilot</span><span>Independent park-day planning technology.</span></div>
  </footer>;
}
