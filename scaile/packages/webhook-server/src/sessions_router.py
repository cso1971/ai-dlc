import asyncio
import logging

from fastapi import APIRouter, WebSocket, WebSocketDisconnect
from fastapi.responses import JSONResponse

from .config import settings
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


@router.post("/{session_id}/stop")
async def stop_session(session_id: str):
    session = session_manager.get_session(session_id)
    if session is None:
        return JSONResponse({"error": "session not found"}, status_code=404)
    if session.status != "running":
        return JSONResponse({"error": "session is not running"}, status_code=409)
    killed = await session.kill_process()
    if not killed:
        return JSONResponse({"error": "no process to kill"}, status_code=409)
    return {"status": "stopped", "session_id": session_id}


@router.post("/{session_id}/restart")
async def restart_session(session_id: str):
    # Find the session (active or on disk) to get restart context
    session = session_manager.get_session(session_id)
    restart_ctx = session._restart_context if session else None

    if restart_ctx is None:
        return JSONResponse(
            {"error": "no restart context available for this session"},
            status_code=409,
        )

    # Stop if still running
    if session and session.status == "running":
        await session.kill_process()

    # Re-trigger in background
    from .webhook_handler import handle_issue_webhook, handle_mr_note_webhook

    if restart_ctx["type"] == "issue":
        asyncio.create_task(
            handle_issue_webhook(
                restart_ctx["payload"],
                restart_ctx["trigger_label"],
                settings,
            )
        )
    elif restart_ctx["type"] == "mr_note":
        asyncio.create_task(
            handle_mr_note_webhook(restart_ctx["note_info"], settings)
        )
    else:
        return JSONResponse({"error": "unknown restart type"}, status_code=400)

    return {"status": "restarting", "original_session_id": session_id}


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
