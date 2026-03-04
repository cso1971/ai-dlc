import logging
from pathlib import Path

import httpx
from fastapi import BackgroundTasks, FastAPI, Request
from fastapi.middleware.cors import CORSMiddleware

from .config import settings
from .sessions_router import router as sessions_router
from .webhook_handler import detect_mr_note, detect_trigger, handle_issue_webhook, handle_mr_note_webhook

logging.basicConfig(
    level=logging.INFO,
    format="%(asctime)s %(levelname)s %(name)s: %(message)s",
)
logger = logging.getLogger(__name__)

app = FastAPI(title="GitLab Webhook Server")

app.add_middleware(
    CORSMiddleware,
    allow_origins=["*"],
    allow_methods=["*"],
    allow_headers=["*"],
)

app.include_router(sessions_router)

# Resolved at startup — used to filter out the bot's own MR comments
_bot_username: str = ""


@app.on_event("startup")
async def startup():
    global _bot_username
    Path(settings.sessions_dir).mkdir(parents=True, exist_ok=True)
    logger.info("Sessions directory: %s", settings.sessions_dir)

    # Resolve bot username for MR note filtering
    if settings.gitlab_bot_username:
        _bot_username = settings.gitlab_bot_username
        logger.info("Bot username (from config): %s", _bot_username)
    else:
        bot_token = settings.gitlab_bot_token or settings.gitlab_token
        if bot_token:
            try:
                async with httpx.AsyncClient() as client:
                    resp = await client.get(
                        f"{settings.gitlab_api_url}/user",
                        headers={"PRIVATE-TOKEN": bot_token},
                        timeout=10,
                    )
                    resp.raise_for_status()
                    _bot_username = resp.json().get("username", "")
                    logger.info("Bot username (auto-detected): %s", _bot_username)
            except Exception:
                logger.warning("Could not auto-detect bot username — MR note loop prevention may not work")


@app.get("/health")
async def health():
    return {"status": "ok"}


async def _handle_webhook(request: Request, bg: BackgroundTasks):
    """Shared handler for all GitLab webhooks.

    Routes to the appropriate handler based on event type:
    - Issue label changes → workflow stages (Breakdown, Ready, Planned)
    - MR note (comment) → MR review response
    """
    payload = await request.json()

    # 1. Check for issue label triggers (existing workflow)
    trigger_label = detect_trigger(payload)
    if trigger_label is not None:
        issue = payload["object_attributes"]
        logger.info(
            "Triggered stage '%s' for issue #%s: %s",
            trigger_label,
            issue["iid"],
            issue["title"],
        )
        bg.add_task(handle_issue_webhook, payload, trigger_label, settings)
        return {"status": "accepted", "stage": trigger_label}

    # 2. Check for MR note events (review comments)
    note_info = detect_mr_note(payload, _bot_username)
    if note_info is not None:
        logger.info(
            "MR note detected on !%s by %s: %s",
            note_info["mr_iid"],
            note_info["author_name"],
            note_info["note"][:100],
        )
        bg.add_task(handle_mr_note_webhook, note_info, settings)
        return {"status": "accepted", "stage": "mr_review"}

    logger.info("Ignoring event — no workflow trigger detected")
    return {"status": "ignored"}


@app.post("/webhook/gitlab")
async def webhook_gitlab(request: Request, bg: BackgroundTasks):
    return await _handle_webhook(request, bg)


@app.post("/webhook/breakdown")
async def webhook_breakdown(request: Request, bg: BackgroundTasks):
    """Backward-compatible alias for the old endpoint."""
    return await _handle_webhook(request, bg)
