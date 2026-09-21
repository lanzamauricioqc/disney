# Real-Time Theme Park Itinerary & AI Guide
## Future Roadmap

This roadmap tracks deferred product work that is not part of the current
development sequence.

---

# Feature 12 — Queue-Time Prediction
**Priority: P1**  
**MVP: NO**

Current wait times are useful, but future wait-time prediction allows the optimizer to make much better decisions.

## User Stories

### [DONE] Story 12.1 — Predict future wait time

As the optimization engine,  
I want to estimate an attraction's wait time later in the day,  
so that I can determine whether visiting now or later is better.

### [DONE] Story 12.2 — Prediction confidence

As the optimization engine,  
I want each prediction to include a confidence measure,  
so that uncertain predictions influence recommendations appropriately.

### [DONE] Story 12.3 — Use historical patterns

As the prediction engine,  
I want historical queue patterns included in forecasts,  
so that predictions reflect typical attraction behavior.

### Story 12.4 — Incorporate current conditions

As the prediction engine,  
I want current queue observations included in forecasts,  
so that predictions respond to unusual conditions.

### Story 12.5 — Prediction accuracy monitoring

As a system administrator,  
I want predicted waits compared with actual waits,  
so that forecast quality can be measured.
