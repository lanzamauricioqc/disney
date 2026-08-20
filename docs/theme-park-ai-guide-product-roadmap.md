# Real-Time Theme Park Itinerary & AI Guide
## Product Vision, Feature Roadmap, and User Stories

---

## 1. General System Description

### 1.1 Product Vision

The system is a **real-time theme park itinerary optimization and decision-support platform** designed to help visitors make better decisions throughout a park visit.

Instead of acting only as a queue-time dashboard, the product continuously answers the most important question a visitor has during the day:

> **What should I do next?**

Before arriving at the park, the visitor selects the attractions they care about, their visit date, expected arrival and departure times, party size, and personal priorities.

The system then creates an optimized itinerary using information such as:

- Current attraction wait times
- Historical wait-time patterns
- Predicted future wait times
- Attraction locations
- Walking times between attractions
- Attraction duration
- Park operating hours
- Attraction operating hours
- Temporary closures
- User priorities
- Previously completed attractions
- Skipped attractions
- Breaks and meal periods
- Party constraints
- Real-time changes during the visit

The itinerary is not static. It is continuously recalculated as conditions change.

For example, if an attraction closes temporarily, another queue becomes significantly shorter, or the visitor takes longer than expected at lunch, the system should automatically adjust the remaining itinerary.

The objective is not simply to minimize waiting time. The optimization engine should attempt to maximize the overall value of the visit while respecting the visitor's priorities and constraints.

Conceptually:

```text
Maximize:
    Attraction priority completed
    + Number of desired attractions completed
    + User satisfaction

Minimize:
    Queue waiting time
    + Walking time
    + Idle time
    + Risk of missing must-do attractions
    + Poor timing decisions
```

The product should eventually offer multiple optimization strategies such as:

- **Safe Plan** — maximize the probability of completing all must-do attractions
- **Balanced Plan** — balance attraction count, walking distance, and queue time
- **Aggressive Plan** — maximize the number of attractions attempted

The system should also explain recommendations. A visitor should not simply see:

> Go to Pirates of the Caribbean.

Instead, the product should be able to explain:

> Go to Pirates of the Caribbean now. The current wait is 20 minutes and is expected to increase to approximately 45 minutes within the next hour. Space Mountain currently has a long wait and is expected to improve later.

This explanation layer builds user trust and makes the system useful even when the visitor decides not to follow the recommendation.

---

## 1.2 Core Product Architecture

The system should be designed around three major logical layers.

### Park Intelligence Layer

This layer represents everything known about the park.

Examples include:

- Parks
- Attractions
- Attraction locations
- Attraction durations
- Restrictions
- Opening and closing hours
- Current wait times
- Historical wait times
- Temporary closures
- Walking distances
- Walking times
- Show schedules
- Restaurant locations
- Weather conditions
- Special queue systems
- Accessibility information

This layer becomes one of the most valuable long-term assets of the platform.

---

### Optimization Engine

The optimization engine is the core decision-making component.

It should determine the best sequence of attractions based on the visitor's preferences and the current park state.

The engine must remain deterministic and testable.

An LLM should **not** be responsible for deciding the actual itinerary.

The optimization engine should consider:

- Current location
- Walking time
- Current queue time
- Predicted future queue time
- Attraction priority
- Attraction duration
- Remaining park time
- Attraction closures
- User constraints
- Completion probability

---

### AI Guide

The AI Guide acts as a conversational layer above the optimization engine.

Its responsibilities include:

- Understanding natural-language requests
- Translating user requests into itinerary constraints
- Explaining recommendations
- Answering park-related questions
- Helping the visitor modify their plan

Example:

```text
User:
"My kids are tired. Can we stop somewhere for 30 minutes without ruining the plan?"

AI interpretation:
- Add a 30-minute break
- Prefer a nearby location
- Preserve all must-do attractions

Optimization Engine:
Recalculate itinerary

AI response:
"Take a 30-minute break near Fantasyland. After that, go to Haunted Mansion.
You should still be able to complete all four of your must-do attractions."
```

The AI should interpret and explain decisions.

The optimization engine should make the decisions.

---

## 1.3 Target Users

### Primary Initial Market — Individual Visitors

Visitors who want to maximize their experience at a theme park without manually analyzing maps, queue times, and attraction schedules.

Typical users include:

- Families
- Couples
- Groups of friends
- First-time visitors
- Tourists unfamiliar with the park
- Visitors with limited time

---

### Future Market — Tour Operators and Travel Agencies

Tour companies could create visit sessions for their customers.

The system could provide:

- Branded visitor experiences
- Preconfigured itineraries
- Customer management
- Visit tracking
- Guide dashboards
- Recommendations
- Custom messages
- Meeting points
- Transport instructions
- Usage-based billing

The same visitor architecture should therefore eventually support both:

```text
Individual User
    └── Visit Session
```

and:

```text
Organization
    ├── Staff
    └── Customers
         └── Visit Sessions
```

The MVP should focus on the consumer product while keeping this future architecture in mind.

---

## 1.4 MVP Definition

