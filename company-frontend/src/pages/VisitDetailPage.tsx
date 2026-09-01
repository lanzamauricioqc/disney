import { useState, type FormEvent } from "react";
import { Link, useParams } from "react-router-dom";
import { ApiMessage, ErrorState, Loading, PageHeader, Status, errorText } from "../components/Ui";
import { post, put, remove } from "../lib/api";
import { asList, dateText, dateTimeText, text } from "../lib/format";
import { useApiData } from "../lib/useApiData";
import type { Entity } from "../types";

export function VisitDetailPage() {
  const { id = "" } = useParams();
  const visit = useApiData<Entity>(`/visits/${id}`);
  const notes = useApiData<unknown>(`/visits/${id}/notes`);
  const overrides = useApiData<unknown>(`/visits/${id}/overrides`);
  const entitlement = useApiData<Entity | null>(`/visits/${id}/entitlement`);
  const [message, setMessage] = useState("");
  const [actionError, setActionError] = useState("");
  const [accessUrl, setAccessUrl] = useState("");
  const [pending, setPending] = useState("");

  const act = async (
    name: string,
    work: () => Promise<unknown>,
    success: string
  ) => {
    setPending(name);
    setActionError("");
    try {
      const result = await work();
      setMessage(success);
      return result;
    } catch (error) {
      setActionError(errorText(error));
      return undefined;
    } finally {
      setPending("");
    }
  };

  if (visit.loading) {
    return <Loading label="Loading visit" />;
  }

  if (visit.error || !visit.data) {
    return <ErrorState message={visit.error || "Visit was not returned by the API."} retry={visit.refresh} />;
  }

  const data = visit.data;
  const completed = Number(data.completedItemCount ?? 0);
  const total = Number(data.totalItemCount ?? 0);
  const progress = total > 0 ? Math.round(completed / total * 100) : 0;
  const noteItems = asList(notes.data);
  const overrideItems = asList(overrides.data);

  const generateAccessLink = async () => {
    const result = await act(
      "link",
      () => post<Record<string, unknown>>(`/visits/${id}/access-link`, {
        expiresAt: defaultAccessExpiry(data.visitDate)
      }),
      "Visitor access link generated. Copy it now; the token is not shown again."
    );
    if (!result) {
      return;
    }

    const token = text((result as Record<string, unknown>).token, "");
    if (token) {
      setAccessUrl(`${window.location.origin}/api/v1/company/visitor-access/${encodeURIComponent(token)}`);
    }
  };

  const assignEntitlement = async () => {
    const result = await act(
      "entitlement",
      () => post<Entity>(`/visits/${id}/entitlement`, {}),
      "One company credit assigned to this visit."
    );
    if (result) {
      entitlement.setData(result as Entity);
    }
  };

  const updateProgress = async (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault();
    const form = new FormData(event.currentTarget);
    const result = await act(
      "progress",
      () => put(`/visits/${id}/progress`, {
        status: form.get("status"),
        completedItemCount: Number(form.get("completedItemCount")),
        totalItemCount: Number(form.get("totalItemCount"))
      }),
      "Visit progress updated."
    );
    if (result) {
      void visit.refresh();
    }
  };

  return <>
    <Link className="back" to="/visits">All visits</Link>
    <PageHeader
      eyebrow="VISIT DETAIL"
      title={text(data.customerName, "Customer visit")}
      description={`${dateText(data.visitDate)} - ${text(data.parkName)}`}
      action={<Status value={data.status} />}
    />
    <ApiMessage error={actionError} success={message} />
    <section className="visit-summary">
      <article>
        <span>ITINERARY PROGRESS</span>
        <strong>{progress}%</strong>
        <div className="big-progress"><i style={{ width: `${progress}%` }} /></div>
        <small>{completed} of {total} planned stops</small>
      </article>
      <article><span>PARTY SIZE</span><strong>{text(data.partySize)}</strong><small>travelers</small></article>
      <article><span>MEETING POINT</span><strong className="summary-text">{text(data.meetingPoint)}</strong></article>
      <article><span>TRANSPORTATION</span><strong className="summary-text">{text(data.transportationDetails)}</strong></article>
    </section>
    <div className="detail-grid">
      <div>
        <section className="panel detail-card">
          <div className="panel-head"><div><span>CUSTOMER-FACING</span><h2>Visitor access</h2></div></div>
          <p>
            {entitlement.data
              ? "A visit credit is assigned. Generate a high-entropy link and share it only with the intended traveler."
              : "Assign one purchased visit credit before generating customer access."}
          </p>
          {!entitlement.loading && !entitlement.data ? (
            <button
              className="btn btn--secondary"
              disabled={pending === "entitlement"}
              onClick={() => void assignEntitlement()}
            >
              Assign one credit
            </button>
          ) : null}
          {accessUrl ? <div className="access-link">
            <code>{accessUrl}</code>
            <button onClick={() => void navigator.clipboard.writeText(accessUrl)}>Copy</button>
          </div> : <div className="inline-empty">Access tokens are shown only when generated.</div>}
          <div className="button-row">
            <button
              className="btn btn--primary"
              disabled={!entitlement.data || pending === "link"}
              onClick={() => void generateAccessLink()}
            >
              {accessUrl ? "Generate replacement" : "Generate access link"}
            </button>
            <button
              className="btn btn--danger"
              disabled={pending === "revoke"}
              onClick={() => void act(
                "revoke",
                () => remove(`/visits/${id}/access-link`),
                "Active access links revoked."
              ).then(() => setAccessUrl(""))}
            >
              Revoke active links
            </button>
          </div>
        </section>
        <section className="panel detail-card">
          <div className="panel-head"><div><span>CUSTOMER-FACING</span><h2>Visit instructions</h2></div></div>
          <dl className="details-list">
            <div><dt>Welcome and special instructions</dt><dd>{text(data.instructions, "No instructions added.")}</dd></div>
            <div><dt>Meeting point</dt><dd>{text(data.meetingPoint)}</dd></div>
            <div><dt>Transportation</dt><dd>{text(data.transportationDetails)}</dd></div>
          </dl>
        </section>
        <section className="panel detail-card internal">
          <div className="panel-head"><div><span>INTERNAL ONLY</span><h2>Itinerary overrides</h2></div></div>
          <form className="inline-form" onSubmit={async event => {
            event.preventDefault();
            const form = event.currentTarget;
            const values = new FormData(form);
            const instruction = String(values.get("instruction") ?? "");
            const reason = String(values.get("reason") ?? "");
            const result = await act(
              "override",
              () => post(`/visits/${id}/overrides`, {
                summary: instruction,
                detailsJson: JSON.stringify({ instruction, reason })
              }),
              "Override recorded."
            );
            if (result) {
              form.reset();
              void overrides.refresh();
            }
          }}>
            <label>Adjustment instruction<textarea name="instruction" rows={2} required /></label>
            <label>Operational reason<input name="reason" required /></label>
            <button className="btn btn--primary" disabled={pending === "override"}>Record override</button>
          </form>
          {overrides.loading ? <Loading /> : overrideItems.length ? (
            <ul className="record-list">{overrideItems.map((item, index) => (
              <li key={text(item.id, String(index))}>
                <strong>{text(item.summary)}</strong>
                <span>{text(item.detailsJson)}</span>
                <time>{dateTimeText(item.createdAt)}</time>
              </li>
            ))}</ul>
          ) : <div className="inline-empty">No itinerary overrides recorded.</div>}
        </section>
      </div>
      <aside>
        <section className="panel detail-card internal">
          <div className="panel-head"><div><span>INTERNAL ONLY</span><h2>Team notes</h2></div></div>
          <form className="note-form" onSubmit={async event => {
            event.preventDefault();
            const form = event.currentTarget;
            const value = new FormData(form);
            const result = await act(
              "note",
              () => post(`/visits/${id}/notes`, { note: value.get("note") }),
              "Internal note added."
            );
            if (result) {
              form.reset();
              void notes.refresh();
            }
          }}>
            <label>Add a note<textarea name="note" rows={3} required placeholder="Visible only to authorized staff" /></label>
            <button className="btn btn--secondary" disabled={pending === "note"}>Add internal note</button>
          </form>
          {notes.loading ? <Loading /> : noteItems.length ? (
            <ul className="notes-list">{noteItems.map((note, index) => (
              <li key={text(note.id, String(index))}>
                <p>{text(note.note)}</p>
                <span>{dateTimeText(note.createdAt)}</span>
              </li>
            ))}</ul>
          ) : <div className="inline-empty">No internal notes recorded.</div>}
        </section>
        <section className="panel detail-card">
          <div className="panel-head"><div><span>VISIT STATUS</span><h2>Progress</h2></div></div>
          <form className="form-stack" onSubmit={updateProgress}>
            <label>Status<select name="status" defaultValue={text(data.status, "Planned")}>
              <option value="Planned">Planned</option>
              <option value="Active">Active</option>
              <option value="Completed">Completed</option>
              <option value="Cancelled">Cancelled</option>
            </select></label>
            <label>Completed items<input name="completedItemCount" type="number" min="0" defaultValue={completed} required /></label>
            <label>Total items<input name="totalItemCount" type="number" min="0" defaultValue={total} required /></label>
            <button className="btn btn--secondary" disabled={pending === "progress"}>Update progress</button>
          </form>
        </section>
      </aside>
    </div>
  </>;
}

function defaultAccessExpiry(visitDateValue: unknown): string {
  const now = new Date();
  const fallback = new Date(now);
  fallback.setDate(fallback.getDate() + 7);
  if (typeof visitDateValue !== "string") {
    return fallback.toISOString();
  }

  const visitExpiry = new Date(`${visitDateValue}T23:59:59`);
  visitExpiry.setDate(visitExpiry.getDate() + 1);
  const maximum = new Date(now);
  maximum.setDate(maximum.getDate() + 89);
  return visitExpiry > now && visitExpiry <= maximum
    ? visitExpiry.toISOString()
    : fallback.toISOString();
}
