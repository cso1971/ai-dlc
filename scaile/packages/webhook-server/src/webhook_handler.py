import asyncio
import json
import logging
import re
from pathlib import Path

from .config import Settings
from .session_manager import Session, session_manager

logger = logging.getLogger(__name__)

PROMPTS_DIR = Path(__file__).resolve().parent.parent / "prompts"

# Maps trigger labels to their prompt template and model
LABEL_CONFIG = {
    "Breakdown": {"prompt": "breakdown.md", "model": "sonnet"},
    "Ready":     {"prompt": "ready.md",     "model": "sonnet"},
    "Planned":   {"prompt": "planned.md",   "model": "sonnet"},
}


def detect_trigger(payload: dict) -> str | None:
    """Return the trigger label name if the webhook represents adding a known trigger label, else None."""
    if payload.get("object_kind") != "issue":
        return None

    attrs = payload.get("object_attributes", {})
    if attrs.get("action") != "update":
        return None

    changes = payload.get("changes", {})
    labels_change = changes.get("labels", {})
    previous = {l["title"] for l in labels_change.get("previous", [])}
    current = {l["title"] for l in labels_change.get("current", [])}

    added = current - previous

    logger.info(
        "Label change — added: %s | registered triggers: %s",
        added or "(none)",
        set(LABEL_CONFIG.keys()),
    )

    for label in LABEL_CONFIG:
        if label in added:
            return label

    return None


def build_prompt(
    prompt_file: str,
    project_id: int,
    issue_iid: int,
    issue_title: str,
    issue_description: str,
) -> str:
    """Read a prompt template and interpolate issue details."""
    template = (PROMPTS_DIR / prompt_file).read_text()
    return template.format(
        project_id=project_id,
        issue_iid=issue_iid,
        issue_title=issue_title,
        issue_description=issue_description or "(no description)",
    )


def _stream_event_to_session(event: dict, session: Session) -> None:
    """Extract human-readable lines from a stream-json event and append to the session."""
    event_type = event.get("type")

    if event_type == "assistant":
        message = event.get("message", {})
        for block in message.get("content", []):
            if block.get("type") == "text":
                text = block["text"].strip()
                if text:
                    session.append_line(f"[assistant] {text}")
            elif block.get("type") == "tool_use":
                tool_name = block.get("name", "unknown")
                tool_input = json.dumps(block.get("input", {}))
                if len(tool_input) > 200:
                    tool_input = tool_input[:200] + "…"
                session.append_line(f"[tool_call] {tool_name}({tool_input})")

    elif event_type == "tool_result":
        content = event.get("content", "")
        if isinstance(content, list):
            content = " ".join(
                b.get("text", "") for b in content if isinstance(b, dict)
            )
        content = str(content).strip()
        if content:
            if len(content) > 300:
                content = content[:300] + "…"
            session.append_line(f"[tool_result] {content}")

    elif event_type == "result":
        result_text = event.get("result", "")
        if result_text:
            if len(result_text) > 300:
                result_text = result_text[:300] + "…"
            session.append_line(f"[result] {result_text}")


async def _run_git(args: list[str], cwd: Path, env: dict[str, str]) -> str:
    """Run a git command and return stdout. Raises on failure."""
    proc = await asyncio.create_subprocess_exec(
        *args,
        stdout=asyncio.subprocess.PIPE,
        stderr=asyncio.subprocess.PIPE,
        cwd=str(cwd),
        env=env,
    )
    stdout, stderr = await proc.communicate()
    if proc.returncode != 0:
        raise RuntimeError(f"git {args[1]} failed: {stderr.decode().strip()}")
    return stdout.decode().strip()


def _slugify(text: str, max_len: int = 40) -> str:
    """Convert text to a kebab-case slug."""
    slug = re.sub(r"[^a-z0-9]+", "-", text.lower()).strip("-")
    return slug[:max_len].rstrip("-")


