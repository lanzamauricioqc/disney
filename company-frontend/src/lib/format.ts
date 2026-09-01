import type { Entity } from "../types";

export function asList(value: unknown, keys: string[] = []): Entity[] {
  if (Array.isArray(value)) return value as Entity[];
  if (value && typeof value === "object") {
    const record = value as Record<string, unknown>;
    for (const key of [...keys, "items", "data", "results"]) if (Array.isArray(record[key])) return record[key] as Entity[];
  }
  return [];
}

export function text(value: unknown, fallback = "—"): string { return typeof value === "string" || typeof value === "number" ? String(value) : fallback; }
export function dateText(value: unknown): string { if (typeof value !== "string" || !value) return "—"; const date = new Date(value); return Number.isNaN(date.valueOf()) ? value : new Intl.DateTimeFormat(undefined, { dateStyle: "medium" }).format(date); }
export function dateTimeText(value: unknown): string { if (typeof value !== "string" || !value) return "—"; const date = new Date(value); return Number.isNaN(date.valueOf()) ? value : new Intl.DateTimeFormat(undefined, { dateStyle: "medium", timeStyle: "short" }).format(date); }
export function initials(value: unknown): string { return text(value, "?").split(/\s+/).slice(0,2).map((part) => part[0]?.toUpperCase()).join(""); }
export function roleAllowsAdjustment(role?: string): boolean { return ["owner", "admin", "administrator"].includes((role ?? "").toLowerCase()); }
