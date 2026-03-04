import { useState } from "react";
import SessionDetail from "./components/SessionDetail";
import SessionList from "./components/SessionList";

export default function App() {
  const [selectedId, setSelectedId] = useState<string | null>(null);

  return (
    <div
      style={{
        display: "flex",
        height: "100vh",
        background: "#1a1a2e",
        color: "#eee",
        fontFamily: "-apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, sans-serif",
      }}
    >
      {/* Sidebar */}
      <div
        style={{
          width: 320,
          flexShrink: 0,
          borderRight: "1px solid #333",
          overflow: "auto",
        }}
      >
        <div
          style={{
            padding: "14px 16px",
            fontWeight: 700,
            fontSize: 14,
            borderBottom: "1px solid #333",
            background: "#16213e",
          }}
        >
          Sessions
        </div>
        <SessionList selectedId={selectedId} onSelect={setSelectedId} />
      </div>

      {/* Main */}
      <div style={{ flex: 1, display: "flex", flexDirection: "column" }}>
        {selectedId ? (
          <SessionDetail sessionId={selectedId} />
        ) : (
          <div
            style={{
              flex: 1,
              display: "flex",
              alignItems: "center",
              justifyContent: "center",
              color: "#555",
              fontSize: 14,
            }}
          >
            Select a session to view logs
          </div>
        )}
      </div>
    </div>
  );
}
