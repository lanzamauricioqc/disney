import type { FormEvent, ReactNode } from "react";
import { ApiError } from "../lib/api";

export function PageHeader({ eyebrow, title, description, action }: { eyebrow?: string; title: string; description?: string; action?: ReactNode }) { return <header className="page-header"><div>{eyebrow && <span>{eyebrow}</span>}<h1>{title}</h1>{description && <p>{description}</p>}</div>{action}</header>; }
export function Loading({ label = "Loading" }: { label?: string }) { return <div className="state-card" role="status"><span className="spinner" /> <strong>{label}…</strong></div>; }
export function ErrorState({ message, retry }: { message: string; retry?: () => void }) { return <div className="state-card state-card--error" role="alert"><div><strong>Unable to load this view</strong><p>{message}</p></div>{retry && <button className="btn btn--secondary" onClick={retry}>Try again</button>}</div>; }
export function Empty({ title, message, action }: { title: string; message: string; action?: ReactNode }) { return <div className="empty"><span aria-hidden="true">◇</span><h2>{title}</h2><p>{message}</p>{action}</div>; }
export function Status({ value }: { value: unknown }) { const label = typeof value === "string" ? value : "Unknown"; return <span className={`status status--${label.toLowerCase().replace(/\W+/g, "-")}`}>{label}</span>; }
export function ApiMessage({ error, success }: { error?: string; success?: string }) { return error ? <div className="notice notice--error" role="alert">{error}</div> : success ? <div className="notice notice--success" role="status">{success}</div> : null; }
export function errorText(error: unknown): string { return error instanceof ApiError ? `${error.info.title}: ${error.info.detail}` : error instanceof Error ? error.message : "The request could not be completed."; }
export function formObject(event: FormEvent<HTMLFormElement>): Record<string, string> { return Object.fromEntries(new FormData(event.currentTarget).entries()) as Record<string, string>; }
