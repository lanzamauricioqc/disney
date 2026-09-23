import { useState } from "react";
import { Empty, ErrorState, Loading, PageHeader } from "../components/Ui";
import { useI18n } from "../i18n";
import { download } from "../lib/api";
import { errorText } from "../lib/apiError";
import { asList, text } from "../lib/format";
import { useApiData } from "../lib/useApiData";

export function ReportsPage() {
  const { t, number, locale } = useI18n();
  const state = useApiData<unknown>("/reports/usage");
  const [downloadError, setDownloadError] = useState("");

  if (state.loading) {
    return <Loading label={t("Loading usage report")} />;
  }

  if (state.error) {
    return <ErrorState message={state.error} retry={state.refresh} />;
  }

  const months = asList(state.data);
  const totalVisits = months.reduce((sum, month) => sum + Number(month.visitsCreated ?? 0), 0);
  const totalCredits = months.reduce((sum, month) => sum + Number(month.creditsConsumed ?? 0), 0);
  const maximum = Math.max(1, ...months.map(month => Number(month.visitsCreated ?? 0)));

  const downloadReport = async () => {
    setDownloadError("");
    try {
      const url = URL.createObjectURL(await download("/reports/usage.csv", "text/csv"));
      const anchor = document.createElement("a");
      anchor.href = url;
      anchor.download = "park-pilot-usage.csv";
      anchor.click();
      URL.revokeObjectURL(url);
    } catch (error) {
      setDownloadError(errorText(error, t));
    }
  };

  return <>
    <PageHeader
      eyebrow={t("USAGE INTELLIGENCE")}
      title={t("Reports")}
      description={t("Monthly organization visit and credit usage.")}
      action={<button className="btn btn--secondary" onClick={() => void downloadReport()}>{t("Download CSV")}</button>}
    />
    {downloadError && <div className="notice notice--error" role="alert">{downloadError}</div>}
    <section className="metric-grid report-metrics">
      <article><span>{t("VISITS CREATED")}</span><strong>{number(totalVisits)}</strong></article>
      <article><span>{t("CREDITS CONSUMED")}</span><strong>{number(totalCredits)}</strong></article>
      <article><span>{t("MONTHS REPORTED")}</span><strong>{number(months.length)}</strong></article>
    </section>
    <section className="panel">
      <div className="panel-head"><div><span>{t("MONTHLY VOLUME")}</span><h2>{t("Visit-credit usage")}</h2></div></div>
      {months.length ? <>
        <div className="bar-chart" role="img" aria-label={t("Monthly visits created")}>
          {months.map((month, index) => {
            const value = Number(month.visitsCreated ?? 0);
            return <div key={text(month.month, String(index))}>
              <span className="bar-value">{number(value)}</span>
              <span className="bar" style={{ height: `${Math.max(4, value / maximum * 100)}%` }} />
              <strong>{monthText(month.month, locale)}</strong>
            </div>;
          })}
        </div>
        <div className="table-scroll">
          <table>
            <thead><tr><th>{t("Month")}</th><th>{t("Visits created")}</th><th>{t("Credits consumed")}</th></tr></thead>
            <tbody>{months.map((month, index) => (
              <tr key={text(month.month, String(index))}>
                <td>{monthText(month.month, locale)}</td>
                <td>{number(Number(month.visitsCreated ?? 0))}</td>
                <td>{number(Number(month.creditsConsumed ?? 0))}</td>
              </tr>
            ))}</tbody>
          </table>
        </div>
      </> : <Empty title={t("No usage data")} message={t("Usage appears after visits and credit activity are recorded.")} />}
    </section>
  </>;
}

function monthText(value: unknown, locale: string): string { const raw = text(value); const match = /^(\d{4})-(\d{2})$/.exec(raw); if (!match) return raw; const date = new Date(Date.UTC(Number(match[1]), Number(match[2]) - 1, 1)); return new Intl.DateTimeFormat(locale, { month: "short", year: "numeric", timeZone: "UTC" }).format(date); }
