import { useCallback, useEffect, useState } from "react";
import { ApiError, get } from "./api";

export function useApiData<T>(path: string) {
  const [data, setData] = useState<T>(); const [loading, setLoading] = useState(true); const [error, setError] = useState("");
  const refresh = useCallback(async () => { setLoading(true); setError(""); try { setData(await get<T>(path)); } catch (err) { setError(err instanceof ApiError ? err.info.detail : "Unable to load data."); } finally { setLoading(false); } }, [path]);
  useEffect(() => { void refresh(); }, [refresh]);
  return { data, loading, error, refresh, setData };
}
