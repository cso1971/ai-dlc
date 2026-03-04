import logging

from fastapi import APIRouter, WebSocket, WebSocketDisconnect

from .session_manager import session_manager

logger = logging.getLogger(__name__)

router = APIRouter(prefix="/sessions", tags=["sessions"])


@router.get("")
async def list_sessions():
    return session_manager.list_sessions()


@router.get("/{session_id}")
async def get_session(session_id: str):
    data = session_manager.get_session_full(session_id)
    if data is None:
        return {"error": "not found"}, 404
    return data


@router.websocket("/{session_id}/stream")
async def stream_session(ws: WebSocket, session_id: str):
    await ws.accept()

    session = session_manager.get_session(session_id)
    if session is None:
        # Session not active — send existing lines from disk then close
        data = session_manager.get_session_full(session_id)
        if data is None:
            await ws.send_json({"type": "error", "message": "session not found"})
            await ws.close()
            return
        for line in data.get("lines", []):
            await ws.send_json({"type": "line", **line})
        await ws.send_json({
            "type": "done",
            "status": data.get("status", "unknown"),
            "finished_at": data.get("finished_at"),
        })
        await ws.close()
        return

    # Active session — send catch-up lines then stream live
    queue = session.subscribe()
    try:
        # Catch-up: send all existing lines
        for line in list(session.lines):
            await ws.send_json({"type": "line", **line})

        # If already finished, send done and exit
        if session.status != "running":
            await ws.send_json({
                "type": "done",
                "status": session.status,
                "finished_at": session.finished_at,
            })
            return

        # Live stream
        while True:
            msg = await queue.get()
            if msg is None:
                break
            await ws.send_json(msg)
    except WebSocketDisconnect:
        logger.info("WebSocket client disconnected from session %s", session_id)
    finally:
        session.unsubscribe(queue)
