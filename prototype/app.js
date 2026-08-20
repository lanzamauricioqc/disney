const attractions = [
  { id: "compass", name: "Compass Voyage", land: "Harbor Quarter", wait: 15, trend: "Holding steady", priority: "would", open: true, walk: 5 },
  { id: "tidal", name: "Tidal Turn", land: "Harbor Quarter", wait: 40, trend: "10 min shorter than usual", priority: "skip", open: true, walk: 7 },
  { id: "lantern", name: "Lantern Flight", land: "Canopy Grove", wait: 20, trend: "Likely 40 min by 11:30", priority: "must", open: true, walk: 6 },
  { id: "canopy", name: "Canopy Flyers", land: "Canopy Grove", wait: 10, trend: "Low for this time", priority: "would", open: true, walk: 4 },
  { id: "skyline", name: "Skyline Racers", land: "Copper Canyon", wait: 35, trend: "Rising quickly", priority: "must", open: true, walk: 8 },
  { id: "canyon", name: "Canyon Railway", land: "Copper Canyon", wait: 55, trend: "Peak demand", priority: "skip", open: true, walk: 9 },
  { id: "firefly", name: "Firefly Ferry", land: "Moonlit Marsh", wait: 5, trend: "Walk-on", priority: "would", open: true, walk: 7 },
  { id: "bayou", name: "Bayou Bounce", land: "Moonlit Marsh", wait: 0, trend: "Temporarily unavailable", priority: "skip", open: false, walk: 8 },
  { id: "carousel", name: "Clockwork Carousel", land: "Storybook Square", wait: 15, trend: "Holding steady", priority: "would", open: true, walk: 5 },
  { id: "theater", name: "Paper Moon Theater", land: "Storybook Square", wait: 25, trend: "Next show at 1:15", priority: "must", open: true, walk: 6 }
];

const icons = {
  compass: '<svg viewBox="0 0 24 24" aria-hidden="true"><path d="M12 3 5 20l7-3.4L19 20 12 3Z"/><path d="M12 3v13.6"/></svg>',
  back: '<svg viewBox="0 0 24 24" aria-hidden="true"><path d="m15 18-6-6 6-6"/></svg>',
  close: '<svg viewBox="0 0 24 24" aria-hidden="true"><path d="m6 6 12 12M18 6 6 18"/></svg>',
  next: '<svg viewBox="0 0 24 24" aria-hidden="true"><path d="m12 3 2.4 5.1L20 10l-4.1 4 .9 5.7-4.8-2.6-4.8 2.6.9-5.7L4 10l5.6-1.9L12 3Z"/></svg>',
  plan: '<svg viewBox="0 0 24 24" aria-hidden="true"><path d="M8 6h12M8 12h12M8 18h12"/><path d="M4 6h.01M4 12h.01M4 18h.01"/></svg>',
  explore: '<svg viewBox="0 0 24 24" aria-hidden="true"><circle cx="11" cy="11" r="7"/><path d="m20 20-4-4"/></svg>',
  visit: '<svg viewBox="0 0 24 24" aria-hidden="true"><circle cx="12" cy="8" r="3"/><path d="M5 20c.8-4 3-6 7-6s6.2 2 7 6"/></svg>',
  sun: '<svg viewBox="0 0 24 24" aria-hidden="true"><circle cx="12" cy="12" r="4"/><path d="M12 2v2M12 20v2M4.9 4.9l1.4 1.4M17.7 17.7l1.4 1.4M2 12h2M20 12h2M4.9 19.1l1.4-1.4M17.7 6.3l1.4-1.4"/></svg>',
  moon: '<svg viewBox="0 0 24 24" aria-hidden="true"><path d="M20 14.5A8 8 0 0 1 9.5 4 7 7 0 1 0 20 14.5Z"/></svg>'
};

const defaultState = {
  phase: "setup",
  setupStep: 1,
  view: "next",
  party: 4,
  visitDate: "2026-08-22",
  arrival: "09:00",
  departure: "20:00",
  priorities: Object.fromEntries(attractions.map((attraction) => [attraction.id, attraction.priority])),
  completed: ["compass", "canopy"],
  skipped: [],
  currentId: "lantern",
  enRoute: false,
  filter: "all",
  search: "",
  notice: "",
  sheet: null,
  loading: false
};

