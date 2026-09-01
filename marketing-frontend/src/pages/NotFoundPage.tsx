import { Button } from "../components/Button";
import { PageMeta } from "../components/PageMeta";

export function NotFoundPage() {
  return <section className="not-found"><PageMeta title="Page not found" description="The requested Park Pilot page could not be found." noIndex /><div className="container narrow"><span className="compass" aria-hidden="true">↗</span><span className="kicker">404 · OFF ROUTE</span><h1>This stop is not on the plan.</h1><p>The page may have moved, but your next best action is easy.</p><Button to="/">Return home</Button></div></section>;
}
