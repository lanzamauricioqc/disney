import type { ApiErrorInfo } from "../types";

const BASE = "/api/v1/company";
let unauthorizedHandler: (() => void) | undefined;
export function onUnauthorized(handler: () => void) { unauthorizedHandler = handler; return () => { if (unauthorizedHandler === handler) unauthorizedHandler = undefined; }; }
export class ApiError extends Error { constructor(public info: ApiErrorInfo) { super(info.detail); this.name = "ApiError"; } }

async function parse(response: Response): Promise<unknown> { const raw = await response.text(); if (!raw) return undefined; try { return JSON.parse(raw) as unknown; } catch { return raw; } }
function errorMessage(body: unknown, fallback: string): { title: string; detail: string } {
  if (typeof body === "string") return { title: fallback, detail: body };
  const value = body && typeof body === "object" ? body as Record<string, unknown> : {};
  const errors = value.errors && typeof value.errors === "object" ? Object.values(value.errors as Record<string, unknown>).flat().join(" ") : "";
  return { title: typeof value.title === "string" ? value.title : fallback, detail: typeof value.detail === "string" ? value.detail : typeof value.message === "string" ? value.message : errors || fallback };
}

export async function api<T = unknown>(path: string, options: RequestInit = {}, authenticated = true): Promise<T> {
  const token = sessionStorage.getItem("parkPilotCompanyToken");
  const headers = new Headers(options.headers); headers.set("Accept", "application/json");
  if (options.body && !(options.body instanceof FormData)) headers.set("Content-Type", "application/json");
  if (authenticated && token) headers.set("Authorization", `Bearer ${token}`);
  let response: Response;
  try { response = await fetch(`${BASE}${path}`, { ...options, headers }); } catch (error) { throw new ApiError({ status: 0, title: "Server unavailable", detail: "Could not reach the Park Pilot company API. Confirm the API proxy and server configuration.", body: error instanceof Error ? error.message : error }); }
  const body = await parse(response);
  if (response.status === 401) unauthorizedHandler?.();
  if (!response.ok) { const info = errorMessage(body, `Request failed (${response.status})`); throw new ApiError({ status: response.status, ...info, body }); }
  return body as T;
}

export const get = <T>(path: string) => api<T>(path);
export const post = <T>(path: string, value?: unknown, authenticated = true) => api<T>(path, { method: "POST", body: value === undefined ? undefined : JSON.stringify(value) }, authenticated);
export const put = <T>(path: string, value: unknown) => api<T>(path, { method: "PUT", body: JSON.stringify(value) });
export const patch = <T>(path: string, value: unknown) => api<T>(path, { method: "PATCH", body: JSON.stringify(value) });
export const remove = <T>(path: string) => api<T>(path, { method: "DELETE" });