let state = loadState();
const app = document.querySelector("#app");

/* ---------- Theme (Sunlight / Low light) ----------
   Sunlight (day) is the default for every new visitor because the primary
   context is outdoors in strong daylight. The user preference is persisted in
   localStorage under its own key so an inline script in index.html can apply it
   before first paint and avoid a theme flash. */
const THEME_KEY = "park-pilot-theme";
let theme = resolveTheme();
applyTheme(theme);

function resolveTheme() {
  let stored = null;
  try {
    stored = localStorage.getItem(THEME_KEY);
  } catch {
    stored = null;
  }
  if (stored === "day" || stored === "night") return stored;
  // No explicit choice yet. The daylight (Sunlight) theme is the default. The
  // system color-scheme is only a secondary hint and is intentionally not
  // allowed to override the outdoor-first default for new visitors.
  return "day";
}

function applyTheme(next) {
  theme = next;
  document.documentElement.setAttribute("data-theme", next);
}

function setTheme(next) {
  if (next !== "day" && next !== "night") return;
  applyTheme(next);
  try {
    localStorage.setItem(THEME_KEY, next);
  } catch {
    /* storage unavailable, keep in-memory preference */
  }
  render();
}

function loadState() {
  try {
    const saved = JSON.parse(localStorage.getItem("park-pilot-prototype"));
    return saved ? { ...defaultState, ...saved, sheet: null, loading: false } : structuredClone(defaultState);
  } catch {
    return structuredClone(defaultState);
  }
}

function saveState() {
  const persistent = { ...state, sheet: null, loading: false, notice: "" };
  localStorage.setItem("park-pilot-prototype", JSON.stringify(persistent));
}

function update(patch, shouldSave = true) {
  state = { ...state, ...patch };
  if (shouldSave) saveState();
  render();
}

function priorityLabel(priority) {
  return priority === "must" ? "Must Do" : priority === "would" ? "Would Like" : "Skip";
}

function selectedAttractions() {
  return attractions.filter((attraction) => state.priorities[attraction.id] !== "skip" && attraction.open);
}

function plannedAttractions() {
  const selected = selectedAttractions();
  return [
    ...state.completed.map((id) => selected.find((item) => item.id === id)).filter(Boolean),
    ...selected.filter((item) => !state.completed.includes(item.id) && !state.skipped.includes(item.id))
  ];
}

function currentAttraction() {
  const available = selectedAttractions().filter(
    (item) => !state.completed.includes(item.id) && !state.skipped.includes(item.id)
  );
  return attractions.find((item) => item.id === state.currentId) || available[0] || attractions[0];
}

function waitClass(wait) {
  if (wait <= 15) return "wait-low";
  if (wait <= 35) return "wait-medium";
  return "wait-high";
}

function waitLabel(attraction) {
  if (!attraction.open) return "Closed";
  return `${attraction.wait} min`;
}

function progress() {
  const total = selectedAttractions().length;
  return { complete: state.completed.filter((id) => selectedAttractions().some((item) => item.id === id)).length, total };
}

function formatVisitDate(value) {
  const date = new Date(`${value}T12:00:00`);
  return new Intl.DateTimeFormat("en-US", { weekday: "short", month: "short", day: "numeric" }).format(date);
}

function brand() {
  return `<div class="brand"><span class="brand-mark">${icons.compass}</span><span>Park Pilot</span></div>`;
}

function stepLine(step) {
  return `<div class="step-line"><div class="step-dots" aria-hidden="true">
    ${[1, 2, 3].map((item) => `<span class="step-dot ${item === step ? "active" : ""}"></span>`).join("")}
  </div><span>Step ${step} of 3</span></div>`;
}