The MVP must validate one core hypothesis:

> **Can the platform produce and dynamically update a theme park itinerary that is meaningfully better than what a visitor could create manually?**

The MVP should therefore focus only on capabilities required to generate, display, execute, and recalculate a useful itinerary.

Features explicitly marked **[MVP REQUIRED]** are mandatory for the first usable product.

Stories marked **[MVP REQUIRED]** are mandatory within those features.

---

# 2. Feature Roadmap

---

# Feature 1 — Park and Attraction Catalog
**Priority: P0**  
**MVP: YES — [MVP REQUIRED]**

The system must have a reliable internal representation of parks and attractions before itinerary optimization can exist.

## User Stories

### [DONE] [MVP REQUIRED] Story 1.1 — View available parks

As a visitor,  
I want to see the parks supported by the platform,  
so that I can select the park I intend to visit.

### [DONE] [MVP REQUIRED] Story 1.2 — View park attractions

As a visitor,  
I want to see the attractions available in a selected park,  
so that I can choose which attractions matter to me.

### [DONE] [MVP REQUIRED] Story 1.3 — Store attraction metadata

As the system,  
I need to store each attraction's name, current land, active status, and optional duration and coordinates,  
so that attraction information can be collected automatically and enriched through administration.

### Story 1.4 — Store attraction restrictions

As a visitor,  
I want the system to know attraction restrictions such as minimum height or accessibility limitations,  
so that incompatible attractions can be excluded from my plan.

### Story 1.5 — Attraction categories

As a visitor,  
I want attractions categorized by type,  
so that I can more easily discover attractions matching my interests.

---

# Feature 2 — Live Queue-Time Collection
**Priority: P0**  
**MVP: YES — [MVP REQUIRED]**

The system must continuously collect current attraction wait times and operational states.

## User Stories

### [DONE] [MVP REQUIRED] Story 2.1 — Collect current wait times

As the system,  
I want to periodically collect attraction wait times,  
so that recommendations can use current park conditions.

### [DONE] [MVP REQUIRED] Story 2.2 — Persist queue observations

As the system,  
I want to store timestamped queue observations,  
so that historical analysis and future predictions become possible.

### [DONE] [MVP REQUIRED] Story 2.3 — Track attraction availability

As the system,  
I want to record whether an attraction is open during each queue observation and whether it remains active in the source catalog,  
so that current operating and availability information is preserved for downstream use.

### Story 2.4 — Detect abnormal queue changes

As the system,  
I want to detect sudden queue-time changes,  
so that future recommendations and alerts can react quickly.

---

# Feature 3 — Historical Queue Data
**Priority: P0**  
**MVP: YES — [MVP REQUIRED]**

Historical observations provide the foundation for understanding queue behavior.

## User Stories

### [DONE] [MVP REQUIRED] Story 3.1 — Store historical wait-time observations

As the system,  
I want to maintain historical wait-time data,  
so that queue trends can be analyzed.

### [DONE] [MVP REQUIRED] Story 3.2 — Query historical wait times

As a park intelligence consumer,  
I want to retrieve valid historical queue observations by attraction and timestamp range,  
so that downstream services can analyze past queue conditions.

### [DONE] Story 3.3 — Historical aggregation

As the system,  
I want historical observations aggregated by useful dimensions such as weekday and hour,  
so that prediction queries can remain efficient.

### Story 3.4 — Historical data quality validation

As the system administrator,  
I want invalid or suspicious observations identified,  
so that unreliable data does not negatively affect predictions.

---

# Feature 4 — Visitor Visit Setup
**Priority: P0**  
**MVP: YES — [MVP REQUIRED]**

The visitor must be able to describe the visit before an itinerary can be generated.

## User Stories

### [MVP REQUIRED] Story 4.1 — Select visit date

As a visitor,  
I want to select the date of my park visit,  
so that the system can generate a plan for the correct day.

### [MVP REQUIRED] Story 4.2 — Select arrival time

As a visitor,  
I want to specify when I expect to arrive,  
so that the itinerary starts at the appropriate time.

### [MVP REQUIRED] Story 4.3 — Select departure time

As a visitor,  
I want to specify when I expect to leave,  
so that the itinerary does not schedule activities after my visit ends.

### [MVP REQUIRED] Story 4.4 — Specify party size

As a visitor,  
I want to specify how many people are in my group,  
so that the visit profile accurately represents my party.

### Story 4.5 — Save reusable visitor preferences

As a returning visitor,  
I want my general preferences remembered,  
so that future visit setup requires less configuration.

---

# Feature 5 — Attraction Priority Selection
**Priority: P0**  
**MVP: YES — [MVP REQUIRED]**

Visitors must communicate which attractions matter most.

## User Stories

### [MVP REQUIRED] Story 5.1 — Mark attractions as Must Do

As a visitor,  
I want to mark attractions as Must Do,  
so that the itinerary prioritizes completing them.

### [MVP REQUIRED] Story 5.2 — Mark attractions as Would Like

As a visitor,  
I want to mark attractions as Would Like,  
so that the optimizer includes them when practical.

