export type PlanCode = "visit-pass" | "trip-pass";

export interface Plan {
  code: "free" | PlanCode;
  productCode: string;
  name: string;
  price: string;
  cadence: string;
  description: string;
  features: string[];
  featured?: boolean;
}

export interface ApiErrorDetails {
  status: number;
  message: string;
  details?: unknown;
}

export interface CheckoutResponse {
  checkoutUrl: string;
}

export interface WaitlistResult {
  outcome: "created" | "already-registered";
  message: string;
}
