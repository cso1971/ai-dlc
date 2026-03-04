import { useQuery } from "@tanstack/react-query";

const BASE_URL =
  import.meta.env.VITE_WEBHOOK_URL ?? "http://localhost:8000";

export interface SessionMeta {
  session_id: string;
  issue_iid: number;
  issue_title: string;
  project_id: number;
  started_at: string;
  status: string;
  finished_at: string | null;
  result_summary: string | null;
  line_count: number;
}

export interface LogLine {
  ts: string;
  text: string;
}

export interface SessionFull extends Omit<SessionMeta, "line_count"> {
  lines: LogLine[];
}

async function fetchJson<T>(path: string): Promise<T> {
  const res = await fetch(`${BASE_URL}${path}`);
  if (!res.ok) throw new Error(`HTTP ${res.status}`);
  return res.json();
}

export function useSessions() {
  return useQuery<SessionMeta[]>({
    queryKey: ["sessions"],
    queryFn: () => fetchJson("/sessions"),
    refetchInterval: 5000,
  });
}

export function useSession(sessionId: string | null) {
  return useQuery<SessionFull>({
    queryKey: ["session", sessionId],
    queryFn: () => fetchJson(`/sessions/${sessionId}`),
    enabled: sessionId !== null,
  });
}

export function wsUrl(sessionId: string): string {
  const http = BASE_URL;
  const ws = http.replace(/^http/, "ws");
  return `${ws}/sessions/${sessionId}/stream`;
}
