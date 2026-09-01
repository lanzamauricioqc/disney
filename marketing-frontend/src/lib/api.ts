import type { ApiErrorDetails, CheckoutResponse, WaitlistResult } from "../types";

export class ApiError extends Error {
  constructor(public readonly info: ApiErrorDetails) { super(info.message); this.name = "ApiError"; }
}

async function parseBody(response: Response): Promise<unknown> {
  const text = await response.text();
  if (!text) return undefined;
  try { return JSON.parse(text) as unknown; } catch { return text; }
}

function messageFrom(body: unknown, fallback: string): string {
  if (typeof body === "string") return body;
  if (body && typeof body === "object") {
    const value = body as Record<string, unknown>;
    for (const key of ["detail", "message", "title", "error"]) if (typeof value[key] === "string") return value[key] as string;
  }
  return fallback;
}

async function post<T>(path: string, payload: object): Promise<{ body: T; status: number }> {
  let response: Response;
  try {
    response = await fetch(path, { method: "POST", headers: { "Content-Type": "application/json", Accept: "application/json" }, body: JSON.stringify(payload) });
  } catch (error) {
    throw new ApiError({ status: 0, message: "Could not reach the Park Pilot service. Check your connection and try again.", details: error instanceof Error ? error.message : error });
  }
  const body = await parseBody(response);
  if (!response.ok) throw new ApiError({ status: response.status, message: messageFrom(body, `Request failed (${response.status}).`), details: body });
  return { body: body as T, status: response.status };
}

export async function joinWaitlist(email: string): Promise<WaitlistResult> {
  try {
    const { body } = await post<Record<string, unknown>>("/api/v1/waitlist", { email });
    const outcome = body.outcome === "already-registered" || body.status === "already_registered" || body.alreadyRegistered === true ? "already-registered" : "created";
    return { outcome, message: typeof body.message === "string" ? body.message : outcome === "created" ? "You are on the list. We will keep you posted." : "You are already on the list. We will be in touch." };
  } catch (error) {
    if (error instanceof ApiError && (error.info.status === 409 || error.info.status === 422) && /already|registered|exists/i.test(error.info.message)) return { outcome: "already-registered", message: "You are already on the list. We will be in touch." };
    throw error;
  }
}

export async function createCheckoutSession(email: string, productCode: string): Promise<CheckoutResponse> {
  const { body } = await post<CheckoutResponse>("/api/v1/billing/checkout-sessions", { email, productCode });
  if (!body?.checkoutUrl || typeof body.checkoutUrl !== "string") throw new ApiError({ status: 200, message: "The billing service returned no checkout URL.", details: body });
  return body;
}
