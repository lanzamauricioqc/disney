import { createContext, useContext, useEffect, useState, type ReactNode } from "react";
import { es, pt, type Message } from "./i18n-data";

export const supportedLocales = ["en", "pt-BR", "es"] as const;
export type Locale = (typeof supportedLocales)[number];
const STORAGE_KEY = "park-pilot-locale";
function initialLocale(): Locale {
  try { const stored = localStorage.getItem(STORAGE_KEY); if (supportedLocales.includes(stored as Locale)) return stored as Locale; } catch { /* Storage can be disabled. */ }
  const languages = typeof navigator === "undefined" ? [] : navigator.languages.length ? navigator.languages : [navigator.language];
  for (const language of languages) { const value = language.toLowerCase(); if (value.startsWith("pt")) return "pt-BR"; if (value.startsWith("es")) return "es"; if (value.startsWith("en")) return "en"; }
  return "en";
}
type Params = Record<string, string | number>;
export type Translate = (message: Message, params?: Params) => string;
interface I18nValue { locale: Locale; setLocale: (locale: Locale) => void; t: Translate; number: (value: number, options?: Intl.NumberFormatOptions) => string; currency: (value: number, currencyCode: string) => string; date: (value: Date, options?: Intl.DateTimeFormatOptions) => string; }
const I18nContext = createContext<I18nValue | undefined>(undefined);
export function I18nProvider({ children }: { children: ReactNode }) {
  const [locale, setLocale] = useState<Locale>(initialLocale);
  useEffect(() => { document.documentElement.lang = locale; document.title = locale === "pt-BR" ? "Empresa Park Pilot" : locale === "es" ? "Empresa Park Pilot" : "Park Pilot Company"; try { localStorage.setItem(STORAGE_KEY, locale); } catch { /* Storage can be disabled. */ } }, [locale]);
  const t: Translate = (message, params) => { let value = locale === "pt-BR" ? pt[message] : locale === "es" ? es[message] : message; for (const [key, replacement] of Object.entries(params ?? {})) value = value.replaceAll(`{${key}}`, String(replacement)); return value; };
  return <I18nContext value={{ locale, setLocale, t, number: (value, options) => new Intl.NumberFormat(locale, options).format(value), currency: (value, currencyCode) => new Intl.NumberFormat(locale, { style: "currency", currency: currencyCode }).format(value), date: (value, options) => new Intl.DateTimeFormat(locale, options).format(value) }}>{children}</I18nContext>;
}
export function useI18n() { const value = useContext(I18nContext); if (!value) throw new Error("useI18n must be used inside I18nProvider"); return value; }
export function LanguageSelector({ className = "" }: { className?: string }) { const { locale, setLocale, t } = useI18n(); return <label className={`language-selector ${className}`.trim()}><span>{t("Language")}</span><select value={locale} onChange={event => setLocale(event.target.value as Locale)}><option value="en">English</option><option value="pt-BR">Português (Brasil)</option><option value="es">Español</option></select></label>; }
const valueMessages = { Active: "Active", Planned: "Planned", Completed: "Completed", Cancelled: "Cancelled", Pending: "Pending", Upcoming: "Upcoming", Unknown: "Unknown", Owner: "Owner", Administrator: "Administrator", Planner: "Planner", Support: "Support", Staff: "Staff" } as const;
export function localizedValue(value: unknown, t: Translate): string { const raw = typeof value === "string" ? value : "Unknown"; const message = valueMessages[raw as keyof typeof valueMessages]; return message ? t(message) : raw; }