function bottomNav() {
  const items = [
    ["next", "Next", icons.next],
    ["plan", "Plan", icons.plan],
    ["explore", "Explore", icons.explore],
    ["visit", "Visit", icons.visit]
  ];
  return `<nav class="bottom-nav" aria-label="Main navigation">
    ${items.map(([id, label, icon]) => `
      <button class="nav-button ${state.view === id ? "active" : ""}" data-view="${id}" ${state.view === id ? 'aria-current="page"' : ""}>
        <span class="nav-icon">${icon}</span><span>${label}</span>
      </button>`).join("")}
  </nav>`;
}

function themeToggle() {
  const options = [
    ["day", "Sunlight", icons.sun],
    ["night", "Low light", icons.moon]
  ];
  return `<div class="theme-toggle" role="group" aria-label="Display theme">
    ${options.map(([id, label, icon]) => `
      <button type="button" class="theme-option" data-theme-set="${id}" aria-pressed="${theme === id}" aria-label="${label} theme">
        <span class="theme-ico" aria-hidden="true">${icon}</span><span>${label}</span>
      </button>`).join("")}
  </div>`;
}

function mainHeader(eyebrow, title, statusHtml) {
  return `<header class="main-header">
    <div class="main-header-top">
      <p class="eyebrow">${eyebrow}</p>
      ${themeToggle()}
    </div>
    <div class="main-header-main">
      <h1 tabindex="-1">${title}</h1>
      ${statusHtml || ""}
    </div>
  </header>`;
}

function setupScreen() {
  return `<main id="app-main" class="screen setup-screen">
    <div class="setup-top">
      ${brand()}
      ${themeToggle()}
    </div>
    ${stepLine(1)}
    <p class="eyebrow">Your day, intelligently routed</p>
    <h1 tabindex="-1">Make the most of your park day</h1>
    <p class="lead">A live plan that adapts as queues change, so your group always knows the best next move.</p>
    <form id="setup-form" class="form-grid">
      <label class="field"><span>Visit date</span><input name="visitDate" type="date" value="${state.visitDate}" required /></label>
      <div class="button-row">
        <label class="field"><span>Arrival</span><input name="arrival" type="time" value="${state.arrival}" required /></label>
        <label class="field"><span>Departure</span><input name="departure" type="time" value="${state.departure}" required /></label>
      </div>
      <div class="field">
        <span id="party-label">Party size</span>
        <div class="party-control" aria-labelledby="party-label">
          <button type="button" data-party="-1" aria-label="Decrease party size">−</button>
          <output aria-live="polite">${state.party} people</output>
          <button type="button" data-party="1" aria-label="Increase party size">+</button>
        </div>
      </div>
      <button class="primary-button" type="submit">Choose attractions</button>
    </form>
    <button class="text-button" type="button" data-action="sample">Use sample visit</button>
  </main>`;
}

function prioritiesScreen() {
  const counts = countPriorities();
  return `<main id="app-main" class="screen">
    <header class="topbar">
      <button class="back-button icon" data-action="back-setup" aria-label="Back to visit details">${icons.back}</button>
      <div class="topbar-copy"><h1>Choose priorities</h1><p>Tell us what matters most</p></div>
      <span class="meta">2 of 3</span>
    </header>
    ${stepLine(2)}
    <label class="field">
      <span>Search attractions</span>
      <input class="search" id="priority-search" type="search" placeholder="Search by name or land" value="${state.search}" />
    </label>
    <ul class="attraction-list" aria-label="Attractions">
      ${filteredPriorityAttractions().map(attractionPriorityCard).join("") || '<li class="empty-state">No attractions match your search.</li>'}
    </ul>
    <div class="sticky-actions">
      <div class="selection-summary"><span>${counts.must} Must Do</span><span>${counts.would} Would Like</span><span>${counts.skip} Skip</span></div>
      <button class="primary-button" data-action="build-plan" style="width:100%">Build my plan</button>
    </div>
  </main>`;
}

function filteredPriorityAttractions() {
  const query = state.search.trim().toLowerCase();
  return attractions.filter((item) => !query || `${item.name} ${item.land}`.toLowerCase().includes(query));
}

