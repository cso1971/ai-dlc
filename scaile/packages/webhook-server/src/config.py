from pydantic_settings import BaseSettings


class Settings(BaseSettings):
    gitlab_token: str = ""
    gitlab_bot_token: str = ""
    gitlab_bot_username: str = ""
    gitlab_api_url: str = "http://gitlab:80/api/v4"
    anthropic_api_key: str = ""
    trigger_label: str = "Requirements"
    workspace_dir: str = "/app"
    repos_dir: str = "/home/claude/repos"
    sessions_dir: str = "/app/sessions"

    model_config = {"env_file": ".env", "extra": "ignore"}


settings = Settings()