async def prepare_repo_clone(
    project_path: str,
    settings: Settings,
    session: Session | None = None,
) -> Path:
    """Clone the repo (if needed) or fetch latest changes. Returns the repo directory.

    This is a read-only checkout on the default branch — used by the Ready stage
    to analyze the codebase before creating tasks.
    """
    repos_dir = Path(settings.repos_dir)
    repos_dir.mkdir(parents=True, exist_ok=True)

    safe_project = project_path.replace("/", "_")
    repo_dir = repos_dir / safe_project

    bot_token = settings.gitlab_bot_token or settings.gitlab_token
    gitlab_host = settings.gitlab_api_url.replace("/api/v4", "")
    clone_url = f"{gitlab_host.replace('http://', f'http://oauth2:{bot_token}@')}/{project_path}.git"

    git_env = {
        "PATH": "/usr/local/sbin:/usr/local/bin:/usr/sbin:/usr/bin:/sbin:/bin",
        "HOME": "/home/claude",
        "GIT_TERMINAL_PROMPT": "0",
    }

    def _log(msg: str) -> None:
        logger.info("[workspace] %s", msg)
        if session:
            session.append_line(f"[workspace] {msg}")

    if not (repo_dir / ".git").exists():
        _log(f"Cloning {project_path} ...")
        repo_dir.mkdir(parents=True, exist_ok=True)
        await _run_git(["git", "clone", clone_url, str(repo_dir)], cwd=repos_dir, env=git_env)
        _log("Clone complete.")
    else:
        _log("Fetching latest changes ...")
        await _run_git(["git", "fetch", "origin"], cwd=repo_dir, env=git_env)
        await _run_git(["git", "checkout", "main"], cwd=repo_dir, env=git_env)
        await _run_git(["git", "reset", "--hard", "origin/main"], cwd=repo_dir, env=git_env)
        _log("Fetch complete — on latest main.")

    return repo_dir


async def prepare_repo_worktree(
    project_path: str,
    issue_iid: int,
    issue_title: str,
    settings: Settings,
    session: Session | None = None,
) -> tuple[Path, str]:
    """Clone the repo (if needed) and create a git worktree for the feature branch.

    Returns (worktree_path, branch_name).
    """
    repos_dir = Path(settings.repos_dir)
    repos_dir.mkdir(parents=True, exist_ok=True)

    # Derive paths
    safe_project = project_path.replace("/", "_")
    repo_dir = repos_dir / safe_project
    slug = _slugify(issue_title)
    branch_name = f"feature/{issue_iid}-{slug}"
    worktree_dir = repos_dir / "worktrees" / f"{issue_iid}-{slug}"

    # Git clone URL using the internal Docker hostname (use bot token)
    bot_token = settings.gitlab_bot_token or settings.gitlab_token
    gitlab_host = settings.gitlab_api_url.replace("/api/v4", "")
    clone_url = f"{gitlab_host.replace('http://', f'http://oauth2:{bot_token}@')}/{project_path}.git"

    git_env = {
        "PATH": "/usr/local/sbin:/usr/local/bin:/usr/sbin:/usr/bin:/sbin:/bin",
        "HOME": "/home/claude",
        "GIT_TERMINAL_PROMPT": "0",
    }

    def _log(msg: str) -> None:
        logger.info("[workspace] %s", msg)
        if session:
            session.append_line(f"[workspace] {msg}")

    # Clone if needed
    if not (repo_dir / ".git").exists():
        _log(f"Cloning {project_path} ...")
        repo_dir.mkdir(parents=True, exist_ok=True)
        await _run_git(["git", "clone", clone_url, str(repo_dir)], cwd=repos_dir, env=git_env)
        _log("Clone complete.")
    else:
        _log("Fetching latest changes ...")
        await _run_git(["git", "fetch", "origin"], cwd=repo_dir, env=git_env)
        _log("Fetch complete.")

    # Clean up stale worktree if it already exists
    if worktree_dir.exists():
        _log(f"Removing stale worktree {worktree_dir.name} ...")
        await _run_git(["git", "worktree", "remove", "--force", str(worktree_dir)], cwd=repo_dir, env=git_env)

    # Create worktree with a new branch based on origin/main
    _log(f"Creating worktree on branch {branch_name} ...")
    worktree_dir.parent.mkdir(parents=True, exist_ok=True)
    await _run_git(
        ["git", "worktree", "add", "-b", branch_name, str(worktree_dir), "origin/main"],
        cwd=repo_dir,
        env=git_env,
    )
    _log(f"Worktree ready at {worktree_dir}")

    return worktree_dir, branch_name