function attractionPriorityCard(attraction) {
  const priority = state.priorities[attraction.id];
  return `<li class="attraction-card">
    <div class="attraction-head">
      <span class="attraction-symbol" aria-hidden="true">${attraction.name.charAt(0)}</span>
      <div class="attraction-copy"><strong>${attraction.name}</strong><span>${attraction.land}</span></div>
      <span class="wait-badge ${waitClass(attraction.wait)}">${waitLabel(attraction)}</span>
    </div>
    <div class="priority-control" aria-label="${attraction.name} priority">
      ${["must", "would", "skip"].map((option) => `
        <button data-priority="${attraction.id}:${option}" aria-pressed="${priority === option}">
          ${priority === option ? "✓ " : ""}${priorityLabel(option)}
        </button>`).join("")}
    </div>
  </li>`;
}

function countPriorities() {
  return Object.values(state.priorities).reduce(
    (counts, priority) => ({ ...counts, [priority]: counts[priority] + 1 }),
    { must: 0, would: 0, skip: 0 }
  );
}

function planReadyScreen() {
  const plan = selectedAttractions();
  return `<main id="app-main" class="screen">
    <header class="topbar">
      <button class="back-button icon" data-action="back-priorities" aria-label="Back to attractions">${icons.back}</button>
      <div class="topbar-copy"><h1>Your day is ready</h1><p>Optimized for live conditions</p></div>
      <span class="status-pill status-good">Ready</span>
    </header>
    ${stepLine(3)}
    <div class="summary-grid">
      <div class="metric"><strong>${plan.length}</strong><span>planned stops</span></div>
      <div class="metric"><strong>74m</strong><span>queue time saved</span></div>
      <div class="metric"><strong>48m</strong><span>estimated walking</span></div>
    </div>
    <div class="health-card"><strong>87% chance to complete all Must Do stops</strong><span>Good plan health with one afternoon risk</span></div>
    <ol class="timeline">
      ${plan.map((item, index) => {
        const time = ["9:15", "10:00", "10:45", "11:35", "12:20", "1:15", "2:10"][index] || `${2 + index}:30`;
        const risk = item.id === "theater";
        return `<li class="timeline-item">
          <span class="timeline-time">${time}</span>
          <div class="timeline-card ${risk ? "risk" : ""}">
            <strong>${item.name}</strong>
            <p>${item.wait} min queue · ${item.walk} min walk · ${priorityLabel(state.priorities[item.id])}${risk ? " · At risk" : ""}</p>
          </div>
        </li>`;
      }).join("")}
    </ol>
    <div class="button-stack" style="margin-top:22px">
      <button class="primary-button" data-action="start-visit">Start visit</button>
      <button class="ghost-button" data-action="back-priorities">Edit priorities</button>
    </div>
  </main>`;
}

function mainLayout(content) {
  return `${content}${bottomNav()}${state.sheet ? sheet() : ""}`;
}

