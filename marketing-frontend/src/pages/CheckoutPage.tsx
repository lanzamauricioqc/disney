import { useActionState } from "react";
import { useFormStatus } from "react-dom";
import { Link, useSearchParams } from "react-router-dom";
import { PageMeta } from "../components/PageMeta";
import { ApiError, createCheckoutSession } from "../lib/api";
import { getPurchasablePlan } from "../lib/plans";

type State = { error?: string; details?: string };
const initialState: State = {};

function CheckoutButton() {
  const { pending } = useFormStatus();
  return <button className="button button--primary checkout-button" type="submit" disabled={pending}>{pending ? "Opening secure checkout…" : "Continue to checkout"}</button>;
}

export function CheckoutPage() {
  const [params] = useSearchParams();
  const checkoutStatus = params.get("status");
  const plan = getPurchasablePlan(params.get("plan"));
  const checkout = async (_: State, formData: FormData): Promise<State> => {
    if (!plan) return { error: "Select a valid paid plan before continuing." };
    const email = String(formData.get("email") ?? "").trim();
    if (!email) return { error: "Enter your email address." };
    try {
      const result = await createCheckoutSession(email, plan.productCode);
      window.location.assign(result.checkoutUrl);
      return {};
    } catch (error) {
      if (error instanceof ApiError) return { error: error.info.message, details: error.info.details === undefined ? undefined : JSON.stringify(error.info.details, null, 2) };
      return { error: "Checkout could not be started.", details: error instanceof Error ? error.message : String(error) };
    }
  };
  const [state, action] = useActionState(checkout, initialState);

  if (checkoutStatus === "success") {
    return <>
      <PageMeta title="Payment submitted" description="Your Park Pilot payment was submitted." noIndex />
      <section className="not-found"><div className="narrow"><span className="kicker">PAYMENT SUBMITTED</span><h1>Thanks for choosing Park Pilot.</h1><p>Stripe returned you successfully. The billing webhook remains the source of truth while your payment and pass are confirmed.</p><Link className="button button--primary" to="/">Return home</Link></div></section>
    </>;
  }

  if (checkoutStatus === "cancelled") {
    return <>
      <PageMeta title="Checkout cancelled" description="Your Park Pilot checkout was cancelled." noIndex />
      <section className="not-found"><div className="narrow"><span className="kicker">CHECKOUT CANCELLED</span><h1>No payment was made.</h1><p>You can return to pricing whenever you are ready. Your selected pass has not been purchased.</p><Link className="button button--primary" to="/pricing">Return to pricing</Link></div></section>
    </>;
  }

  return <>
    <PageMeta title="Checkout" description="Start Park Pilot pass checkout." noIndex />
    <section className="checkout-section"><div className="container checkout-grid">
      <div><Link className="back-link" to="/pricing">← Back to pricing</Link><span className="kicker">CHECKOUT</span><h1>{plan ? `Get your ${plan.name}` : "Plan not found"}</h1><p>{plan ? "Enter your email to continue to our secure payment partner." : "The selected plan is not available. Return to pricing to choose Visit Pass or Trip Pass."}</p>
        {plan && <form className="checkout-form" action={action}><div className="field"><label htmlFor="checkout-email">Email address</label><input id="checkout-email" name="email" type="email" autoComplete="email" placeholder="you@example.com" required /><small>Your receipt and pass details will be sent here.</small></div><CheckoutButton />
          {state.error && <div className="api-error" role="alert"><strong>Checkout could not start</strong><p>{state.error}</p>{state.details && <details><summary>Backend error details</summary><pre>{state.details}</pre></details>}<p className="error-help">Billing may not be configured in this draft environment. You can retry or return to pricing.</p></div>}
        </form>}
        {!plan && <Link className="button button--primary" to="/pricing">Choose a plan</Link>}
      </div>
      {plan && <aside className="order-card" aria-label="Order summary"><small>ORDER SUMMARY</small><h2>{plan.name}</h2><p>{plan.description}</p><ul>{plan.features.map((feature) => <li key={feature}>{feature}</li>)}</ul><div className="order-total"><span>Draft total</span><strong>{plan.price}</strong></div><p className="fine-print">{plan.cadence}. Introductory draft pricing; final price and terms may change. No recurring subscription.</p></aside>}
    </div></section>
  </>;
}
