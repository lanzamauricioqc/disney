import { useState, type FormEvent } from "react";
import { Link } from "react-router-dom";
import { Modal } from "../components/Modal";
import { ApiMessage, Empty, ErrorState, Loading, PageHeader, errorText, formObject } from "../components/Ui";
import { post, put } from "../lib/api";
import { asList, dateText, initials, text } from "../lib/format";
import { useApiData } from "../lib/useApiData";
import type { Entity } from "../types";

export function CustomersPage() {
  const { data, loading, error, refresh } = useApiData<unknown>("/customers"); const [query, setQuery] = useState(""); const [editing, setEditing] = useState<Entity | null>(); const [message, setMessage] = useState("");
  const customers = asList(data, ["customers"]).filter((item) => JSON.stringify(item).toLowerCase().includes(query.toLowerCase()));
  return <><PageHeader eyebrow="CUSTOMER DIRECTORY" title="Customers" description="Customer profiles available to your organization." action={<button className="btn btn--primary" onClick={() => setEditing({})}>+ Add customer</button>} /><div className="toolbar"><label className="search"><span aria-hidden="true">⌕</span><span className="sr-only">Search customers</span><input value={query} onChange={(e) => setQuery(e.target.value)} placeholder="Search name or email" /></label><span>{customers.length} shown</span></div><ApiMessage success={message} />
    {loading ? <Loading label="Loading customers" /> : error ? <ErrorState message={error} retry={refresh} /> : customers.length ? <section className="panel"><div className="table-scroll"><table><thead><tr><th>Customer</th><th>Contact</th><th>Upcoming visit</th><th>Status</th><th><span className="sr-only">Actions</span></th></tr></thead><tbody>{customers.map((customer, i) => <tr key={text(customer.id, String(i))}><td><div className="identity"><span>{initials(customer.name ?? customer.fullName)}</span><div><strong>{text(customer.name ?? customer.fullName, "Unnamed customer")}</strong><small>{text(customer.reference ?? customer.externalReference, "No reference")}</small></div></div></td><td><strong>{text(customer.email)}</strong><small>{text(customer.phone, "No phone")}</small></td><td>{dateText(customer.nextVisitDate)}</td><td>{text(customer.status, "Active")}</td><td className="actions">{customer.id && <Link to={`/customers/${customer.id}`}>Open</Link>}<button onClick={() => setEditing(customer)}>Edit</button>{customer.id && <Link to={`/visits?customerId=${customer.id}`}>Visits</Link>}</td></tr>)}</tbody></table></div></section> : <Empty title="No customers found" message={query ? "Try a different search." : "Add the first customer to begin preparing a visit."} action={!query && <button className="btn btn--primary" onClick={() => setEditing({})}>Add customer</button>} />}
    {editing !== undefined && <CustomerForm customer={editing ?? {}} onClose={() => setEditing(undefined)} onSaved={() => { setEditing(undefined); setMessage("Customer saved."); void refresh(); }} />}
  </>;
}

function CustomerForm({ customer, onClose, onSaved }: { customer: Entity; onClose: () => void; onSaved: () => void }) {
  const [pending, setPending] = useState(false); const [error, setError] = useState("");
  const submit = async (event: FormEvent<HTMLFormElement>) => { event.preventDefault(); setPending(true); setError(""); try { const value = formObject(event); customer.id ? await put(`/customers/${customer.id}`, value) : await post("/customers", value); onSaved(); } catch (err) { setError(errorText(err)); } finally { setPending(false); } };
  return <Modal title={customer.id ? "Edit customer" : "Add customer"} onClose={onClose}><form className="form-grid" onSubmit={submit}><label className="span-2">Full name<input name="name" defaultValue={text(customer.name ?? customer.fullName, "")} required /></label><label>Email<input name="email" type="email" defaultValue={text(customer.email, "")} /></label><label>Phone<input name="phone" type="tel" defaultValue={text(customer.phone, "")} /></label><label className="span-2">External reference<input name="externalReference" defaultValue={text(customer.externalReference ?? customer.reference, "")} /></label><label className="span-2">Customer context<textarea name="notes" defaultValue={text(customer.notes, "")} rows={3} /></label><ApiMessage error={error} /><div className="form-actions span-2"><button type="button" className="btn btn--ghost" onClick={onClose}>Cancel</button><button className="btn btn--primary" disabled={pending}>{pending ? "Saving…" : "Save customer"}</button></div></form></Modal>;
}