function nextScreen() {
  const current = currentAttraction();
  const status = progress();
  const percent = Math.round((status.complete / Math.max(status.total, 1)) * 100);
  const remaining = selectedAttractions().filter(
    (item) => item.id !== current.id && !state.completed.includes(item.id) && !state.skipped.includes(item.id)
  );
  const time = new Date().toLocaleTimeString([], { hour: "numeric", minute: "2-digit" });
  const livePill = '<span class="live-pill"><span class="live-dot"></span>Live plan</span>';
  return mainLayout(`<main id="app-main" class="screen">
    ${mainHeader(time, "Wonderwood Park", livePill)}
    ${state.notice ? `<div class="notice" role="status">${state.notice}</div>` : ""}
    <div class="progress-header"><span>${status.complete} of ${status.total} complete</span><span>${percent}%</span></div>
    <div class="progress-track" role="progressbar" aria-valuemin="0" aria-valuemax="${status.total}" aria-valuenow="${status.complete}" aria-valuetext="${status.complete} of ${status.total} attractions complete">
      <div class="progress-fill" style="width:${percent}%"></div>
    </div>
    <section class="recommendation" aria-labelledby="recommendation-title">
      <p class="eyebrow">Best next stop</p>
      <h2 id="recommendation-title">${current.name}</h2>
      <p class="land">${current.land}</p>
      <div class="recommendation-metrics">
        <div class="recommendation-metric"><strong>${current.wait}m</strong><span>current queue</span></div>
        <div class="recommendation-metric"><strong>${current.walk}m</strong><span>walk from here</span></div>
        <div class="recommendation-metric"><strong>${current.wait + current.walk + 8}m</strong><span>estimated finish</span></div>
      </div>
      ${state.enRoute ? `<p class="route-status"><span class="live-dot" aria-hidden="true"></span>On your way, about ${current.walk} min</p>` : ""}
      <button class="primary-button" data-action="${state.enRoute ? "complete" : "head-there"}" ${state.loading ? "disabled" : ""}>
        ${state.loading ? '<span class="loading"><span class="spinner"></span>Updating plan</span>' : state.enRoute ? "Mark complete" : "Head there"}
      </button>
    </section>
    ${state.enRoute ? "" : `<button class="secondary-button confirm-complete" data-action="complete" ${state.loading ? "disabled" : ""}>${state.loading ? '<span class="loading"><span class="spinner"></span>Updating plan</span>' : "Mark complete now"}</button>`}
    <section class="explanation-card">
      <h3>Why now?</h3>
      <p>The queue is ${current.wait} min and is likely to reach ${Math.max(current.wait + 20, 40)} min soon. Visiting now protects a Must Do later.</p>
      <span class="confidence">High confidence · Based on current and typical queues</span>
    </section>
    <div class="health-card"><strong>Plan health: Good</strong><span>All ${countPriorities().must} Must Do stops remain likely</span></div>
    <p class="section-label" id="adjust-label">Adjust this stop</p>
    <div class="button-stack adjust-actions" role="group" aria-labelledby="adjust-label">
      <button class="ghost-button" data-action="alternatives" ${state.loading ? "disabled" : ""}>Show another option</button>
      <button class="ghost-button danger-ghost" data-action="skip" ${state.loading ? "disabled" : ""}>Skip this stop</button>
    </div>
    <div class="section-heading"><h3>Coming up</h3><button class="text-button" data-view="plan">View plan</button></div>
    <div>${remaining.slice(0, 2).map((item, index) => `
      <div class="mini-stop"><span class="number">${index + 2}</span><div><strong>${item.name}</strong><span>${item.land} · ${item.walk} min walk</span></div><span class="wait-badge ${waitClass(item.wait)}">${item.wait} min</span></div>
    `).join("") || '<div class="empty-state">This is your final planned stop.</div>'}</div>
  </main>`);
}

function planScreen() {
  const current = currentAttraction();
  const selected = selectedAttractions();
  const completed = selected.filter((item) => state.completed.includes(item.id));
  const later = selected.filter((item) => item.id !== current.id && !state.completed.includes(item.id));
  return mainLayout(`<main id="app-main" class="screen">
    ${mainHeader("Today", "Your plan", `<span class="status-pill status-good">${progress().complete}/${progress().total} done</span>`)}
    ${state.notice ? `<div class="notice" role="status">${state.notice}</div>` : ""}
    ${planGroup("Completed", completed, "completed")}
    ${planGroup("Up next", [current], "current")}
    ${planGroup("Later", later, "")}
    ${state.skipped.length ? planGroup("Skipped", attractions.filter((item) => state.skipped.includes(item.id)), "skipped") : ""}
    <button class="secondary-button full" data-action="replan" style="margin-top:20px">${state.loading ? '<span class="loading"><span class="spinner"></span>Checking queues and walking times</span>' : "Replan remaining day"}</button>
  </main>`);
}

