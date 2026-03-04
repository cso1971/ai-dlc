import { type SessionMeta, useSessions } from "../api";

interface Props {
  selectedId: string | null;
  onSelect: (id: string) => void;
}

function statusBadge(status: string) {
  switch (status) {
    case "running":
      return "🟡";
    case "success":
      return "🟢";
    case "error":
      return "🔴";
    default:
      return "⚪";
  }
}

function formatTime(iso: string) {
  return new Date(iso).toLocaleTimeString();
}

export default function SessionList({ selectedId, onSelect }: Props) {
  const { data: sessions, isLoading, error } = useSessions();

  if (isLoading) return <div style={{ padding: 16 }}>Loading...</div>;
  if (error) return <div style={{ padding: 16, color: "#f44" }}>Error loading sessions</div>;
  if (!sessions || sessions.length === 0)
    return <div style={{ padding: 16, color: "#888" }}>No sessions yet</div>;

  return (
    <div>
      {sessions.map((s: SessionMeta) => (
        <div
          key={s.session_id}
          onClick={() => onSelect(s.session_id)}
          style={{
            padding: "10px 14px",
            cursor: "pointer",
            borderBottom: "1px solid #333",
            background: s.session_id === selectedId ? "#2a2a3e" : "transparent",
          }}
        >
          <div style={{ display: "flex", justifyContent: "space-between", alignItems: "center" }}>
            <span style={{ fontWeight: 600, fontSize: 13 }}>
              {statusBadge(s.status)} Issue #{s.issue_iid}
            </span>
            <span style={{ fontSize: 11, color: "#888" }}>{formatTime(s.started_at)}</span>
          </div>
          <div style={{ fontSize: 12, color: "#aaa", marginTop: 4, overflow: "hidden", textOverflow: "ellipsis", whiteSpace: "nowrap" }}>
            {s.issue_title}
          </div>
          <div style={{ fontSize: 11, color: "#666", marginTop: 2 }}>
            {s.line_count} lines
          </div>
        </div>
      ))}
    </div>
  );
}