### [MVP REQUIRED] Story 5.3 — Mark attractions as Skip

As a visitor,  
I want to mark attractions as Skip,  
so that they are excluded from the itinerary.

### Story 5.4 — Recommend unselected attractions

As a visitor,  
I want the system to suggest attractions I did not select,  
so that unused itinerary time can still be valuable.

---

# Feature 6 — Park Map Graph and Walking-Time Model
**Priority: P0**  
**MVP: YES — [MVP REQUIRED]**

The optimizer needs a model of physical movement through the park.

## User Stories

### [MVP REQUIRED] Story 6.1 — Store attraction coordinates

As the system,  
I want to store attraction coordinates,  
so that distances between attractions can be calculated.

### [MVP REQUIRED] Story 6.2 — Estimate walking time

As the optimization engine,  
I want to estimate walking time between attractions,  
so that itinerary decisions account for travel time.

### [MVP REQUIRED] Story 6.3 — Calculate routes between attractions

As the optimization engine,  
I want a graph representing walkable park routes,  
so that realistic path distances can be used instead of straight-line distance.

### Story 6.4 — Account for inaccessible routes

As the system,  
I want temporarily or permanently unavailable pathways represented,  
so that routing avoids unusable paths.

---

# Feature 7 — Initial Itinerary Optimization Engine
**Priority: P0**  
**MVP: YES — [MVP REQUIRED]**

This is the core product capability.

## User Stories

### [MVP REQUIRED] Story 7.1 — Generate optimized itinerary

As a visitor,  
I want the system to generate an ordered itinerary,  
so that I know which attraction I should visit and when.

### [MVP REQUIRED] Story 7.2 — Prioritize Must Do attractions

As a visitor,  
I want Must Do attractions heavily prioritized,  
so that the plan minimizes the risk of missing them.

### [MVP REQUIRED] Story 7.3 — Consider current and historical queue times

As the optimization engine,  
I want current waits and relevant historical queue patterns included in itinerary calculations,  
so that the itinerary reflects live conditions and typical queue behavior.

### [MVP REQUIRED] Story 7.4 — Consider walking time

As the optimization engine,  
I want walking time included in itinerary calculations,  
so that the plan avoids inefficient park traversal.

### [MVP REQUIRED] Story 7.5 — Respect visit end time

As the visitor,  
I want the system to stop scheduling activities beyond my departure time,  
so that the itinerary remains realistic.

### [MVP REQUIRED] Story 7.6 — Respect attraction availability

As a visitor,  
I want the optimizer to use the latest recorded open and active statuses when generating recommendations,  
so that I am never intentionally sent to a closed or unavailable attraction.

### Story 7.7 — Optimization scoring

As the system,  
I want itinerary candidates evaluated using a configurable scoring function,  
so that optimization behavior can evolve without redesigning the platform.

---

# Feature 8 — Visit Session and Itinerary State
**Priority: P0**  
**MVP: YES — [MVP REQUIRED]**

The system must know what has already happened during the visit.

## User Stories

### [MVP REQUIRED] Story 8.1 — Start visit session

As a visitor,  
I want to start my planned visit,  
so that the system can begin tracking itinerary execution.

### [MVP REQUIRED] Story 8.2 — Mark attraction completed

As a visitor,  
I want to mark an attraction as completed,  
so that the system knows it no longer needs to recommend it.

### [MVP REQUIRED] Story 8.3 — Skip attraction

As a visitor,  
I want to skip a planned attraction,  
so that the remaining itinerary can be adjusted.

### [MVP REQUIRED] Story 8.4 — Persist visit state

As a visitor,  
I want my visit progress preserved,  
so that refreshing or reopening the application does not lose my itinerary state.

### Story 8.5 — Resume interrupted visit

As a visitor,  
I want to resume my current visit session after reopening the application,  
so that I can continue without reconfiguration.

---

# Feature 9 — Live "What Should I Do Next?" Experience
**Priority: P0**  
**MVP: YES — [MVP REQUIRED]**

This should become the primary visitor experience during the park visit.

## User Stories

### [MVP REQUIRED] Story 9.1 — Show next recommended attraction

As a visitor,  
I want one clear next recommendation,  
so that I do not need to analyze the entire itinerary.

### [MVP REQUIRED] Story 9.2 — Show current wait time

As a visitor,  
I want to see the current queue for the recommended attraction,  
so that I understand the immediate cost of the recommendation.

### [MVP REQUIRED] Story 9.3 — Show walking time

As a visitor,  
I want to see the estimated walking time to the recommendation,  
so that I can understand how far away it is.

### [MVP REQUIRED] Story 9.4 — Show itinerary progress

As a visitor,  
I want to see how many planned attractions I have completed,  
so that I understand my progress through the visit.

### Story 9.5 — Allow visitor to request another option

As a visitor,  
I want to reject the current recommendation and see another option,  
so that I remain in control of my visit.

---

# Feature 10 — Automatic Itinerary Replanning
**Priority: P0**  
**MVP: YES — [MVP REQUIRED]**

