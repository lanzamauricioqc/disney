import { useState } from "react";
import { Empty, ErrorState, Loading, PageHeader } from "../components/Ui";
import { asList, text } from "../lib/format";
import { useApiData } from "../lib/useApiData";

export function ReportsPage() {
  const state = useApiData<unknown>("/reports/usage");
  const [downloadError, setDownloadError] = useState("");

  if (state.loading) {
    return <Loading label="Loading usage report" />;
  }

  if (state.error) {
    return <ErrorState message={state.error} retry={state.refresh} />;
  }

  const months = asList(state.data);
  const totalVisits = months.reduce((sum, month) => sum + Number(month.visitsCreated ?? 0), 0);
  const totalCredits = months.reduce((sum, month) => sum + Number(month.creditsConsumed ?? 0), 0);
  const maximum = Math.max(1, ...months.map(month => Number(month.visitsCreated ?? 0)));

  const download = async () => {
    setDownloadError("");
    try {
      const token = sessionStorage.getItem("parkPilotCompanyToken") ?? "";
      const response = await fetch("/api/v1/company/reports/usage.csv", {
        headers: { Authorization: `Bearer ${token}` }
      });
      if (!response.ok) {
        throw new Error(`CSV download failed (${response.status}).`);
      }

      const url = URL.createObjectURL(await response.blob());
      const anchor = document.createElement("a");
      anchor.href = url;
      anchor.download = "park-pilot-usage.csv";
      anchor.click();
      URL.revokeObjectURL(url);
    } catch (error) {
      setDownloadError(error instanceof Error ? error.message : "CSV download failed.");
    }
  };

  return <>
    <PageHeader
      eyebrow="USAGE INTELLIGENCE"
      title="Reports"
      description="Monthly organization visit and credit usage."
      action={<button className="btn btn--secondary" onClick={() => void download()}>Download CSV</button>}
    />
    {downloadError && <div className="notice notice--error" role="alert">{downloadError}</div>}
    <section className="metric-grid report-metrics">
      <article><span>VISITS CREATED</span><strong>{totalVisits}</strong></article>
      <article><span>CREDITS CONSUMED</span><strong>{totalCredits}</strong></article>
      <article><span>MONTHS REPORTED</span><strong>{months.length}</strong></article>
    </section>
    <section className="panel">
      <div className="panel-head"><div><span>MONTHLY VOLUME</span><h2>Visit-credit usage</h2></div></div>
      {months.length ? <>
        <div className="bar-chart" role="img" aria-label="Monthly visits created">
          {months.map((month, index) => {
            const value = Number(month.visitsCreated ?? 0);
            return <div key={text(month.month, String(index))}>
              <span className="bar-value">{value}</span>
              <span className="bar" style={{ height: `${Math.max(4, value / maximum * 100)}%` }} />
              <strong>{text(month.month)}</strong>
            </div>;
          })}
        </div>
        <div className="table-scroll">
          <table>
            <thead><tr><th>Month</th><th>Visits created</th><th>Credits consumed</th></tr></thead>
            <tbody>{months.map((month, index) => (
              <tr key={text(month.month, String(index))}>
                <td>{text(month.month)}</td>
                <td>{text(month.visitsCreated, "0")}</td>
                <td>{text(month.creditsConsumed, "0")}</td>
              </tr>
            ))}</tbody>
          </table>
        </div>
      </> : <Empty title="No usage data" message="Usage appears after visits and credit activity are recorded." />}
    </section>
  </>;
}
