# Park Pilot Mobile Prototype Design

## Visual thesis

Park Pilot is a usability-first outdoor wayfinder: a bright, high-contrast day
companion that a distracted visitor can read at a glance in direct sun, through
tinted sunglasses, while walking and using one hand. Legibility and a clear next
action come before aesthetics. A calm low-light theme covers evenings and dark
rides without changing the layout or the priority of information.

## Outdoor usability rationale

The real use context, not a showroom, drives every decision:

- Strong sun and glare wash out low-contrast interfaces, so surfaces are solid
  and text sits near the top of the contrast range.
- Common sunglass tints (gray, brown, and polarized) shift and mute color, so
  status uses saturated, dark, high-contrast fills plus a text label, never a
  faint tint or color alone.
- The visitor is walking and distracted, so type is larger, hierarchy is blunt,
  and the single most useful action is the biggest, boldest control on screen.
- One-handed use means the primary next action and the navigation sit inside the
  natural thumb arc at the bottom, and destructive or skip actions are kept away
  from the positive action to prevent mis-taps.
- Reduced cognitive load: one clear recommendation, its cost (wait, walk,
  finish), one reason to trust it, and one primary action, all above the fold.

## Product scope

Static consumer prototype covering:

1. Visit setup
2. Attraction priority selection
3. Generated plan review
4. Live next recommendation
5. Itinerary progress
6. Explore and queue status
7. Complete, skip, alternate choice, and replanning feedback
8. P1 previews for recommendation explanations and completion risk

All content uses fictional park and attraction names. No external assets, fonts,
or network requests are used. All artwork is abstract inline SVG; no castle
silhouettes, characters, or entertainment-company marks appear.

## Content hierarchy

1. Immediate decision: what the visitor should do next
2. Decision cost: queue, walk, total time, and operating state
3. Trust: why now, forecast direction, and plan risk
4. Control: start, complete, skip, choose another option, or replan
5. Context: itinerary progress and later stops
6. Discovery: queue list and attraction details
7. Configuration: visit details, priorities, and display theme

## Information architecture

### Setup flow

- Welcome and visit details (with the theme toggle in the header)
- Attraction priorities
- Generated plan
- Start visit

### Main application

- Next: primary live recommendation (default view)
- Plan: ordered itinerary and progress
- Explore: searchable attraction queue list
- Visit: visit summary, display theme control, and reset

Bottom navigation stays visible in the main application. Every main screen and
the setup header carry the same compact theme toggle. The Visit screen also
exposes the theme as a labeled setting card.

## Themes and tokens

Two deliberate themes share one layout, one type scale, and one set of component
shapes. Only color tokens change. The theme is stored on the root element as
`data-theme="day"` or `data-theme="night"`.

- Sunlight (day) is the default. `color-scheme: light`. Tuned for glare and
  sunglass tints: bright solid canvas, white cards, thick boundaries, saturated
  dark status fills with white text, and a single bold blue accent.
- Low light (night). `color-scheme: dark`. Calm and readable for evenings, still
  leading with hierarchy rather than premium ornament.

Both themes avoid heavy gradients, neon or glow effects, glassmorphism, and
hairline-only boundaries. No blur is used for surfaces; the sticky footer and
bottom navigation are solid. Status is never carried by color alone.

### Sunlight (day) tokens - default

Surfaces:

- `--bg: #e7ecf2` - app canvas, bright and slightly cool to cut glare
- `--surface: #ffffff` - solid card
- `--surface-2: #ffffff` - input field, paired with a strong border
- `--surface-3: #d9e2ee` - hover and chip
- `--line: #b7c2d3` - divider, never the sole boundary of a control
- `--line-strong: #55637c` - control boundary, at least 3:1 on white

Text (on white):

- `--text: #0b1626` - primary, about 16:1
- `--text-2: #303e57` - secondary, about 9:1
- `--text-3: #45536c` - minor labels, about 6.6:1

Accent (single bold blue):