A static itinerary will quickly become obsolete. Replanning is therefore part of the MVP value proposition.

## User Stories

### [MVP REQUIRED] Story 10.1 — Recalculate after attraction completion

As a visitor,  
I want the remaining itinerary recalculated after completing an attraction,  
so that the next recommendation reflects my current state.

### [MVP REQUIRED] Story 10.2 — Recalculate after skip

As a visitor,  
I want the itinerary recalculated when I skip an attraction,  
so that the remaining time can be used efficiently.

### [MVP REQUIRED] Story 10.3 — Recalculate after closure

As a visitor,  
I want my itinerary automatically updated if a planned attraction closes,  
so that I am not sent toward an unavailable ride.

### [MVP REQUIRED] Story 10.4 — Recalculate after major queue changes

As a visitor,  
I want the itinerary adjusted when queue conditions materially change,  
so that the plan remains optimized.

### Story 10.5 — Preserve itinerary stability

As a visitor,  
I do not want minor queue changes to constantly reorder my entire itinerary,  
so that the experience remains understandable and predictable.

---

# Feature 11 — Mobile-First Visitor Interface
**Priority: P0**  
**MVP: YES — [MVP REQUIRED]**

The product will primarily be used while walking through a park.

## User Stories

### [DONE] [MVP REQUIRED] Story 11.1 — Mobile-responsive interface

As a visitor,  
I want the application to work well on a mobile phone,  
so that I can comfortably use it inside the park.

### [MVP REQUIRED] Story 11.2 — Fast next-action screen

As a visitor,  
I want the current recommendation visible immediately,  
so that I can make decisions without navigating through multiple pages.

### [MVP REQUIRED] Story 11.3 — Fast loading

As a visitor,  
I want itinerary and queue information to load quickly,  
so that the application remains practical while moving through the park.

### Story 11.4 — Installable PWA

As a visitor,  
I want to install the application on my phone,  
so that it behaves similarly to a native application.

---

# Feature 12 — Queue-Time Prediction
**Priority: P1**  
**MVP: NO**

Current wait times are useful, but future wait-time prediction allows the optimizer to make much better decisions.

## User Stories

### Story 12.1 — Predict future wait time

As the optimization engine,  
I want to estimate an attraction's wait time later in the day,  
so that I can determine whether visiting now or later is better.

### Story 12.2 — Prediction confidence

As the optimization engine,  
I want each prediction to include a confidence measure,  
so that uncertain predictions influence recommendations appropriately.

### Story 12.3 — Use historical patterns

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

---

# Feature 13 — Recommendation Explanations
**Priority: P1**  
**MVP: NO**

The system should explain why an attraction is recommended.

## User Stories

### Story 13.1 — Explain next recommendation

As a visitor,  
I want to know why an attraction is recommended,  
so that I can trust the system's decision.

### Story 13.2 — Compare now versus later

As a visitor,  
I want to know whether a queue is expected to improve or worsen,  
so that I understand the timing recommendation.

### Story 13.3 — Explain trade-offs

As a visitor,  
I want the system to explain trade-offs such as walking farther now to save waiting later,  
so that optimization decisions feel transparent.

---

# Feature 14 — Completion Probability and Plan Risk
**Priority: P1**  
**MVP: NO**

The system should estimate how likely the visitor is to complete the itinerary.

## User Stories

### Story 14.1 — Overall itinerary completion probability

As a visitor,  
I want to see the probability of completing my planned itinerary,  
so that I understand whether my plan is realistic.

### Story 14.2 — Per-attraction completion probability

As a visitor,  
I want to see the probability of completing each Must Do attraction,  
so that I can understand which attractions are at risk.

### Story 14.3 — Highlight high-risk attractions

As a visitor,  
I want the system to identify attractions likely to be missed,  
so that I can prioritize them.

---

# Feature 15 — Optimization Modes
**Priority: P1**  
**MVP: NO**

Different visitors may prefer different itinerary strategies.

## User Stories

### Story 15.1 — Safe Plan

As a visitor,  
I want a conservative itinerary,  
so that the probability of completing my Must Do attractions is maximized.

### Story 15.2 — Balanced Plan

As a visitor,  
I want a balanced itinerary,  
so that queue time, walking, and attraction count are reasonably optimized.

### Story 15.3 — Aggressive Plan

As a visitor,  
I want an aggressive itinerary,  
so that I can attempt the maximum number of attractions.

### Story 15.4 — Compare plan strategies

As a visitor,  
I want to compare different itinerary strategies,  
so that I can select the one best aligned with my preferences.

---

# Feature 16 — Breaks, Meals, and Visitor Constraints
**Priority: P1**  
**MVP: NO**

The itinerary should eventually represent the visitor's real day rather than only attraction visits.

## User Stories

### Story 16.1 — Add planned meal

As a visitor,  
I want to reserve time for meals,  
so that the itinerary accounts for realistic breaks.

### Story 16.2 — Add break

As a visitor,  
I want to schedule a break,  
so that the system recalculates around my rest time.

