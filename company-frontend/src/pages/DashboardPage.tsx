import { Link } from "react-router-dom";
import { Empty, ErrorState, Loading, PageHeader, Status } from "../components/Ui";
import { asList, dateText, text } from "../lib/format";
import { useApiData } from "../lib/useApiData";

export function DashboardPage() {
  const dashboard = useApiData<Record<string, unknown>>("/dashboard");
  const activeVisits = useApiData<unknown>("/visits?status=Active&limit=8");
  const plannedVisits = useApiData<unknown>("/visits?status=Planned&limit=8");

  if (dashboard.loading) {
    return <Loading label="Loading organization dashboard" />;
  }

  if (dashboard.error) {
    return <ErrorState message={dashboard.error} retry={dashboard.refresh} />;
  }

  const data = dashboard.data;
  const active = asList(activeVisits.data, ["visits"]);
  const planned = asList(plannedVisits.data, ["visits"]);

  return <>
    <PageHeader
      eyebrow="OPERATIONS"
      title="Today at a glance"
      description="Current organization, visit, and credit totals."
      action={<Link className="btn btn--primary" to="/visits?new=1">+ Prepare visit</Link>}
    />
    <section className="metric-grid" aria-label="Organization summary">
      <article className="metric metric--credits">
        <span>CREDITS AVAILABLE</span>
        <strong>{text(data?.creditsRemaining, "0")}</strong>
        <Link to="/billing">Manage credits</Link>
      </article>
      <article><span>ACTIVE VISITS</span><strong>{text(data?.activeVisits, "0")}</strong></article>
      <article><span>PLANNED VISITS</span><strong>{text(data?.upcomingVisits, "0")}</strong></article>
      <article><span>COMPLETED VISITS</span><strong>{text(data?.completedVisits, "0")}</strong></article>
      <article><span>CUSTOMERS</span><strong>{text(data?.customers, "0")}</strong></article>
      <article><span>CREDITS CONSUMED</span><strong>{text(data?.creditsConsumed, "0")}</strong></article>
    </section>
    <div className="dashboard-grid">
      <VisitList
        title="Active visits"
        loading={activeVisits.loading}
        error={activeVisits.error}
        visits={active}
        empty="No customer visits are currently active."
      />
      <VisitList
        title="Planned visits"
        loading={plannedVisits.loading}
        error={plannedVisits.error}
        visits={planned}
        empty="No future visits have been prepared."
      />
    </div>
  </>;
}

function VisitList({
  title,
  loading,
  error,
  visits,
  empty
}: {
  title: string;
  loading: boolean;
  error: string;
  visits: Record<string, unknown>[];
  empty: string;
}) {
  return <section className="panel panel--wide">
    <div className="panel-head">
      <div><span>VISIT OPERATIONS</span><h2>{title}</h2></div>
      <Link to="/visits">View all</Link>
    </div>
    {loading ? <Loading /> : error ? <ErrorState message={error} /> : visits.length ? (
      <div className="table-scroll">
        <table>
          <thead><tr><th>Customer</th><th>Park</th><th>Date</th><th>Status</th></tr></thead>
          <tbody>{visits.map((visit, index) => (
            <tr key={text(visit.id, String(index))}>
              <td><Link to={`/visits/${text(visit.id)}`}>{text(visit.customerName)}</Link></td>
              <td>{text(visit.parkName)}</td>
              <td>{dateText(visit.visitDate)}</td>
              <td><Status value={visit.status} /></td>
            </tr>
          ))}</tbody>
        </table>
      </div>
    ) : <Empty title={title} message={empty} />}
  </section>;
}
