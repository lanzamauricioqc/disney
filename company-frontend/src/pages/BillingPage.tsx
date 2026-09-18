import { useEffect, useState, type FormEvent } from "react";
import { useSearchParams } from "react-router-dom";
import { useAuth } from "../AuthContext";
import { ApiMessage, Empty, ErrorState, Loading, PageHeader, errorText } from "../components/Ui";
import { useI18n } from "../i18n";
import { post } from "../lib/api";
import { asList, dateTimeText, roleAllowsAdjustment, text } from "../lib/format";
import { useApiData } from "../lib/useApiData";

export function BillingPage() {
  const { t, locale, number, currency } = useI18n();
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
      setMessage(t("Stripe returned successfully. Credits appear only after the verified webhook is processed."));
      void credits.refresh();
    } else if (status === "cancelled") {
      setMessage(t("Checkout was cancelled. No credits were added."));
    }
  }, [searchParams]);

  if (credits.loading) {
    return <Loading label={t("Loading credits")} />;
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
        throw new Error(t("Billing service returned no checkout URL."));
      }
      window.location.assign(url);
    } catch (error) {
      setFormError(`${errorText(error, t)} ${t("Billing may not be configured in this environment.")}`);
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
      setMessage(t("Credit adjustment recorded."));
      void credits.refresh();
    } catch (error) {
      setFormError(errorText(error, t));
    } finally {
      setPending("");
    }
  };

  return <>
    <PageHeader
      eyebrow={t("COMMERCIAL ACCESS")}
      title={t("Credits & billing")}
      description={t("Track visit entitlements and manage company purchasing.")}
    />
    <ApiMessage error={formError} success={message} />
    <section className="credit-hero">
      <div><span>{t("AVAILABLE VISIT CREDITS")}</span><strong>{number(Number(balance?.remaining ?? 0))}</strong></div>
      <div><span>{t("CONSUMED")}</span><strong>{number(Number(balance?.consumed ?? 0))}</strong></div>
    </section>
    <div className="two-column billing-columns">
      <section className="panel">
        <div className="panel-head"><div><span>{t("DRAFT PURCHASING")}</span><h2>{t("Credit bundles")}</h2></div></div>
        {products.loading ? <Loading /> : products.error ? (
          <ErrorState message={products.error} retry={products.refresh} />
        ) : bundles.length ? (
          <div className="bundle-grid">{bundles.map((bundle, index) => {
            const code = text(bundle.code, String(index));
            return <article key={code}>
              <strong>{text(bundle.name, t("Credit bundle"))}</strong>
              <span>{t("{count} visit credits",{count:number(Number(bundle.credits ?? 0))})}</span>
              {bundle.displayPrice ? <b>{text(bundle.displayPrice)}</b> : typeof bundle.unitAmount === "number" ? <b>{currency(bundle.unitAmount / 100, text(bundle.currency,"USD"))}</b> : <b>{t("Price configured in Stripe")}</b>}
              <button
                className="btn btn--primary"
                disabled={pending === code}
                onClick={() => void checkout(code)}
              >
                {t("Continue to checkout")}
              </button>
            </article>;
          })}</div>
        ) : <Empty title={t("No bundles configured")} message={t("Credit bundles appear after Stripe products are configured.")} />}
      </section>
      {roleAllowsAdjustment(session?.user.role) && (
        <aside className="panel">
          <div className="panel-head"><div><span>{t("ADMINISTRATOR ONLY")}</span><h2>{t("Manual adjustment")}</h2></div></div>
          <form className="form-stack" onSubmit={adjust}>
            <label>{t("Credit amount")}<input name="amount" type="number" required /></label>
            <label>{t("Reason")}<textarea name="reason" rows={3} required /></label>
            <button className="btn btn--secondary" disabled={pending === "adjust"}>{t("Record adjustment")}</button>
          </form>
        </aside>
      )}
    </div>
    <section className="panel below-panel">
      <div className="panel-head"><div><span>{t("AUDITABLE BALANCE")}</span><h2>{t("Credit ledger")}</h2></div></div>
      {ledger.length ? (
        <div className="table-scroll">
          <table>
            <thead><tr><th>{t("Date")}</th><th>{t("Source")}</th><th>{t("Reason")}</th><th>{t("Change")}</th><th>{t("Reference")}</th></tr></thead>
            <tbody>{ledger.map((entry, index) => (
              <tr key={text(entry.id, String(index))}>
                <td>{dateTimeText(entry.createdAt, locale)}</td>
                <td>{text(entry.source)}</td>
                <td>{text(entry.reason)}</td>
                <td className={Number(entry.amount) >= 0 ? "positive" : "negative"}>
                  {Number(entry.amount) >= 0 ? "+" : ""}{number(Number(entry.amount ?? 0))}
                </td>
                <td>{text(entry.sourceReference, "-")}</td>
              </tr>
            ))}</tbody>
          </table>
        </div>
      ) : <Empty title={t("No ledger entries")} message={t("Purchases, usage, and adjustments will appear here.")} />}
    </section>
  </>;
}
