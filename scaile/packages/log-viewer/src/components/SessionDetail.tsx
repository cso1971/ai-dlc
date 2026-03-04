import { useEffect, useRef, useState } from "react";
import { useQueryClient } from "@tanstack/react-query";
import { type LogLine, useSession, stopSession, restartSession } from "../api";
import { useSessionStream } from "../useSessionStream";

interface Props {
  sessionId: string;
}

function formatTs(iso: string) {
  const d = new Date(iso);
  const hms = d.toLocaleTimeString(undefined, {
    hour: "2-digit",
    minute: "2-digit",
    second: "2-digit",
  });
  const ms = String(d.getMilliseconds()).padStart(3, "0");
  return `${hms}.${ms}`;
}

export default function SessionDetail({ sessionId }: Props) {
  const { data: session, isLoading } = useSession(sessionId);
  const isRunning = session?.status === "running";
  const stream = useSessionStream(sessionId, isRunning);
  const bottomRef = useRef<HTMLDivElement>(null);
  const queryClient = useQueryClient();
  const [actionPending, setActionPending] = useState(false);

  const lines: LogLine[] = isRunning ? stream.lines : (session?.lines ?? []);
  const status = isRunning ? (stream.done ? stream.status : "running") : session?.status;

  useEffect(() => {
    bottomRef.current?.scrollIntoView({ behavior: "smooth" });
  }, [lines.length]);

  if (isLoading) return <div style={{ padding: 24 }}>Loading session...</div>;
  if (!session) return <div style={{ padding: 24, color: "#f44" }}>Session not found</div>;

  return (
    <div style={{ display: "flex", flexDirection: "column", height: "100%" }}>
      {/* Header */}
      <div style={{ padding: "16px 20px", borderBottom: "1px solid #333", flexShrink: 0 }}>
        <div style={{ display: "flex", justifyContent: "space-between", alignItems: "flex-start" }}>
          <div>
            <h2 style={{ margin: 0, fontSize: 16 }}>
              Issue #{session.issue_iid}: {session.issue_title}
            </h2>
            <div style={{ fontSize: 12, color: "#888", marginTop: 4 }}>
              Session: {session.session_id} &middot; Status:{" "}
              <span
                style={{
                  color: status === "running" ? "#fc0" : status === "success" ? "#4f4" : "#f44",
                  fontWeight: 600,
                }}
              >
                {status}
              </span>
              {session.finished_at && (
                <> &middot; Finished: {new Date(session.finished_at).toLocaleTimeString()}</>
              )}
            </div>
          </div>
          <div style={{ display: "flex", gap: 8, flexShrink: 0 }}>
            {status === "running" && (
              <button
                disabled={actionPending}
                onClick={async () => {
                  setActionPending(true);
                  try {
                    await stopSession(sessionId);
                    queryClient.invalidateQueries({ queryKey: ["sessions"] });
                    queryClient.invalidateQueries({ queryKey: ["session", sessionId] });
                  } catch (e) {
                    console.error("Stop failed:", e);
                  } finally {
                    setActionPending(false);
                  }
                }}
                style={{
                  background: "#d32f2f",
                  color: "#fff",
                  border: "none",
                  borderRadius: 4,
                  padding: "6px 14px",
                  fontSize: 12,
                  fontWeight: 600,
                  cursor: actionPending ? "not-allowed" : "pointer",
                  opacity: actionPending ? 0.6 : 1,
                }}
              >
                Stop
              </button>
            )}
            <button
              disabled={actionPending}
              onClick={async () => {
                setActionPending(true);
                try {
                  await restartSession(sessionId);
                  queryClient.invalidateQueries({ queryKey: ["sessions"] });
                } catch (e) {
                  console.error("Restart failed:", e);
                } finally {
                  setActionPending(false);
                }
              }}
              style={{
                background: "#e65100",
                color: "#fff",
                border: "none",
                borderRadius: 4,
                padding: "6px 14px",
                fontSize: 12,
                fontWeight: 600,
                cursor: actionPending ? "not-allowed" : "pointer",
                opacity: actionPending ? 0.6 : 1,
              }}
            >
              Restart
            </button>
          </div>
        </div>
      </div>

      {/* Log output */}
      <div
        style={{
          flex: 1,
          overflow: "auto",
          padding: "12px 20px",
          fontFamily: "'JetBrains Mono', 'Fira Code', 'Cascadia Code', monospace",
          fontSize: 12,
          lineHeight: 1.6,
          background: "#0d0d0d",
        }}
      >
        {lines.map((line, i) => (
          <div key={i} style={{ display: "flex", gap: 12 }}>
            <span style={{ color: "#555", flexShrink: 0, userSelect: "none" }}>
              {formatTs(line.ts)}
            </span>
            <span style={{ color: "#ccc", whiteSpace: "pre-wrap", wordBreak: "break-all" }}>
              {line.text}
            </span>
          </div>
        ))}
        {isRunning && !stream.done && (
          <div style={{ color: "#fc0", marginTop: 8 }}>
            &#9679; streaming...
          </div>
        )}
        <div ref={bottomRef} />
      </div>
    </div>
  );
}
