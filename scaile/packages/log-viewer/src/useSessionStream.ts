import { useEffect, useRef, useState } from "react";
import { type LogLine, wsUrl } from "./api";

interface StreamState {
  lines: LogLine[];
  done: boolean;
  status: string | null;
}

export function useSessionStream(
  sessionId: string | null,
  isRunning: boolean,
): StreamState {
  const [state, setState] = useState<StreamState>({
    lines: [],
    done: false,
    status: null,
  });
  const wsRef = useRef<WebSocket | null>(null);

  useEffect(() => {
    if (!sessionId || !isRunning) {
      return;
    }

    setState({ lines: [], done: false, status: null });

    const ws = new WebSocket(wsUrl(sessionId));
    wsRef.current = ws;

    ws.onmessage = (ev) => {
      const msg = JSON.parse(ev.data);
      if (msg.type === "line") {
        setState((prev) => ({
          ...prev,
          lines: [...prev.lines, { ts: msg.ts, text: msg.text }],
        }));
      } else if (msg.type === "done") {
        setState((prev) => ({
          ...prev,
          done: true,
          status: msg.status,
        }));
      }
    };

    ws.onerror = () => {
      setState((prev) => ({ ...prev, done: true, status: "ws_error" }));
    };

    return () => {
      ws.close();
      wsRef.current = null;
    };
  }, [sessionId, isRunning]);

  return state;
}