- `--accent: #1550d6` - primary fill
- `--accent-strong: #0f3fb0` - hover and active
- `--on-accent: #ffffff` - text on accent
- `--accent-text: #0f45c0` - accent used as text on light surfaces
- `--accent-soft: #dde8ff` - solid tinted information surface
- `--accent-soft-border: #92b0ef`

Status badges (solid, saturated, white text, dark border):

- OK / low queue: bg `#0b6b30`, fg `#ffffff`, border `#0a5527`
- Warn / medium queue: bg `#a5490a`, fg `#ffffff`, border `#883c07`
- Danger / high queue: bg `#bf2415`, fg `#ffffff`, border `#9a1c0f`
- Neutral: bg `#d9e2ee`, fg `#303e57`, border `#55637c`
- Accent: bg `#dde8ff`, fg `#0f45c0`, border `#92b0ef`

Status surfaces and text for cards and notices:

- OK: surface `#d8eede`, border `#74bd89`, text `#0a5327`
- Warn: surface `#fbe6ce`, border `#dd9f5c`, text `#7f360a`
- Danger: surface `#fbdfdc`, border `#e39289`, text `#99190d`

Hero recommendation (solid, no gradient):

- `--hero-bg: #123fbf`, text `#ffffff`, eyebrow `#cfe0ff`
- chip `#0d3196` with a 34% white border
- CTA `#ffffff` on `#0b1626` (near-black text on white button)

System:

- `--focus: #0b3ba8`
- `--page-bg: #ccd6e4`, `--shell-frame: #ffffff`

### Low light (night) tokens

Surfaces:

- `--bg: #0e1420`, `--surface: #172231`, `--surface-2: #1d293c`,
  `--surface-3: #26344e`
- `--line: #33425e`, `--line-strong: #4c5d7c`

Text:

- `--text: #f2f6fc`, `--text-2: #c4cede`, `--text-3: #9ba7be`

Accent:

- `--accent: #4f7bff`, `--accent-strong: #6d92ff`, `--on-accent: #0a1024`
- `--accent-text: #a6bdff`, `--accent-soft: #172445`,
  `--accent-soft-border: #33518f`

Status badges (calm tinted fills, light text, tinted border):

- OK: bg `rgba(52,211,153,0.18)`, fg `#7fe3b2`, border `rgba(52,211,153,0.40)`
- Warn: bg `rgba(245,176,66,0.18)`, fg `#f7c46a`, border `rgba(245,176,66,0.42)`
- Danger: bg `rgba(248,113,113,0.18)`, fg `#f7a6a0`,
  border `rgba(248,113,113,0.42)`
- Neutral: bg `#26344e`, fg `#c4cede`, border `#4c5d7c`
- Accent: bg `#172445`, fg `#a6bdff`, border `#33518f`

Hero recommendation:

- `--hero-bg: #21315e`, text `#ffffff`, eyebrow `#b9ccff`
- chip `#141f3a` with a 16% white border
- CTA `#eaf1ff` on `#10203f`

System:

- `--focus: #bcd4ff`
- `--page-bg: #070b13`, `--shell-frame: #05070f`

### Status color semantics

Both themes use one consistent traffic-light mapping so the meaning never
changes between themes:

- Low wait / good / done: green
- Medium wait / caution: amber
- High wait / at risk / closed / destructive: red
- Recommended / informational: blue accent
- Later / inactive: neutral gray