function planGroup(title, items, rowClass) {
  if (!items.length) return "";
  const pillClass = rowClass === "completed" ? "status-good" : rowClass === "current" ? "status-accent" : "status-neutral";
  const pillText = rowClass === "current" ? "Recommended" : rowClass === "completed" ? "Done" : rowClass === "skipped" ? "Skipped" : "Later";
  return `<h2 class="group-title">${title}</h2><ul class="plan-list">
    ${items.map((item, index) => `<li class="plan-row ${rowClass}">
      <span class="plan-time">${["9:15", "10:00", "10:45", "11:35", "1:15"][index] || "Later"}</span>
      <div class="plan-copy"><strong>${item.name}</strong><span>${item.land} · ${item.wait} min queue · ${priorityLabel(state.priorities[item.id])}</span></div>
      <span class="status-pill ${pillClass}">${pillText}</span>
    </li>`).join("")}
  </ul>`;
}

function exploreScreen() {
  const items = filteredExploreAttractions();
  return mainLayout(`<main id="app-main" class="screen">
    ${mainHeader("Park queues", "Explore", '<span class="live-pill"><span class="live-dot"></span>Updated now</span>')}
    <label class="field"><span>Search attractions</span><input id="explore-search" class="search" type="search" placeholder="Name or land" value="${state.search}" /></label>
    <div class="filters" aria-label="Attraction filters">
      ${[["all", "All"], ["low", "Low wait"], ["must", "Must Do"], ["open", "Open now"]].map(([id, label]) => `<button class="filter-button" data-filter="${id}" aria-pressed="${state.filter === id}">${label}</button>`).join("")}
    </div>
    <ul class="queue-list">
      ${items.map((item) => `<li><button class="queue-row ${item.open ? "" : "closed-row"}" data-detail="${item.id}">
        <span class="attraction-symbol" aria-hidden="true">${item.name.charAt(0)}</span>
        <span class="queue-copy"><strong>${item.name}</strong><span>${item.land}</span><small class="trend">${item.trend}</small></span>
        <span class="wait-badge ${item.open ? waitClass(item.wait) : "wait-high"}">${waitLabel(item)}</span>
      </button></li>`).join("") || '<li class="empty-state">No attractions match these filters.</li>'}
    </ul>
  </main>`);
}

function filteredExploreAttractions() {
  const query = state.search.trim().toLowerCase();
  return attractions.filter((item) => {
    const matchesSearch = !query || `${item.name} ${item.land}`.toLowerCase().includes(query);
    const matchesFilter =
      state.filter === "all" ||
      (state.filter === "low" && item.open && item.wait <= 15) ||
      (state.filter === "must" && state.priorities[item.id] === "must") ||
      (state.filter === "open" && item.open);
    return matchesSearch && matchesFilter;
  });
}

function visitScreen() {
  const counts = countPriorities();
  return mainLayout(`<main id="app-main" class="screen">
    ${mainHeader("Visit details", "Your park day", '<span class="status-pill status-accent">In progress</span>')}
    <div class="visit-card">
      <dl class="detail-list">
        <div><dt>Park</dt><dd>Wonderwood Park</dd></div>
        <div><dt>Date</dt><dd>${formatVisitDate(state.visitDate)}</dd></div>
        <div><dt>Visit hours</dt><dd>${state.arrival}–${state.departure}</dd></div>
        <div><dt>Party</dt><dd>${state.party} people</dd></div>
        <div><dt>Priorities</dt><dd>${counts.must} Must Do · ${counts.would} Would Like</dd></div>
        <div><dt>Progress</dt><dd>${progress().complete} of ${progress().total}</dd></div>
      </dl>
    </div>
    <div class="setting-card">
      <span class="field-label">Display theme</span>
      <p class="setting-hint">Sunlight is tuned for bright outdoor use. Low light suits evenings and dim spaces.</p>
      ${themeToggle()}
    </div>
    <div class="disclosure"><strong>Prototype mode</strong><br />All attractions, queue times, forecasts, and itinerary updates are static sample data designed to demonstrate the product experience.</div>
    <button class="danger-button full" data-action="restart">Restart prototype</button>
  </main>`);
}