### Story 16.3 — Request immediate break

As a visitor,  
I want to tell the system that I need a break now,  
so that the itinerary can adapt immediately.

### Story 16.4 — Maximum walking preference

As a visitor,  
I want to specify that I prefer less walking,  
so that the system favors geographically compact plans.

### Story 16.5 — Accessibility constraints

As a visitor,  
I want accessibility requirements reflected in itinerary generation,  
so that recommendations remain appropriate for my party.

---

# Feature 17 — Personalized Walking Model
**Priority: P2**  
**MVP: NO**

The system can improve estimates by learning how quickly each visitor group moves.

## User Stories

### Story 17.1 — Compare predicted and actual walking time

As the system,  
I want to compare predicted walking durations with actual visitor progress,  
so that I can estimate the group's walking speed.

### Story 17.2 — Adjust walking multiplier

As the optimization engine,  
I want to adapt walking-time estimates to the current visitor group,  
so that future itinerary calculations become more accurate.

### Story 17.3 — Persist visitor walking preference

As a returning visitor,  
I want the system to remember my approximate walking pace,  
so that future itineraries start with better estimates.

---

# Feature 18 — AI Conversational Guide
**Priority: P2**  
**MVP: NO**

The AI layer should interpret natural-language requests and explain system decisions.

## User Stories

### Story 18.1 — Ask what to do next

As a visitor,  
I want to ask the AI what I should do next,  
so that I can interact naturally with the itinerary system.

### Story 18.2 — Modify itinerary using natural language

As a visitor,  
I want to say things like "we want lunch now" or "skip thrill rides for an hour,"  
so that the system can update my itinerary without complex controls.

### Story 18.3 — Ask why a recommendation was made

As a visitor,  
I want to ask why the system recommends an attraction,  
so that I can understand the decision.

### Story 18.4 — Ask park-related questions

As a visitor,  
I want to ask questions about attractions, park areas, and visit logistics,  
so that the same interface serves as my digital park guide.

### Story 18.5 — Convert language into optimizer constraints

As the system,  
I want AI requests translated into deterministic itinerary constraints,  
so that the LLM does not directly control routing decisions.

---

# Feature 19 — Alerts and Notifications
**Priority: P2**  
**MVP: NO**

Notifications can help visitors react without constantly checking the application.

## User Stories

### Story 19.1 — Attraction closure alert

As a visitor,  
I want to be notified when an attraction in my itinerary closes,  
so that I know the plan has changed.

### Story 19.2 — Attraction reopening alert

As a visitor,  
I want to be notified when an important closed attraction reopens,  
so that I can potentially visit it.

### Story 19.3 — Recommendation change alert

As a visitor,  
I want to be notified if the optimal next attraction materially changes,  
so that I can respond quickly.

### Story 19.4 — Queue opportunity alert

As a visitor,  
I want to know when a Must Do attraction has an unusually favorable queue,  
so that I can take advantage of the opportunity.

---

# Feature 20 — Interactive Park Map
**Priority: P2**  
**MVP: NO**

The map becomes a visual representation of the itinerary.

## User Stories

### Story 20.1 — Show attractions on map

As a visitor,  
I want to see attraction locations on a park map,  
so that I understand the park layout.

### Story 20.2 — Highlight next attraction

As a visitor,  
I want the next recommended attraction highlighted,  
so that I know where to go.

### Story 20.3 — Show itinerary route

As a visitor,  
I want to see the planned route between upcoming attractions,  
so that I understand the movement required.

### Story 20.4 — Show nearby alternatives

As a visitor,  
I want nearby alternative attractions visible,  
so that I can manually change plans if desired.

---

# Feature 21 — Authentication and User Accounts
**Priority: P2**  
**MVP: NO, unless required for persistence strategy**

Accounts are useful but should not delay validation of the itinerary engine.

## User Stories

### Story 21.1 — Create account

As a visitor,  
I want to create an account,  
so that I can save visits and preferences.

### Story 21.2 — Sign in

As a returning visitor,  
I want to sign in securely,  
so that I can access my saved data.

### Story 21.3 — View previous visits

As a visitor,  
I want to see previous visit sessions,  
so that I can review my history.

### Story 21.4 — Manage account

As a visitor,  
I want to manage my account details,  
so that my personal information remains current.

---

# Feature 22 — Payments and Visit Passes
**Priority: P2**  
**MVP: NO**

The recommended initial consumer business model is visit-based rather than subscription-based.

Potential products:

- Single-day park pass
- Multi-day trip pass
- Multi-park trip pass

## User Stories

### Story 22.1 — Purchase visit pass

As a visitor,  
I want to purchase access for a park visit,  
so that I can use premium itinerary optimization.

### Story 22.2 — Activate visit entitlement

As the system,  
I want purchased access linked to the correct visit,  
so that premium capabilities are only available to entitled users.

### Story 22.3 — Multi-day trip pass

As a visitor,  
I want to purchase access for several consecutive park days,  
so that I do not need to purchase each visit independently.

### Story 22.4 — Payment history