Every status also carries a text label or number (for example "12 min", "At
risk", "Closed", "Done"), so a visitor who cannot separate the colors through a
tinted lens still reads the state.

## Theme switching, persistence, and default

### Toggle

- A compact segmented control with two labeled options: a sun icon plus
  "Sunlight" and a moon icon plus "Low light". It is text plus icon, never an
  ambiguous icon-only switch.
- It appears in the setup header and in the header of every main screen (Next,
  Plan, Explore, Visit), and again as an explicit setting card on the Visit
  screen.
- The control is a `role="group"` labeled "Display theme". Each option is a
  button with `aria-pressed` reflecting the active theme and an `aria-label`
  such as "Sunlight theme". Each option target is at least 44 px tall inside a
  56 px control.

### Persistence and default logic

- The choice is stored in `localStorage` under `park-pilot-theme` with the value
  `day` or `night`. This is a separate key from the app state
  (`park-pilot-prototype`), so changing theme never disturbs visit state.
- On load, `resolveTheme()` returns the stored value when present. When there is
  no explicit choice, it returns `day`.
- The system `prefers-color-scheme` is treated only as a weak secondary hint and
  is deliberately not allowed to override the daylight default for new visitors.
  The compelling reason is the primary context: the app is used outdoors in
  daylight, so a new visitor must land on the glare-optimized Sunlight theme
  even on a device set to dark mode. Once the visitor chooses a theme, that
  explicit choice always wins and persists.
- To prevent a wrong-theme flash, a small inline script in the document head
  reads `park-pilot-theme` and sets `data-theme` on the root element before the
  stylesheet loads, defaulting to `day`.

## One-handed and above-the-fold layout

- The Next screen shows the recommendation, its wait, walk, and finish metrics,
  the reason to trust it, and the primary "Head there" action within a 375 x 667
  viewport without scrolling. Validation measured the primary action bottom edge
  at 566 to 584 px, inside the 667 px fold.
- Bottom navigation is fixed and solid, sitting above the safe-area inset, inside
  the natural thumb arc.
- All primary and navigation touch targets are at least 48 px; icon controls are
  at least 44 px.
- On the Next screen the positive path ("Head there", then "Mark complete now")
  is separated from the "Adjust this stop" group (show another option, and a
  clearly styled danger-ghost "Skip this stop"), so a distracted one-handed tap
  cannot easily hit a skip or destructive control by accident.
- Numeric values (waits, walks, counts, times) use tabular figures so scanning
  numbers while moving is steady.

## Typography

- Font stack: `-apple-system, BlinkMacSystemFont, "Segoe UI", "Inter", Roboto,
  Helvetica, Arial, sans-serif`. Modern on Windows (Segoe UI) and macOS, with no
  network dependency.
- Base body size is 17 px, larger than a typical mobile default, to hold up while
  walking.
- Display: clamp 28 to 34, 700. Page title: 24, 700. Section title: 18, 700.
- Body: 17, 400. Supporting: 13 to 15. Metric and hero name: 18 to 30, 700.
- Sentence case and short, direct labels suitable for reading in motion.

## Spacing and shape

- Spacing scale: 4, 8, 12, 16, 20, 24, 30.
- App side padding: 20 on mobile, 24 at 480 and wider.
- Control minimum height: 52 for primary controls, 48 for priority and list
  actions, 44 for icon buttons.
- Primary radius: 16; small radius: 12. Rounding is moderate, not pill-shaped
  cards.
- Full pill shape is reserved for compact status badges, filters, and the
  segmented priority and theme controls.
- Depth is restrained: solid 2 px boundaries carry structure, with one soft card
  shadow and a stronger shadow only on the desktop shell. No glow, no glass.

## Screen specifications

### 1. Welcome and visit setup

- Header row: gradient-free brand chip with an abstract navigation glyph, plus
  the Sunlight / Low light theme toggle.
- Step indicator "Step 1 of 3", eyebrow, heading "Make the most of your park
  day", and a short lead.
- Fields: Visit date, Arrival time, Departure time, Party size stepper. Native
  date and time pickers inherit the active color scheme.
- Primary action "Choose attractions"; secondary text button "Use sample visit".
- Validation appears below the affected field and receives focus after submit.

### 2. Attraction priorities

- Sticky header with back action, title, and "Step 2 of 3".
- Search input labeled "Search attractions".
- Each row: abstract letter avatar, name and fictional land, current queue badge,
  and a three-option segmented control: Must Do, Would Like, Skip.
- Selected states use the accent fill, a check icon, a text label, and
  `aria-pressed`.
- Persistent solid footer summarizes counts and holds "Build my plan".

### 3. Generated plan

- Heading "Your day is ready".
- Summary metrics: attraction count, queue time saved, walking estimate.
- P1 preview: completion confidence on an OK-status surface.
- Ordered timeline with time, attraction, queue, walk, and priority; a high-risk
  stop carries a danger rule and "At risk" text.
- Primary action "Start visit"; secondary "Edit priorities".

### 4. Next recommendation (default main screen)

- Header: eyebrow, current time and park name, a compact live status pill, and
  the theme toggle.
- Progress: "2 of 7 complete" with a labeled progress bar.
- Solid hero panel: eyebrow "Best next stop", attraction name and land, three
  metric chips (queue, walk, estimated finish), and a bold white "Head there"
  button with near-black text.
- Positive follow-up "Mark complete now" sits directly under the hero.
- "Adjust this stop" group below: "Show another option" and a danger-ghost "Skip
  this stop", separated from the positive path.
- Explanation preview "Why now?" on an accent surface with a confidence line.
- Plan-health strip on an OK-status surface.
- Upcoming preview with the next two stops.
- After "Head there", the primary becomes "Mark complete now" and a route status
  reads that the visitor is on the way.

### 5. Plan

- Linear progress and a completed count.
- Ordered semantic list grouped into Completed, Up next, Later, and Skipped.
- Each row shows time, queue, priority, and a status pill: Done, Recommended,
  Later, or Skipped. The current row has an accent rule and wash.
- Skipped rows stay visible with muted text and a "Skipped" status, no
  strikethrough.
- Action "Replan remaining day".

### 6. Explore

- Search input and filter pills: All, Low wait, Must Do, Open now.
- Queue rows show name, land, trend text, and open or closed status.
- Queue badge: Low green, Moderate amber, High red, each with its number or
  "Closed".
- Selecting a row opens an in-shell detail sheet with current wait, forecast,
  walking time, priority, and "Make this next".

### 7. Visit

- Visit date and time range, party size, and priority counts.
- Display theme setting card with the Sunlight / Low light toggle and a hint.
- Static-data disclosure on a tinted surface.
- Action "Restart prototype" using the destructive style.

## Interaction and content states

### Complete

1. Visitor activates "Mark complete now".
2. Short loading state "Updating plan".
3. Success notice naming the completed stop.
4. Progress increments and the next valid attraction becomes the recommendation.

### Skip

1. Visitor activates "Skip this stop".
2. Confirmation sheet explains keeping it for later or removing it today.
3. Actions: "Move later", "Skip today", "Cancel".
4. Replanning notice states what changed.

### Choose another option

- Show two nearby alternatives, each with a one-sentence trade-off.
- Selecting one updates the recommendation and keeps the remaining order stable
  where possible.

### Replanning

- An 850 ms simulated update with an inline spinner and text "Checking queues and
  walking times".
- Result notice names cause and effect.

### Required states

- Default, hover (pointer only), focus, active, disabled, loading, empty, error,
  success, and selected are all specified.
- Focus: a 3 px `--focus` ring with 2 px offset; inputs also show an accent
  emphasis.
- Loading is text plus spinner, never a spinner alone.
- Empty search: "No attractions match these filters."
- Error simulation uses a danger notice with `role="alert"` styling; success uses
  a status notice on an OK surface.
- Selected uses icon, label, color, and ARIA state together.

## Navigation behavior

- Bottom navigation items: Next, Plan, Explore, Visit, each with an inline SVG
  icon and visible text.
- Active state uses accent text, an active marker, and `aria-current="page"`.
- Navigation updates the main view without a page reload.
- Focus moves to the destination page heading (which carries `tabindex="-1"`)
  after a view change.

## Responsive behavior

### Mobile, 320 to 479

- Full-viewport single column. Bottom navigation fixed above the safe area.
- Single-column lists use `grid-template-columns: minmax(0, 1fr)` so long content
  wraps instead of forcing horizontal overflow.
- The detail sheet occupies up to about 88% of viewport height.

### Wide mobile and tablet, 480 to 899

- Slightly larger side padding and two-column metric rows where space permits.

### Desktop, 900 and above

- Center a phone shell and keep the mobile composition rather than expanding into
  a dashboard.
- The page background and shell frame follow the active theme (light neutral in
  Sunlight, near-black in Low light); no external pattern or image.

## Accessibility

- Meets WCAG 2.2 AA from inception. Normal text meets at least 4.5:1 and large
  text and status fills meet at least 3:1 in both themes. Measured lowest normal
  text ratio was 5.9:1 in Sunlight and 5.04:1 in Low light; no element failed.
- Semantic headings, lists, buttons, forms, labels, and landmarks. A skip link
  targets the main content.
- Every inline SVG is `aria-hidden="true"` unless it conveys unique information;
  every icon control has an accessible name.
- Touch targets are at least 44 x 44, and primary and list actions at least 48.
- Confirmation sheets use dialog semantics, an accessible name, initial focus,
  escape to close, and focus return. Non-modal panels do not trap focus.
- Notices use `role="status"`; errors use `role="alert"`. Progress exposes
  current, minimum, maximum, and a text value.
- Status is never color alone; each state also carries text or a number.
- The theme toggle exposes `aria-pressed` on the active option.
- Respects `prefers-reduced-motion`: transitions collapse to about 1 ms and the
  live pulse stops, while state changes remain immediate.

## Motion

- Default transitions about 120 to 160 ms.
- View change: a short fade and small vertical movement.
- Sheet: a translate from the bottom.
- Live status: a slow pulse ring conveying liveness.
- No confetti; completion uses a brief spinner during the simulated update.
- Reduced motion removes non-essential animation.

## Static sample content

Fictional park: "Wonderwood Park".

Fictional lands and attractions:

- Harbor Quarter: Compass Voyage, Tidal Turn
- Canopy Grove: Lantern Flight, Canopy Flyers
- Copper Canyon: Skyline Racers, Canyon Railway
- Moonlit Marsh: Firefly Ferry, Bayou Bounce
- Storybook Square: Clockwork Carousel, Paper Moon Theater

Queue values range from 5 to 55 minutes. Bayou Bounce is a temporary closure and
Lantern Flight carries a rising queue trend.

## Validation results

- Two themes verified: initial `data-theme` is `day` for a new visitor; Sunlight
  option reports `aria-pressed="true"`; switching to Low light updates and
  persists in `localStorage`; restart returns to setup.
- WCAG 2.2 AA contrast: 51 sampled selectors per theme across setup, priorities,
  plan, next, plan list, explore, and visit; 0 failures in both themes with
  translucent night surfaces composited over their real backgrounds.
- No horizontal overflow: page `scrollWidth` equals `clientWidth` at 320, 375,
  and 414 px in both themes. The only horizontal scroller is the intentional
  Explore filter strip, which does not overflow the page.
- Primary action above the fold: "Head there" bottom edge measured at 566 to
  584 px within a 375 x 667 viewport.
- No JavaScript console errors (no SEVERE entries) across the full flow in both
  themes and at 320, 375, 414, and desktop widths.
- All existing static flows and interactions preserved.

Validation used headless Chrome driven by Selenium with a CDP device-metrics
override to force true mobile viewports, because headless Chrome otherwise clamps
the viewport width.

## Quality checklist

- [x] Sunlight (day) is the default theme for new visitors
- [x] Compact, labeled text-plus-icon theme toggle in setup and every main screen
- [x] Theme persisted in localStorage; system preference only a secondary hint
- [x] Day theme uses high luminance contrast, solid surfaces, and strong borders
- [x] Status colors survive glare and common sunglass tints and carry text
- [x] No heavy gradients, neon, glow, glassmorphism, or hairline-only boundaries
- [x] Low-light theme is calm and leads with hierarchy
- [x] Primary next action above the fold at 375 x 667
- [x] Sticky solid bottom navigation within the thumb arc
- [x] Touch targets at least 48 px for primary and list actions
- [x] Destructive and skip actions separated from the positive action
- [x] Clear wait, walk, and finish metrics on the recommendation
- [x] All setup and main flows and interactions preserved
- [x] WCAG 2.2 AA contrast met in both themes
- [x] Reduced motion supported
- [x] No horizontal overflow at 320, 375, and 414 px
- [x] No JavaScript console errors
- [x] No network dependency or copyrighted park artwork is used
