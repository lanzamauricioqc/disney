import { useActionState } from "react";
import { useFormStatus } from "react-dom";
import { ApiError, joinWaitlist } from "../lib/api";

type State = { kind: "idle" | "success" | "known" | "error"; message?: string };
const initialState: State = { kind: "idle" };

async function submitWaitlist(_: State, formData: FormData): Promise<State> {
  const email = String(formData.get("email") ?? "").trim();
  if (!email) return { kind: "error", message: "Enter your email address." };
  try {
    const result = await joinWaitlist(email);
    return { kind: result.outcome === "created" ? "success" : "known", message: result.message };
  } catch (error) {
    return { kind: "error", message: error instanceof ApiError ? error.info.message : "Something went wrong. Please try again." };
  }
}

function SubmitButton() {
  const { pending } = useFormStatus();
  return <button className="button button--primary" disabled={pending} type="submit">{pending ? "Joining…" : "Join the waitlist"}</button>;
}

export function WaitlistForm({ compact = false }: { compact?: boolean }) {
  const [state, action] = useActionState(submitWaitlist, initialState);
  return <form className={`waitlist-form ${compact ? "waitlist-form--compact" : ""}`} action={action}>
    <div className="field"><label htmlFor={compact ? "footer-email" : "hero-email"}>Email address</label><input id={compact ? "footer-email" : "hero-email"} name="email" type="email" autoComplete="email" placeholder="you@example.com" required /></div>
    <SubmitButton />
    {state.kind !== "idle" && <p className={`form-message form-message--${state.kind}`} role={state.kind === "error" ? "alert" : "status"}>{state.message}</p>}
  </form>;
}