As a visitor,  
I want to see my previous purchases,  
so that I can understand what access I have purchased.

---

# Feature 23 — Tour Company Organizations
**Priority: P3**  
**MVP: NO**

The product can later expand into a B2B platform for travel agencies and tour operators.

## User Stories

### Story 23.1 — Create organization

As a tour company administrator,  
I want an organization account,  
so that my company can manage visitor sessions.

### Story 23.2 — Manage staff

As an organization administrator,  
I want to invite and manage staff accounts,  
so that employees can manage customers.

### Story 23.3 — Create customer visit

As a tour company employee,  
I want to create a visit session for a customer,  
so that the customer receives a prepared park experience.

### Story 23.4 — Assign visit entitlement

As a tour company employee,  
I want to assign purchased visit access to a customer,  
so that the customer can use the guide during the visit.

### Story 23.5 — Send visitor access link

As a tour company employee,  
I want to send a unique visit link to a customer,  
so that they can access their itinerary without complex setup.

---

# Feature 24 — White-Label Tour Company Experience
**Priority: P3**  
**MVP: NO**

Tour operators may eventually want the visitor experience to appear as part of their own service.

## User Stories

### Story 24.1 — Organization branding

As a tour company administrator,  
I want to configure my logo and branding,  
so that customers recognize the experience as part of my service.

### Story 24.2 — Custom welcome message

As a tour company administrator,  
I want to configure a welcome message,  
so that customers receive company-specific instructions.

### Story 24.3 — Custom visitor instructions

As a tour company employee,  
I want to add meeting points, transportation information, and special instructions,  
so that the digital guide supports the broader tour experience.

---

# Feature 25 — Tour Company Operations Dashboard
**Priority: P3**  
**MVP: NO**

Tour companies may need visibility into active customer visits.

## User Stories

### Story 25.1 — View active visits

As a tour company employee,  
I want to see active customer visits,  
so that I understand which customers are currently using the service.

### Story 25.2 — View customer itinerary progress

As a tour company employee,  
I want to see customer itinerary progress,  
so that I can provide support if necessary.

### Story 25.3 — Override itinerary

As an authorized employee,  
I want to adjust a customer's itinerary,  
so that I can respond to exceptional situations.

### Story 25.4 — Add customer note

As a tour company employee,  
I want to add operational notes to a visit,  
so that staff can coordinate customer support.

---

# Feature 26 — B2B Billing and Usage Management
**Priority: P3**  
**MVP: NO**

Tour companies will require a business-oriented commercial model.

## User Stories

### Story 26.1 — Purchase visit credits

As a tour company,  
I want to purchase a number of visitor sessions,  
so that I can distribute them to customers.

### Story 26.2 — Track usage

As a tour company administrator,  
I want to see how many visit entitlements have been consumed,  
so that I can manage my account.

### Story 26.3 — Subscription plan

As a tour company administrator,  
I want the option of a recurring plan,  
so that frequent usage can be billed predictably.

### Story 26.4 — Usage reporting

As a tour company administrator,  
I want reports on visitor usage,  
so that I can evaluate the value of the platform.

---

# Feature 27 — Analytics and Product Intelligence
**Priority: P3**  
**MVP: NO**

Internal analytics will help evaluate whether the optimizer is actually improving visitor outcomes.

## User Stories

### Story 27.1 — Measure attraction completion rate

As a product owner,  
I want to know how many planned attractions visitors complete,  
so that itinerary effectiveness can be measured.

### Story 27.2 — Measure predicted versus actual itinerary performance

As a product owner,  
I want predicted completion compared with actual results,  
so that optimization quality can be evaluated.

### Story 27.3 — Measure recommendation acceptance

As a product owner,  
I want to know how often visitors follow system recommendations,  
so that recommendation trust can be evaluated.

### Story 27.4 — Measure itinerary replanning frequency

As a product owner,  
I want to know how often itineraries require replanning,  
so that park volatility and algorithm stability can be studied.

---

# Feature 28 — Advanced Park Intelligence
**Priority: P4**  
**MVP: NO**

Once the core itinerary product is validated, additional park information can improve recommendations.

## User Stories

### Story 28.1 — Park operating hours

As the optimization engine,  
I want park hours included in calculations,  
so that the itinerary respects park opening and closing times.

### Story 28.2 — Attraction-specific operating hours

As the optimization engine,  
I want attraction-specific schedules,  
so that attractions are only scheduled when available.

### Story 28.3 — Show schedules

As a visitor,  
I want shows included in itinerary planning,  
so that fixed-time experiences can be incorporated.

### Story 28.4 — Restaurant information

As a visitor,  
I want restaurants represented in the park model,  
so that meal planning can be integrated with the itinerary.

### Story 28.5 — Weather integration

As the optimization engine,  
I want weather conditions included in itinerary decisions,  
so that outdoor and weather-sensitive attractions can be planned more effectively.

---

# Feature 29 — Attraction Favorites and Watchlists
**Priority: P4**  
**MVP: NO**

Users may want to follow specific attractions outside of an active itinerary.