function sheet() {
  if (state.sheet.type === "skip") {
    const current = currentAttraction();
    return sheetShell("Change this stop", `<p class="sheet-copy">You can keep ${current.name} in the plan for later or remove it for today.</p>
      <div class="button-stack">
        <button class="secondary-button" data-action="move-later">Move later</button>
        <button class="danger-button" data-action="skip-today">Skip today</button>
        <button class="ghost-button" data-action="close-sheet">Cancel</button>
      </div>`);
  }

  if (state.sheet.type === "alternatives") {
    const current = currentAttraction();
    const options = selectedAttractions().filter((item) => item.id !== current.id && !state.completed.includes(item.id)).slice(0, 2);
    return sheetShell("Choose another option", `<p class="sheet-copy">These choices keep the rest of your plan stable.</p>
      <div class="button-stack">${options.map((item) => `<button class="queue-row" data-alternative="${item.id}">
        <span class="attraction-symbol" aria-hidden="true">${item.name.charAt(0)}</span>
        <span class="queue-copy"><strong>${item.name}</strong><span>${item.wait <= current.wait ? "Shorter queue, slightly more walking" : "Longer queue, closer to your next stop"}</span></span>
        <span class="wait-badge ${waitClass(item.wait)}">${item.wait} min</span>
      </button>`).join("")}</div>`);
  }

  const attraction = attractions.find((item) => item.id === state.sheet.id);
  return sheetShell(attraction.name, `<p class="meta">${attraction.land} · ${priorityLabel(state.priorities[attraction.id])}</p>
    <div class="sheet-metrics">
      <div class="metric"><strong>${waitLabel(attraction)}</strong><span>current wait</span></div>
      <div class="metric"><strong>${attraction.open ? `+${attraction.wait >= 30 ? 10 : 20}m` : "—"}</strong><span>forecast in 1h</span></div>
      <div class="metric"><strong>${attraction.walk}m</strong><span>walk from here</span></div>
    </div>
    <p class="sheet-copy">${attraction.trend}. ${attraction.open ? "This attraction can be added as your next stop without changing completed items." : "The optimizer will avoid this attraction until it reopens."}</p>
    <button class="primary-button" data-make-next="${attraction.id}" style="width:100%" ${attraction.open ? "" : "disabled"}>Make this next</button>`);
}

function sheetShell(title, body) {
  return `<div class="sheet-backdrop" data-action="backdrop">
    <section class="sheet" role="dialog" aria-modal="true" aria-labelledby="sheet-title">
      <div class="sheet-handle" aria-hidden="true"></div>
      <button class="icon-button icon sheet-close" data-action="close-sheet" aria-label="Close">${icons.close}</button>
      <h2 id="sheet-title">${title}</h2>
      ${body}
    </section>
  </div>`;
}

function render() {
  if (state.phase === "setup") app.innerHTML = setupScreen();
  else if (state.phase === "priorities") app.innerHTML = prioritiesScreen();
  else if (state.phase === "ready") app.innerHTML = planReadyScreen();
  else if (state.view === "next") app.innerHTML = nextScreen();
  else if (state.view === "plan") app.innerHTML = planScreen();
  else if (state.view === "explore") app.innerHTML = exploreScreen();
  else app.innerHTML = visitScreen();

  if (state.sheet) {
    requestAnimationFrame(() => document.querySelector(".sheet-close")?.focus());
  }
}

function simulateUpdate(message, callback) {
  update({ loading: true, sheet: null, notice: "" }, false);
  window.setTimeout(() => {
    callback();
    update({ loading: false, notice: message });
  }, 850);
}

app.addEventListener("submit", (event) => {
  if (event.target.id !== "setup-form") return;
  event.preventDefault();
  const form = new FormData(event.target);
  update({
    visitDate: form.get("visitDate"),
    arrival: form.get("arrival"),
    departure: form.get("departure"),
    phase: "priorities",
    search: ""
  });
});

app.addEventListener("input", (event) => {
  if (event.target.id === "priority-search" || event.target.id === "explore-search") {
    state.search = event.target.value;
    saveState();
    render();
    const input = document.querySelector(`#${event.target.id}`);
    input?.focus();
    input?.setSelectionRange(state.search.length, state.search.length);
  }
});

