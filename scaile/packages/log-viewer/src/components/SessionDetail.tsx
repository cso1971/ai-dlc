import { useEffect, useRef } from "react";
import { type LogLine, useSession } from "../api";
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
