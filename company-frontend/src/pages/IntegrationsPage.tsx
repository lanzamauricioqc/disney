import { useState, type FormEvent } from "react";
import { ApiMessage, Empty, ErrorState, Loading, PageHeader, errorText } from "../components/Ui";
import { useI18n } from "../i18n";
import { post, remove } from "../lib/api";
import { asList, dateText, text } from "../lib/format";
import { useApiData } from "../lib/useApiData";

const importExample = (travelerName: string, parkName: string) => `POST /api/v1/company/integrations/reservations
X-Api-Key: YOUR_KEY
Content-Type: application/json

{
  "customerExternalReference": "customer-123",
  "customerName": "${travelerName}",
  "customerEmail": "traveler@example.com",
  "customerPhone": null,
  "customerNotes": null,
  "reservationExternalReference": "booking-456",
  "parkName": "${parkName}",
  "visitDate": "YYYY-MM-DD",
  "timeZone": "America/New_York",
  "partySize": 2,
  "instructions": null,
  "meetingPoint": null,
  "transportationDetails": null
}`;

export function IntegrationsPage() {
  const { t, locale } = useI18n();
  const state = useApiData<unknown>("/integrations/api-keys");
  const [secret, setSecret] = useState("");
  const [message, setMessage] = useState("");
  const [formError, setFormError] = useState("");
  const keys = asList(state.data);

  const create = async (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault();
    setFormError("");
    const form = new FormData(event.currentTarget);
    try {
      const result = await post<Record<string, unknown>>(
        "/integrations/api-keys",
        { name: form.get("name") }
      );
      setSecret(text(result.secret, ""));
      event.currentTarget.reset();
      void state.refresh();
    } catch (error) {
      setFormError(errorText(error, t));
    }
  };

  const revoke = async (id: string) => {
    setFormError("");
    try {
      await remove(`/integrations/api-keys/${id}`);
      setMessage(t("API key revoked."));
      void state.refresh();
    } catch (error) {
      setFormError(errorText(error, t));
    }
  };

  return <>
    <PageHeader
      eyebrow={t("CONNECTED OPERATIONS")}
      title={t("Integrations")}
      description={t("Manage credentials for authorized reservation workflows.")}
    />
    <ApiMessage error={formError} success={message} />
    {secret && <section className="secret-card" role="status">
      <span>{t("NEW API KEY - SHOWN ONCE")}</span>
      <h2>{t("Copy this key now")}</h2>
      <div><code>{secret}</code><button onClick={() => void navigator.clipboard.writeText(secret)}>{t("Copy")}</button></div>
      <p>{t("Park Pilot will not display the full key again.")}</p>
      <button className="close-secret" onClick={() => setSecret("")}>{t("I stored it securely")}</button>
    </section>}
    <div className="two-column integrations-grid">
      <section className="panel">
        <div className="panel-head"><div><span>{t("PROGRAMMATIC ACCESS")}</span><h2>{t("API keys")}</h2></div></div>
        <form className="key-form" onSubmit={create}>
          <label>{t("Key name")}<input name="name" placeholder={t("Reservation importer")} required /></label>
          <button className="btn btn--primary">{t("Create API key")}</button>
        </form>
        {state.loading ? <Loading /> : state.error ? (
          <ErrorState message={state.error} retry={state.refresh} />
        ) : keys.length ? (
          <div className="table-scroll">
            <table>
              <thead><tr><th>{t("Name")}</th><th>{t("Prefix")}</th><th>{t("Created")}</th><th>{t("Last used")}</th><th /></tr></thead>
              <tbody>{keys.map((key, index) => (
                <tr key={text(key.id, String(index))}>
                  <td>{text(key.name)}</td>
                  <td><code>{text(key.prefix)}</code></td>
                  <td>{dateText(key.createdAt, locale)}</td>
                  <td>{dateText(key.lastUsedAt, locale)}</td>
                  <td><button className="table-action danger" onClick={() => key.id && void revoke(String(key.id))}>{t("Revoke")}</button></td>
                </tr>
              ))}</tbody>
            </table>
          </div>
        ) : <Empty title={t("No API keys")} message={t("Create a named key when an approved integration is ready.")} />}
      </section>
      <aside className="panel integration-guidance">
        <div className="panel-head"><div><span>{t("RESERVATION IMPORT")}</span><h2>{t("Endpoint example")}</h2></div></div>
        <p>{t("Reservation imports authenticate with the API key header, not a company-user bearer token.")}</p>
        <pre><code>{importExample(t("Traveler name"), t("Park name"))}</code></pre>
        <h3>{t("Security checklist")}</h3>
        <ul>
          <li>{t("Store keys in a secrets manager, never source code.")}</li>
          <li>{t("Use one key per integration and revoke unused credentials.")}</li>
          <li>{t("Send only traveler data needed to prepare the visit.")}</li>
        </ul>
      </aside>
    </div>
  </>;
}