async def invoke_claude(
    prompt: str,
    settings: Settings,
    session: Session | None = None,
    model: str | None = None,
    cwd: str | None = None,
    resume_session_id: str | None = None,
) -> dict:
    """Run `claude -p` as a subprocess with streaming output.

    If resume_session_id is provided, resumes an existing Claude session
    instead of starting a new one (used for continuation after max-turns).
    """
    if resume_session_id:
        cmd = [
            "claude",
            "--resume", resume_session_id,
            "-p", prompt,
            "--output-format", "stream-json",
            "--verbose",
            "--dangerously-skip-permissions",
        ]
    else:
        cmd = [
            "claude",
            "-p",
            prompt,
            "--output-format", "stream-json",
            "--verbose",
            "--dangerously-skip-permissions",
        ]
    if model:
        cmd.extend(["--model", model])

    work_dir = cwd or settings.workspace_dir
    logger.info("Running command: %s", " ".join(cmd[:6]) + " ...")
    logger.info("Working directory: %s", work_dir)

    env = {
        "PATH": "/usr/local/sbin:/usr/local/bin:/usr/sbin:/usr/bin:/sbin:/bin",
        "HOME": "/home/claude",
        "ANTHROPIC_API_KEY": settings.anthropic_api_key,
        "GITLAB_PERSONAL_ACCESS_TOKEN": settings.gitlab_bot_token or settings.gitlab_token,
        "GITLAB_API_URL": settings.gitlab_api_url,
    }

    timeout_sec = settings.claude_timeout_minutes * 60

    proc = await asyncio.create_subprocess_exec(
        *cmd,
        stdout=asyncio.subprocess.PIPE,
        stderr=asyncio.subprocess.PIPE,
        cwd=work_dir,
        env=env,
    )

    if session is not None:
        session.set_process(proc)

    result_data: dict = {}
    stderr_lines: list[str] = []

    async def _drain_stdout():
        nonlocal result_data
        assert proc.stdout is not None
        async for raw_line in proc.stdout:
            line = raw_line.decode().rstrip()
            if not line:
                continue

            try:
                event = json.loads(line)
            except json.JSONDecodeError:
                logger.debug("Non-JSON stdout line: %s", line[:200])
                continue

            if session is not None:
                _stream_event_to_session(event, session)

            # Capture the final result event
            if event.get("type") == "result":
                result_data = event

    async def _drain_stderr():
        assert proc.stderr is not None
        async for raw_line in proc.stderr:
            line = raw_line.decode().rstrip()
            stderr_lines.append(line)
            logger.info("[claude stderr] %s", line)

    timed_out = False
    try:
        await asyncio.wait_for(
            asyncio.gather(_drain_stdout(), _drain_stderr()),
            timeout=timeout_sec,
        )
    except asyncio.TimeoutError:
        timed_out = True
        logger.error("Claude CLI timed out after %d minutes — killing process", settings.claude_timeout_minutes)
        if session:
            session.append_line(f"[workflow] TIMEOUT: Claude killed after {settings.claude_timeout_minutes} minutes")
        proc.kill()

    await proc.wait()

    if session is not None:
        session.set_process(None)

    stderr_text = "\n".join(stderr_lines)

    if timed_out:
        return {"error": f"timeout after {settings.claude_timeout_minutes} minutes", "stderr": stderr_text}

    if proc.returncode != 0:
        logger.error("Claude CLI exited with code %d", proc.returncode)
        logger.error("Claude CLI stderr:\n%s", stderr_text or "(empty)")
        return {"error": f"exit {proc.returncode}", "stderr": stderr_text}

    logger.info("Claude CLI exited successfully (code 0)")

    if result_data:
        return result_data

    return {"error": "no result event received"}


