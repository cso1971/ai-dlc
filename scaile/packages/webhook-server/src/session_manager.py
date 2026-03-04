from __future__ import annotations

import asyncio
import json
import logging
import uuid
from dataclasses import dataclass, field
from datetime import datetime, timezone
from pathlib import Path
from typing import Any

from .config import settings

logger = logging.getLogger(__name__)


@dataclass
class Session:
    session_id: str
    issue_iid: int
    issue_title: str
    project_id: int
    started_at: str
    model: str = ""
    status: str = "running"
    finished_at: str | None = None
    result_summary: str | None = None
    lines: list[dict[str, str]] = field(default_factory=list)

    _subscribers: set[asyncio.Queue[dict | None]] = field(
        default_factory=set, repr=False
    )
    _flush_handle: asyncio.TimerHandle | None = field(default=None, repr=False)
    _process: asyncio.subprocess.Process | None = field(default=None, repr=False)
    _restart_context: dict | None = field(default=None, repr=False)

    def subscribe(self) -> asyncio.Queue[dict | None]:
        q: asyncio.Queue[dict | None] = asyncio.Queue()
        self._subscribers.add(q)
        return q

    def unsubscribe(self, q: asyncio.Queue[dict | None]) -> None:
        self._subscribers.discard(q)

    def set_process(self, proc: asyncio.subprocess.Process | None) -> None:
        self._process = proc

    async def kill_process(self) -> bool:
        """Kill the running subprocess. Returns True if killed, False if not running."""
        if self._process is None or self._process.returncode is not None:
            return False
        try:
            self._process.kill()
            await self._process.wait()
        except ProcessLookupError:
            pass
        self._process = None
        self.append_line("[workflow] Session stopped by user")
        self.finish("error", "Stopped by user")
        return True

    def append_line(self, text: str) -> None:
        entry = {
            "ts": datetime.now(timezone.utc).isoformat(),
            "text": text,
        }
        self.lines.append(entry)
        for q in self._subscribers:
            q.put_nowait({"type": "line", **entry})
        self._schedule_flush()

    def finish(self, status: str, summary: str | None = None) -> None:
        self.status = status
        self.finished_at = datetime.now(timezone.utc).isoformat()
        self.result_summary = summary
        done_msg: dict[str, Any] = {
            "type": "done",
            "status": status,
            "finished_at": self.finished_at,
        }
        for q in self._subscribers:
            q.put_nowait(done_msg)
            q.put_nowait(None)  # sentinel
        self._flush_to_disk()

    def to_dict(self, include_lines: bool = True) -> dict:
        d: dict[str, Any] = {
            "session_id": self.session_id,
            "issue_iid": self.issue_iid,
            "issue_title": self.issue_title,
            "project_id": self.project_id,
            "started_at": self.started_at,
            "model": self.model,
            "status": self.status,
            "finished_at": self.finished_at,
            "result_summary": self.result_summary,
        }
        if include_lines:
            d["lines"] = self.lines
        else:
            d["line_count"] = len(self.lines)
        return d

    # ── disk persistence ──

    def _session_path(self) -> Path:
        return Path(settings.sessions_dir) / f"{self.session_id}.json"

    def _flush_to_disk(self) -> None:
        try:
            path = self._session_path()
            path.parent.mkdir(parents=True, exist_ok=True)
            path.write_text(json.dumps(self.to_dict(), indent=2))
        except Exception:
            logger.exception("Failed to flush session %s to disk", self.session_id)

    def _schedule_flush(self) -> None:
        if self._flush_handle is not None:
            return
        try:
            loop = asyncio.get_running_loop()
            self._flush_handle = loop.call_later(2.0, self._do_debounced_flush)
        except RuntimeError:
            self._flush_to_disk()

    def _do_debounced_flush(self) -> None:
        self._flush_handle = None
        self._flush_to_disk()


class SessionManager:
    def __init__(self) -> None:
        self._active: dict[str, Session] = {}

    def create_session(
        self, issue_iid: int, issue_title: str, project_id: int, model: str = ""
    ) -> Session:
        now = datetime.now(timezone.utc)
        session_id = (
            f"{now.strftime('%Y%m%d_%H%M%S')}_issue{issue_iid}_{uuid.uuid4().hex[:8]}"
        )
        session = Session(
            session_id=session_id,
            issue_iid=issue_iid,
            issue_title=issue_title,
            project_id=project_id,
            started_at=now.isoformat(),
            model=model,
        )
        self._active[session_id] = session
        logger.info("Created session %s", session_id)
        return session

    def get_session(self, session_id: str) -> Session | None:
        return self._active.get(session_id)

    def list_sessions(self) -> list[dict]:
        """Return metadata for all sessions (active + completed on disk)."""
        seen: set[str] = set()
        results: list[dict] = []

        # Active sessions first
        for s in self._active.values():
            seen.add(s.session_id)
            results.append(s.to_dict(include_lines=False))

        # Then scan disk for completed sessions not in memory
        sessions_dir = Path(settings.sessions_dir)
        if sessions_dir.is_dir():
            for path in sessions_dir.glob("*.json"):
                sid = path.stem
                if sid in seen:
                    continue
                try:
                    data = json.loads(path.read_text())
                    data.pop("lines", None)
                    if "line_count" not in data:
                        data["line_count"] = 0
                    results.append(data)
                except Exception:
                    logger.warning("Skipping corrupt session file %s", path)

        results.sort(key=lambda d: d.get("started_at", ""), reverse=True)
        return results

    def get_session_full(self, session_id: str) -> dict | None:
        """Return full session data including lines."""
        active = self._active.get(session_id)
        if active:
            return active.to_dict(include_lines=True)

        path = Path(settings.sessions_dir) / f"{session_id}.json"
        if path.is_file():
            try:
                return json.loads(path.read_text())
            except Exception:
                logger.warning("Failed to read session file %s", path)
        return None


session_manager = SessionManager()
