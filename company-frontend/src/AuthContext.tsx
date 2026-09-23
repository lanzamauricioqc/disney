import { createContext, useContext, useEffect, useState, type ReactNode } from "react";
import { onUnauthorized, post } from "./lib/api";
import { clearSession, getAccessToken, readStoredSession, storeSession } from "./lib/session";
import type { AuthResponse, CompanyUser, Session } from "./types";

interface AuthValue { session?: Session; ready: boolean; login: (email: string, password: string) => Promise<void>; establish: (response: AuthResponse) => void; logout: () => void }
const AuthContext = createContext<AuthValue | undefined>(undefined);
function stored(): Session | undefined { try { const token = getAccessToken(); const value = readStoredSession(); if (!token || !value) return undefined; const session = JSON.parse(value) as Session; if (session.expiresAt && new Date(session.expiresAt) <= new Date()) return undefined; return { ...session, accessToken: token }; } catch { return undefined; } }

export function AuthProvider({ children }: { children: ReactNode }) {
  const [session, setSession] = useState<Session | undefined>(() => stored()); const [ready] = useState(true);
  const logout = () => { clearSession(); setSession(undefined); };
  useEffect(() => onUnauthorized(logout), []);
  const establish = (response: AuthResponse) => { const next: Session = { accessToken: response.accessToken, expiresAt: response.expiresAt, user: response.user }; storeSession(next.accessToken, JSON.stringify(next)); setSession(next); };
  const login = async (email: string, password: string) => establish(await post<AuthResponse>("/auth/login", { email, password }, false));
  return <AuthContext value={{ session, ready, login, establish, logout }}>{children}</AuthContext>;
}

export function useAuth() { const value = useContext(AuthContext); if (!value) throw new Error("useAuth must be used inside AuthProvider"); return value; }
export function userLabel(user?: CompanyUser, fallback = "Company user") { return user?.name || user?.email || fallback; }