MAX_RESUME_ATTEMPTS = 3


async def _maybe_resume(
    result: dict,
    settings: Settings,
    session: Session | None,
    model: str | None,
    cwd: str | None,
) -> dict:
    """If Claude hit max turns, resume the session to let it finish.

    Returns the final result (either the original or from the last resume).
    """
    attempts = 0
    while (
        result.get("subtype") == "error_max_turns"
        and attempts < MAX_RESUME_ATTEMPTS
    ):
        attempts += 1
        claude_session_id = result.get("session_id")
        if not claude_session_id:
            logger.warning("Max turns reached but no session_id in result — cannot resume")
            break

        logger.info(
            "Claude hit max turns (attempt %d/%d) — resuming session %s",
            attempts, MAX_RESUME_ATTEMPTS, claude_session_id,
        )
        if session:
            session.append_line(
                f"[workflow] Max turns reached — auto-resuming (attempt {attempts}/{MAX_RESUME_ATTEMPTS})"
            )

        result = await invoke_claude(
            prompt="Continue where you left off. Complete the remaining steps from the original instructions.",
            settings=settings,
            session=session,
            model=model,
            cwd=cwd,
            resume_session_id=claude_session_id,
        )

    if result.get("subtype") == "error_max_turns":
        logger.error("Claude still incomplete after %d resume attempts", MAX_RESUME_ATTEMPTS)
        result["error"] = f"incomplete after {MAX_RESUME_ATTEMPTS} resume attempts"

    return result


def detect_mr_note(payload: dict, bot_username: str) -> dict | None:
    """Return note info dict if the webhook is a new MR comment (not from the bot), else None."""
    if payload.get("object_kind") != "note":
        return None

    attrs = payload.get("object_attributes", {})

    # Only MR comments
    if attrs.get("noteable_type") != "MergeRequest":
        return None

    # Ignore system notes (e.g. "added 1 commit")
    if attrs.get("system", False):
        return None

    # Only new comments, not edits
    if attrs.get("action") != "create":
        return None

    # Ignore bot's own comments to prevent loops
    user = payload.get("user", {})
    if user.get("username") == bot_username:
        logger.info("Ignoring MR note from bot user '%s'", bot_username)
        return None

    mr = payload.get("merge_request", {})
    if not mr:
        return None

    return {
        "note": attrs.get("note", ""),
        "note_id": attrs.get("id"),
        "mr_iid": mr.get("iid"),
        "mr_title": mr.get("title", ""),
        "source_branch": mr.get("source_branch", ""),
        "target_branch": mr.get("target_branch", "main"),
        "project_id": payload.get("project", {}).get("id"),
        "project_path": payload.get("project", {}).get("path_with_namespace", ""),
        "author_name": user.get("name", user.get("username", "reviewer")),
    }