## User Stories

### Story 29.1 — Favorite attraction

As a visitor,  
I want to favorite attractions,  
so that I can quickly access the attractions I care about.

### Story 29.2 — Queue threshold alert

As a visitor,  
I want to define a target queue time for an attraction,  
so that I can be alerted when the wait becomes favorable.

### Story 29.3 — Attraction watchlist

As a visitor,  
I want a watchlist of selected attractions,  
so that I can monitor them independently of the main itinerary.

---

# Feature 30 — Historical Analytics and Data Export
**Priority: P4**  
**MVP: NO**

These features are useful for analysis but should not compete with core visitor value.

## User Stories

### [DONE] Story 30.1 — Historical queue charts

As a user,  
I want to see historical queue patterns,  
so that I can understand typical attraction behavior.

### Story 30.2 — Compare attractions

As a user,  
I want to compare historical wait times across attractions,  
so that I can identify better times to visit them.

### Story 30.3 — Export data

As an authorized user,  
I want to export historical observations to CSV,  
so that I can perform external analysis.

---

# 3. Recommended Development Sequence

The development order should focus on validating the optimizer before adding AI, monetization, or B2B complexity.

## Phase 1 — Data Foundation

1. **Feature 1 — Park and Attraction Catalog** [MVP REQUIRED]
2. **Feature 2 — Live Queue-Time Collection** [MVP REQUIRED]
3. **Feature 3 — Historical Queue Data** [MVP REQUIRED]
4. **Feature 6 — Park Map Graph and Walking-Time Model** [MVP REQUIRED]

Goal:

Build the park intelligence foundation required by every later feature.

---

## Phase 2 — First Usable Optimizer

5. **Feature 4 — Visitor Visit Setup** [MVP REQUIRED]
6. **Feature 5 — Attraction Priority Selection** [MVP REQUIRED]
7. **Feature 7 — Initial Itinerary Optimization Engine** [MVP REQUIRED]
8. **Feature 8 — Visit Session and Itinerary State** [MVP REQUIRED]

Goal:

Allow a visitor to create a visit and receive a realistic optimized itinerary.

At this point the system should already be testable internally.

---

## Phase 3 — Real-Time Visitor Product

9. **Feature 9 — Live "What Should I Do Next?" Experience** [MVP REQUIRED]
10. **Feature 10 — Automatic Itinerary Replanning** [MVP REQUIRED]
11. **Feature 11 — Mobile-First Visitor Interface** [MVP REQUIRED]

Goal:

Create the first complete MVP.

At the end of this phase a visitor should be able to:

```text
Select park
    ↓
Select date and visit times
    ↓
Select attractions
    ↓
Set priorities
    ↓
Generate itinerary
    ↓
Start visit
    ↓
Receive next recommendation
    ↓
Complete or skip attraction
    ↓
Automatically receive updated itinerary
```

---

## Phase 4 — Make the Optimizer Smarter

12. Feature 12 — Queue-Time Prediction
13. Feature 13 — Recommendation Explanations
14. Feature 14 — Completion Probability and Plan Risk
15. Feature 15 — Optimization Modes
16. Feature 16 — Breaks, Meals, and Visitor Constraints
17. Feature 17 — Personalized Walking Model

Goal:

Move from a useful itinerary planner to a sophisticated decision engine.

---

## Phase 5 — AI Experience

18. Feature 18 — AI Conversational Guide
19. Feature 19 — Alerts and Notifications
20. Feature 20 — Interactive Park Map

Goal:

Make the optimizer easier and more natural to use.

The AI should enhance the optimizer rather than replace it.

---

## Phase 6 — Consumer Commercialization

21. Feature 21 — Authentication and User Accounts
22. Feature 22 — Payments and Visit Passes

Goal:

Turn the validated visitor product into a sellable consumer service.

A likely consumer model is:

```text
Free
- Attraction information
- Current queue information
- Basic park browsing

Visit Pass
- Optimized itinerary
- Live replanning
- Queue prediction
- Completion probability
- AI Guide

Possible pricing:
$5–$10 per park day
```

A second option could be:

```text
Trip Pass
$15–$25

Valid for:
- Multiple park days
- Multiple supported parks
- Limited validity window
```

Pricing must ultimately be validated experimentally.

---

## Phase 7 — B2B Expansion

23. Feature 23 — Tour Company Organizations
24. Feature 24 — White-Label Tour Company Experience
25. Feature 25 — Tour Company Operations Dashboard
26. Feature 26 — B2B Billing and Usage Management

Goal:

Turn the consumer itinerary engine into a platform that travel companies can distribute to their customers.

---

## Phase 8 — Optimization, Analytics, and Expansion

27. Feature 27 — Analytics and Product Intelligence
28. Feature 28 — Advanced Park Intelligence
29. Feature 29 — Attraction Favorites and Watchlists
30. Feature 30 — Historical Analytics and Data Export

Goal:

Improve optimization quality, product intelligence, customer retention, and platform breadth.

---

# 4. MVP Scope Summary

