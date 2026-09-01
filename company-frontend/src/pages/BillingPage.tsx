import { useEffect, useState, type FormEvent } from "react";
import { useSearchParams } from "react-router-dom";
import { useAuth } from "../AuthContext";
import { ApiMessage, Empty, ErrorState, Loading, PageHeader, errorText } from "../components/Ui";
import { post } from "../lib/api";
import { asList, dateTimeText, roleAllowsAdjustment, text } from "../lib/format";
import { useApiData } from "../lib/useApiData";

export function BillingPage() {
  const { session } = useAuth();
  const [searchParams] = useSearchParams();
  const credits = useApiData<Record<string, unknown>>("/credits");
  const products = useApiData<unknown>("/billing/products");
  const [message, setMessage] = useState("");
  const [formError, setFormError] = useState("");
  const [pending, setPending] = useState("");

  useEffect(() => {
    const status = searchParams.get("status");
    if (status === "success") {
      setMessage("Stripe returned successfully. Credits appear only after the verified webhook is processed.");
      void credits.refresh();
    } else if (status === "cancelled") {
      setMessage("Checkout was cancelled. No credits were added.");
    }
  }, [searchParams]);

  if (credits.loading) {
    return <Loading label="Loading credits" />;
  }

  if (credits.error) {
    return <ErrorState message={credits.error} retry={credits.refresh} />;
  }

  const balance = credits.data?.balance as Record<string, unknown> | undefined;
  const ledger = asList(credits.data?.entries);
  const bundles = asList(products.data);

  const checkout = async (bundleCode: string) => {
    setPending(bundleCode);
    setFormError("");
    try {
      const result = await post<Record<string, unknown>>(
        "/billing/checkout-sessions",
        { bundleCode }
      );
      const url = text(result.checkoutUrl, "");
      if (!url) {
        throw new Error("Billing service returned no checkout URL.");
      }
      window.location.assign(url);
    } catch (error) {
      setFormError(`${errorText(error)} Billing may not be configured in this environment.`);
    } finally {
      setPending("");
    }
  };

  const adjust = async (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault();
    setPending("adjust");
    setFormError("");
    const form = new FormData(event.currentTarget);
    try {
      await post("/credits/adjustments", {
        amount: Number(form.get("amount")),
        reason: form.get("reason")
      });
      event.currentTarget.reset();
      setMessage("Credit adjustment recorded.");
      void credits.refresh();
    } catch (error) {
      setFormError(errorText(error));
    } finally {
      setPending("");
    }
  };

  return <>
    <PageHeader
      eyebrow="COMMERCIAL ACCESS"
      title="Credits & billing"
      description="Track visit entitlements and manage company purchasing."
    />
    <ApiMessage error={formError} success={message} />
    <section className="credit-hero">
      <div><span>AVAILABLE VISIT CREDITS</span><strong>{text(balance?.remaining, "0")}</strong></div>
      <div><span>CONSUMED</span><strong>{text(balance?.consumed, "0")}</strong></div>
    </section>
    <div className="two-column billing-columns">
      <section className="panel">
        <div className="panel-head"><div><span>DRAFT PURCHASING</span><h2>Credit bundles</h2></div></div>
        {products.loading ? <Loading /> : products.error ? (
          <ErrorState message={products.error} retry={products.refresh} />
        ) : bundles.length ? (
          <div className="bundle-grid">{bundles.map((bundle, index) => {
            const code = text(bundle.code, String(index));
            return <article key={code}>
              <strong>{text(bundle.name, "Credit bundle")}</strong>
              <span>{text(bundle.credits)} visit credits</span>
              {bundle.displayPrice ? <b>{text(bundle.displayPrice)}</b> : <b>Price configured in Stripe</b>}
              <button
                className="btn btn--primary"
                disabled={pending === code}
                onClick={() => void checkout(code)}
              >
                Continue to checkout
              </button>
            </article>;
          })}</div>
        ) : <Empty title="No bundles configured" message="Credit bundles appear after Stripe products are configured." />}
      </section>
      {roleAllowsAdjustment(session?.user.role) && (
        <aside className="panel">
          <div className="panel-head"><div><span>ADMINISTRATOR ONLY</span><h2>Manual adjustment</h2></div></div>
          <form className="form-stack" onSubmit={adjust}>
            <label>Credit amount<input name="amount" type="number" required /></label>
            <label>Reason<textarea name="reason" rows={3} required /></label>
            <button className="btn btn--secondary" disabled={pending === "adjust"}>Record adjustment</button>
          </form>
        </aside>
      )}
    </div>
    <section className="panel below-panel">
      <div className="panel-head"><div><span>AUDITABLE BALANCE</span><h2>Credit ledger</h2></div></div>
      {ledger.length ? (
        <div className="table-scroll">
          <table>
            <thead><tr><th>Date</th><th>Source</th><th>Reason</th><th>Change</th><th>Reference</th></tr></thead>
            <tbody>{ledger.map((entry, index) => (
              <tr key={text(entry.id, String(index))}>
                <td>{dateTimeText(entry.createdAt)}</td>
                <td>{text(entry.source)}</td>
                <td>{text(entry.reason)}</td>
                <td className={Number(entry.amount) >= 0 ? "positive" : "negative"}>
                  {Number(entry.amount) >= 0 ? "+" : ""}{text(entry.amount)}
                </td>
                <td>{text(entry.sourceReference, "-")}</td>
              </tr>
            ))}</tbody>
          </table>
        </div>
      ) : <Empty title="No ledger entries" message="Purchases, usage, and adjustments will appear here." />}
    </section>
  </>;
}
