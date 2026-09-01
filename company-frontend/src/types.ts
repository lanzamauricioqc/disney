export type Entity = Record<string, unknown> & { id?: string; name?: string; status?: string };
export interface CompanyUser { id?: string; email: string; name?: string; role?: string; organizationName?: string }
export interface AuthResponse { accessToken: string; expiresAt?: string; user: CompanyUser }
export interface Session { accessToken: string; expiresAt?: string; user: CompanyUser }
export interface ApiErrorInfo { status: number; title: string; detail: string; body?: unknown }