The MVP should contain only the following features:

- [x] Feature 1 — Park and Attraction Catalog
- [x] Feature 2 — Live Queue-Time Collection
- [x] Feature 3 — Historical Queue Data
- [x] Feature 4 — Visitor Visit Setup
- [x] Feature 5 — Attraction Priority Selection
- [x] Feature 6 — Park Map Graph and Walking-Time Model
- [x] Feature 7 — Initial Itinerary Optimization Engine
- [x] Feature 8 — Visit Session and Itinerary State
- [x] Feature 9 — Live "What Should I Do Next?" Experience
- [x] Feature 10 — Automatic Itinerary Replanning
- [x] Feature 11 — Mobile-First Visitor Interface

The MVP explicitly does **not** require:

- AI chatbot
- User accounts
- Payments
- Tour company features
- Advanced analytics
- CSV exports
- Favorites
- Queue alerts
- Complex maps
- White-label functionality
- Advanced machine-learning prediction

Queue prediction is extremely valuable, but the first optimizer can initially use:

- current queue times;
- simple historical averages;
- deterministic heuristics.

A more sophisticated forecasting engine can be introduced after the core optimization loop works.

---

# 5. MVP Success Criteria

The MVP should be considered successful only if it can demonstrate that the itinerary engine provides measurable visitor value.

Suggested initial metrics:

### Itinerary Completion Rate

Percentage of Must Do attractions successfully completed.

### Waiting-Time Efficiency

Estimated total waiting time compared with a simple baseline itinerary.

Possible baselines:

- User-selected attraction order
- Nearest-attraction-first
- Shortest-current-queue-first

### Walking Efficiency

Total estimated distance or walking time required by the optimized itinerary.

### Replanning Effectiveness

Whether itinerary recalculation improves the expected visit outcome after closures, queue spikes, or user delays.

### Recommendation Acceptance Rate

Percentage of recommended next actions accepted by visitors.

### Must-Do Success Rate

Percentage of visit sessions where all Must Do attractions were completed.

---

# 6. Core Domain Model — Initial Direction

A possible domain model could eventually include:

```text
Park
Attraction
AttractionLocation
AttractionSchedule
QueueObservation
QueuePrediction
ParkRoute
ParkRouteSegment

Visitor
Visit
VisitPreference
VisitAttractionPreference

Itinerary
ItineraryItem
ItineraryVersion

VisitProgress
AttractionVisit
VisitEvent

Recommendation
RecommendationReason
```

Future B2B entities:

```text
Organization
OrganizationUser
Customer
VisitEntitlement
Subscription
UsageRecord
```

The exact schema should evolve with implementation rather than being fixed prematurely.

---

# 7. Important Technical Principle

The optimization engine and AI layer should remain separate.

The recommended architecture is:

```text
Mobile/Web Application
        |
        v
Application API
        |
        +--------------------------+
        |                          |
        v                          v
Optimization Engine            AI Guide
        |                          |
        |                          |
        +------------+-------------+
                     |
                     v
              Park Intelligence
                     |
         +-----------+-----------+
         |                       |
         v                       v
Current Queue Data       Historical Queue Data
```

The AI should never be the sole authority for itinerary decisions.

For example:

```text
User:
"We're tired and want to stop for 30 minutes."

        ↓

AI Guide:
Intent = AddBreak
Duration = 30 minutes
Preference = Nearby

        ↓

Optimization Engine:
Recalculate itinerary

        ↓

AI Guide:
Explain updated recommendation
```

This keeps the core product:

- deterministic;
- testable;
- explainable;
- cheaper to operate;
- less dependent on a specific LLM provider.

---

# 8. Long-Term Product Differentiation

The strongest long-term differentiation is unlikely to be the chatbot itself.

The defensible value is more likely to come from the combination of:

1. High-quality historical queue data
2. Accurate future queue predictions
3. Reliable park walking models
4. A strong itinerary optimization algorithm
5. Real-time replanning
6. Personalized visitor behavior models
7. Completion-probability modeling
8. Accumulated visit outcome data

Over time, the system could evolve from:

```text
"Space Mountain currently has a 45-minute wait."
```

to:

```text
"Space Mountain currently has a 45-minute wait.

Based on today's conditions and historical behavior, there is a 74% probability
that the wait will fall below 30 minutes between 2:00 PM and 3:00 PM.

Visit Pirates of the Caribbean now instead.

Doing so increases the estimated probability of completing all four of your
Must Do attractions from 78% to 91%."
```

That transition—from displaying information to making high-quality decisions—is the core product opportunity.

---

# 9. Ultimate Product Positioning

The product should eventually be positioned less as a queue tracker and more as:

> **A real-time decision engine for theme park visits.**

Possible product promise:

> Tell us what you want to experience.  
> We will continuously determine the best way to experience it.

The visitor should not need to repeatedly analyze:

- maps;
- queues;
- walking distances;
- future wait patterns;
- park hours;
- attraction closures;
- schedule conflicts.

The platform should continuously convert those inputs into one simple output:

> **Here is what you should do next.**
