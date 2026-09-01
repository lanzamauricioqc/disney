import type { Plan, PlanCode } from "../types";

export const plans: Plan[] = [
  { code: "free", productCode: "free", name: "Free", price: "$0", cadence: "always", description: "Explore the park before you commit.", features: ["Attraction information", "Current queue times", "Basic park browsing"] },
  { code: "visit-pass", productCode: "visit-pass", name: "Visit Pass", price: "$8", cadence: "per park day", description: "A focused co-pilot for one full park day.", features: ["Optimized itinerary", "Live replanning", "Queue predictions", "Completion probability", "AI Guide"], featured: true },
  { code: "trip-pass", productCode: "trip-pass", name: "Trip Pass", price: "$20", cadence: "per trip", description: "Plan across multiple days and parks.", features: ["Everything in Visit Pass", "Multiple park days", "Cross-day priority planning", "Trip-wide flexibility"] },
];

export function getPurchasablePlan(value: string | null): Plan | undefined {
  const aliases: Record<string, PlanCode> = { visit: "visit-pass", visit_pass: "visit-pass", trip: "trip-pass", trip_pass: "trip-pass" };
  const normalized = value ? aliases[value] ?? value : "visit-pass";
  const plan = plans.find((candidate) => candidate.code === normalized);
  return plan?.code === "free" ? undefined : plan;
}