async def prepare_review_worktree(
    project_path: str,
    branch_name: str,
    settings: Settings,
    session: Session | None = None,
) -> Path:
    """Clone the repo (if needed) and create a worktree on an existing remote branch.

    Unlike prepare_repo_worktree, this checks out an existing branch rather than
    creating a new one — used when Claude needs to push fixes to an MR's source branch.
    """
    repos_dir = Path(settings.repos_dir)
    repos_dir.mkdir(parents=True, exist_ok=True)

    safe_project = project_path.replace("/", "_")
    repo_dir = repos_dir / safe_project
    safe_branch = branch_name.replace("/", "_")
    worktree_dir = repos_dir / "worktrees" / f"review-{safe_branch}"

    bot_token = settings.gitlab_bot_token or settings.gitlab_token
    gitlab_host = settings.gitlab_api_url.replace("/api/v4", "")
    clone_url = f"{gitlab_host.replace('http://', f'http://oauth2:{bot_token}@')}/{project_path}.git"

    git_env = {
        "PATH": "/usr/local/sbin:/usr/local/bin:/usr/sbin:/usr/bin:/sbin:/bin",
        "HOME": "/home/claude",
        "GIT_TERMINAL_PROMPT": "0",
    }

    def _log(msg: str) -> None:
        logger.info("[workspace] %s", msg)
        if session:
            session.append_line(f"[workspace] {msg}")

    # Clone if needed
    if not (repo_dir / ".git").exists():
        _log(f"Cloning {project_path} ...")
        repo_dir.mkdir(parents=True, exist_ok=True)
        await _run_git(["git", "clone", clone_url, str(repo_dir)], cwd=repos_dir, env=git_env)
        _log("Clone complete.")
    else:
        _log("Fetching latest changes ...")
        await _run_git(["git", "fetch", "origin"], cwd=repo_dir, env=git_env)
        _log("Fetch complete.")

    # Clean up stale worktree if it exists
    if worktree_dir.exists():
        _log(f"Removing stale worktree {worktree_dir.name} ...")
        await _run_git(["git", "worktree", "remove", "--force", str(worktree_dir)], cwd=repo_dir, env=git_env)

    # Create worktree on the existing remote branch
    _log(f"Creating worktree on branch {branch_name} ...")
    worktree_dir.parent.mkdir(parents=True, exist_ok=True)
    await _run_git(
        ["git", "worktree", "add", str(worktree_dir), f"origin/{branch_name}"],
        cwd=repo_dir,
        env=git_env,
    )
    # Set up local branch tracking the remote so pushes work
    await _run_git(
        ["git", "checkout", "-B", branch_name, f"origin/{branch_name}"],
        cwd=worktree_dir,
        env=git_env,
    )
    _log(f"Worktree ready at {worktree_dir} (branch: {branch_name})")

    return worktree_dir


def build_review_prompt(
    prompt_file: str,
    project_id: int,
    mr_iid: int,
    mr_title: str,
    reviewer_name: str,
    review_comment: str,
    source_branch: str,
) -> str:
    """Read the review prompt template and interpolate MR details."""
    template = (PROMPTS_DIR / prompt_file).read_text()
    return template.format(
        project_id=project_id,
        mr_iid=mr_iid,
        mr_title=mr_title,
        reviewer_name=reviewer_name,
        review_comment=review_comment,
        source_branch=source_branch,
    )


async def handle_mr_note_webhook(note_info: dict, settings: Settings) -> None:
    """Respond to a reviewer comment on a Merge Request."""
    try:
        project_id = note_info["project_id"]
        mr_iid = note_info["mr_iid"]
        mr_title = note_info["mr_title"]
        source_branch = note_info["source_branch"]
        project_path = note_info["project_path"]
        reviewer_name = note_info["author_name"]
        review_comment = note_info["note"]

        model = "sonnet"

        session = session_manager.create_session(
            issue_iid=mr_iid,
            issue_title=f"MR Review: {mr_title}",
            project_id=project_id,
            model=model,
        )
        session._restart_context = {
            "type": "mr_note",
            "note_info": note_info,
        }
        session.append_line(f"[workflow] MR note review triggered")
        session.append_line(f"[workflow] MR !{mr_iid}: {mr_title}")
        session.append_line(f"[workflow] Reviewer: {reviewer_name}")
        session.append_line(f"[workflow] Comment: {review_comment[:200]}")

        prompt = build_review_prompt(
            "review.md",
            project_id=project_id,
            mr_iid=mr_iid,
            mr_title=mr_title,
            reviewer_name=reviewer_name,
            review_comment=review_comment,
            source_branch=source_branch,
        )
        logger.info(
            "Built review prompt (%d chars) for MR !%d [model=%s]",
            len(prompt), mr_iid, model,
        )

        # Prepare worktree on the MR's source branch
        work_cwd: str | None = None
        if project_path and source_branch:
            worktree_dir = await prepare_review_worktree(
                project_path=project_path,
                branch_name=source_branch,
                settings=settings,
                session=session,
            )
            work_cwd = str(worktree_dir)
            session.append_line(f"[workspace] Branch: {source_branch}")

        result = await invoke_claude(prompt, settings, session=session, model=model, cwd=work_cwd)

        # Auto-resume if Claude hit max turns without finishing
        result = await _maybe_resume(result, settings, session, model, work_cwd)

        if "error" in result:
            logger.error("MR review failed for !%d: %s", mr_iid, result["error"])
            session.finish("error", result.get("error"))
        else:
            summary = result.get("result", json.dumps(result))[:500]
            logger.info("MR review complete for !%d: %s", mr_iid, summary)
            session.finish("success", summary)
    except Exception:
        logger.exception("Unhandled error processing MR note webhook")
        if "session" in locals():
            session.finish("error", "Unhandled exception")


