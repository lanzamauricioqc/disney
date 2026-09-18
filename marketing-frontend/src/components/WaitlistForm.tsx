import { useActionState } from "react";
import { useFormStatus } from "react-dom";
import { useI18n } from "../i18n";
import { joinWaitlist } from "../lib/api";

type State = { kind: "idle" | "success" | "known" | "error"; message?: string };
const initialState: State = { kind: "idle" };

function SubmitButton() {
  const { pending } = useFormStatus();
  const { t } = useI18n();
  return <button className="button button--primary" disabled={pending} type="submit">{pending ? t("Joining…") : t("Join the waitlist")}</button>;
}

export function WaitlistForm({ compact = false }: { compact?: boolean }) {
  const { t } = useI18n();
  const submitWaitlist = async (_: State, formData: FormData): Promise<State> => {
    const email = String(formData.get("email") ?? "").trim();
    if (!email) return { kind: "error", message: "Enter your email address." };
    if (!/^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(email)) return { kind: "error", message: "Enter a valid email address." };
    try {
      const result = await joinWaitlist(email);
      return { kind: result.outcome === "created" ? "success" : "known", message: result.outcome === "created" ? "You are on the list. We will keep you posted." : "You are already on the list. We will be in touch." };
    } catch {
      return { kind: "error", message: "Something went wrong. Please try again." };
    }
  };
  const [state, action] = useActionState(submitWaitlist, initialState);
  return <form className={`waitlist-form ${compact ? "waitlist-form--compact" : ""}`} action={action} noValidate>
    <div className="field"><label htmlFor={compact ? "footer-email" : "hero-email"}>{t("Email address")}</label><input id={compact ? "footer-email" : "hero-email"} name="email" type="email" autoComplete="email" placeholder={t("you@example.com")} required /></div>
    <SubmitButton />
    {state.kind !== "idle" && <p className={`form-message form-message--${state.kind}`} role={state.kind === "error" ? "alert" : "status"}>{state.message ? t(state.message) : null}</p>}
  </form>;
}