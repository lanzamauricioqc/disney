import { useCallback, useEffect, useState } from "react";
import { useI18n } from "../i18n";
import { get } from "./api";
import { errorText } from "./apiError";
export function useApiData<T>(path: string) { const { t } = useI18n(); const [data, setData] = useState<T>(); const [loading, setLoading] = useState(true); const [error, setError] = useState(""); const refresh = useCallback(async () => { setLoading(true); setError(""); try { setData(await get<T>(path)); } catch (err) { setError(errorText(err, t)); } finally { setLoading(false); } }, [path, t]); useEffect(() => { void refresh(); }, [refresh]); return { data, loading, error, refresh, setData }; }