async def handle_issue_webhook(payload: dict, trigger_label: str, settings: Settings) -> None:
    """Orchestrate a workflow stage based on the trigger label."""
    try:
        issue = payload["object_attributes"]
        project_id = issue["project_id"]
        issue_iid = issue["iid"]
        issue_title = issue["title"]
        issue_description = issue.get("description", "")

        stage_config = LABEL_CONFIG[trigger_label]
        prompt_file = stage_config["prompt"]
        model = stage_config["model"]

        session = session_manager.create_session(
            issue_iid=issue_iid,
            issue_title=issue_title,
            project_id=project_id,
            model=model,
        )
        session._restart_context = {
            "type": "issue",
            "payload": payload,
            "trigger_label": trigger_label,
        }
        session.append_line(f"[workflow] Stage triggered: {trigger_label}")
        session.append_line(f"[workflow] Model: {model}")
        session.append_line(f"[workflow] Using prompt: {prompt_file}")

        prompt = build_prompt(prompt_file, project_id, issue_iid, issue_title, issue_description)
        logger.info(
            "Built prompt (%d chars) for issue #%d [stage=%s, model=%s]",
            len(prompt), issue_iid, trigger_label, model,
        )

        # For stages that need the project codebase, clone/fetch the repo.
        # - Ready: clone only (read-only analysis to create tasks)
        # - Planned: clone + worktree on a feature branch (for implementation)
        work_cwd: str | None = None
        if trigger_label in ("Ready", "Planned"):
            project_path = payload.get("project", {}).get("path_with_namespace", "")
            if project_path:
                if trigger_label == "Planned":
                    worktree_dir, branch_name = await prepare_repo_worktree(
                        project_path=project_path,
                        issue_iid=issue_iid,
                        issue_title=issue_title,
                        settings=settings,
                        session=session,
                    )
                    work_cwd = str(worktree_dir)
                    session.append_line(f"[workspace] Branch: {branch_name}")
                else:
                    # Ready stage: just clone/fetch so Claude can read the codebase
                    work_cwd = str(await prepare_repo_clone(
                        project_path=project_path,
                        settings=settings,
                        session=session,
                    ))

        result = await invoke_claude(prompt, settings, session=session, model=model, cwd=work_cwd)

        # Auto-resume if Claude hit max turns without finishing
        result = await _maybe_resume(result, settings, session, model, work_cwd)

        if "error" in result:
            logger.error(
                "Stage %s failed for issue #%d: %s",
                trigger_label, issue_iid, result["error"],
            )
            session.finish("error", result.get("error"))
        else:
            summary = result.get("result", json.dumps(result))[:500]
            logger.info(
                "Stage %s complete for issue #%d: %s",
                trigger_label, issue_iid, summary,
            )
            session.finish("success", summary)
    except Exception:
        logger.exception("Unhandled error processing issue webhook [stage=%s]", trigger_label)
        if "session" in locals():
            session.finish("error", "Unhandled exception")
