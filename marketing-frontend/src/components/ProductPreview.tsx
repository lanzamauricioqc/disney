export function ProductPreview() {
  return <div className="phone-preview" aria-label="Illustration of a live Park Pilot itinerary">
    <div className="phone-top"><span>10:42</span><span className="signal">● ● ●</span></div>
    <div className="preview-heading"><div><small>YOUR NEXT MOVE</small><h3>River Rapids</h3></div><span className="walk-chip">8 min walk</span></div>
    <div className="reason-card"><span className="reason-icon" aria-hidden="true">↗</span><div><strong>Go now and save about 25 min</strong><p>The queue is 18 min and likely to rise after 11:15.</p></div></div>
    <ol className="timeline">
      <li className="done"><span className="timeline-dot">✓</span><div><small>COMPLETED · 9:35</small><strong>Skyline Gliders</strong></div></li>
      <li className="current"><span className="timeline-dot">2</span><div><small>NEXT · 10:50</small><strong>River Rapids</strong><em>18 min wait</em></div></li>
      <li><span className="timeline-dot">3</span><div><small>12:05</small><strong>Trailside lunch</strong><em>Break · 40 min</em></div></li>
      <li><span className="timeline-dot">4</span><div><small>1:10</small><strong>Summit Coaster</strong><em>Predicted 32 min</em></div></li>
    </ol>
    <div className="replan-toast"><span className="pulse" aria-hidden="true" /> <div><strong>Plan updated</strong><small>14 minutes saved</small></div></div>
  </div>;
}