app.addEventListener("click", (event) => {
  const target = event.target.closest("button, [data-action='backdrop']");
  if (!target) return;

  if (target.dataset.themeSet) {
    setTheme(target.dataset.themeSet);
    return;
  }

  if (target.dataset.party) {
    update({ party: Math.min(12, Math.max(1, state.party + Number(target.dataset.party))) });
    return;
  }

  if (target.dataset.priority) {
    const [id, priority] = target.dataset.priority.split(":");
    update({ priorities: { ...state.priorities, [id]: priority } });
    return;
  }

  if (target.dataset.view) {
    update({ view: target.dataset.view, search: "", notice: "", sheet: null });
    requestAnimationFrame(() => document.querySelector("h1")?.focus({ preventScroll: true }));
    return;
  }

  if (target.dataset.filter) {
    update({ filter: target.dataset.filter });
    return;
  }

  if (target.dataset.detail) {
    update({ sheet: { type: "detail", id: target.dataset.detail } }, false);
    return;
  }

  if (target.dataset.alternative) {
    const attraction = attractions.find((item) => item.id === target.dataset.alternative);
    update({ currentId: attraction.id, sheet: null, enRoute: false, notice: `${attraction.name} is now your next stop. The remaining order stayed the same.` });
    return;
  }

  if (target.dataset.makeNext) {
    const attraction = attractions.find((item) => item.id === target.dataset.makeNext);
    update({ currentId: attraction.id, view: "next", sheet: null, enRoute: false, notice: `${attraction.name} is now your next recommendation.` });
    return;
  }

  const action = target.dataset.action;
  if (!action) return;

  if (action === "sample") update({ phase: "priorities", search: "" });
  if (action === "back-setup") update({ phase: "setup" });
  if (action === "back-priorities") update({ phase: "priorities" });
  if (action === "build-plan") update({ phase: "ready", search: "" });
  if (action === "start-visit") update({ phase: "active", view: "next", notice: "Your live plan has started. Queue conditions are up to date." });
  if (action === "head-there") update({ enRoute: true, notice: "" });
  if (action === "skip") update({ sheet: { type: "skip" } }, false);
  if (action === "alternatives") update({ sheet: { type: "alternatives" } }, false);
  if (action === "close-sheet") update({ sheet: null }, false);
  if (action === "backdrop" && event.target.classList.contains("sheet-backdrop")) update({ sheet: null }, false);

  if (action === "complete") {
    const current = currentAttraction();
    const next = selectedAttractions().find(
      (item) => item.id !== current.id && !state.completed.includes(item.id) && !state.skipped.includes(item.id)
    );
    simulateUpdate(`${current.name} completed. Your plan was updated.`, () => {
      state.completed = [...new Set([...state.completed, current.id])];
      state.currentId = next?.id || current.id;
      state.enRoute = false;
    });
  }

  if (action === "move-later") {
    const current = currentAttraction();
    const next = selectedAttractions().find(
      (item) => item.id !== current.id && !state.completed.includes(item.id) && !state.skipped.includes(item.id)
    );
    simulateUpdate(`${current.name} moved later. ${next?.name || "Your next stop"} now comes first.`, () => {
      state.currentId = next?.id || current.id;
      state.enRoute = false;
    });
  }

  if (action === "skip-today") {
    const current = currentAttraction();
    const next = selectedAttractions().find(
      (item) => item.id !== current.id && !state.completed.includes(item.id) && !state.skipped.includes(item.id)
    );
    simulateUpdate(`${current.name} was skipped. The remaining time was reassigned.`, () => {
      state.skipped = [...new Set([...state.skipped, current.id])];
      state.currentId = next?.id || current.id;
      state.enRoute = false;
    });
  }

  if (action === "replan") {
    simulateUpdate("Skyline Racers rose to 55 min, so Clockwork Carousel moved ahead.", () => {
      state.currentId = "carousel";
    });
  }

  if (action === "restart") {
    localStorage.removeItem("park-pilot-prototype");
    state = structuredClone(defaultState);
    render();
  }
});

document.addEventListener("keydown", (event) => {
  if (event.key === "Escape" && state.sheet) update({ sheet: null }, false);
});

render();
