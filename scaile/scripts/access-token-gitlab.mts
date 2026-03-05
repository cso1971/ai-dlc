#!/usr/bin/env npx tsx
import "dotenv/config";
import "zx/globals";
import { readFileSync, writeFileSync } from "fs";
import { resolve, dirname } from "path";
import { fileURLToPath } from "url";

// ---------------------------------------------------------------------------
// Config
// ---------------------------------------------------------------------------
const GITLAB_URL = process.env.GITLAB_URL || "http://localhost:8090";
const GITLAB_TOKEN = process.env.GITLAB_TOKEN;
const force = process.argv.includes("--force");

if (GITLAB_TOKEN && !force) {
	console.log(
		`GITLAB_TOKEN is already set (${GITLAB_TOKEN.slice(0, 10)}...). Skipping. Use --force to regenerate.`,
	);
	process.exit(0);
}

const DOCKER_SERVICE = "gitlab";
const TOKEN_NAME = "setup-token";

// ---------------------------------------------------------------------------
// 1. Wait for GitLab to be ready via health check
// ---------------------------------------------------------------------------
console.log("Waiting for GitLab to be ready...");
const MAX_RETRIES = 30;
for (let i = 0; i < MAX_RETRIES; i++) {
	try {
		const res = await fetch(`${GITLAB_URL}/version`);
		if (res.ok) {
			console.log("GitLab is ready.");
			break;
		}
	} catch {
		// not ready yet
	}
	if (i === MAX_RETRIES - 1) {
		console.error("GitLab did not become ready in time.");
		process.exit(1);
	}
	console.log(`  Retrying in 10s... (${i + 1}/${MAX_RETRIES})`);
	await sleep(10000);
}

// ---------------------------------------------------------------------------
// 2. Generate personal access token via Rails runner (all available scopes)
// ---------------------------------------------------------------------------
console.log(`Generating personal access token "${TOKEN_NAME}"...`);

const railsScript = `
scopes = Gitlab::Auth.all_available_scopes
token = User.find_by_username('root').personal_access_tokens.create!(
  name: '${TOKEN_NAME}',
  scopes: scopes,
  expires_at: 365.days.from_now
)
puts token.token
`.trim();

const result =
	await $`docker compose exec -T ${DOCKER_SERVICE} gitlab-rails runner ${railsScript}`;

const token = result.stdout.trim();
if (!token || !token.startsWith("glpat-")) {
	console.error("Failed to extract token from Rails output:");
	console.error(result.stdout);
	console.error(result.stderr);
	process.exit(1);
}

// ---------------------------------------------------------------------------
// 3. Update .env file
// ---------------------------------------------------------------------------
const __dirname = dirname(fileURLToPath(import.meta.url));
const envPath = resolve(__dirname, "../.env");
let envContent = readFileSync(envPath, "utf-8");

if (/^GITLAB_TOKEN=.*/m.test(envContent)) {
	envContent = envContent.replace(/^GITLAB_TOKEN=.*/m, `GITLAB_TOKEN=${token}`);
} else {
	envContent += `\nGITLAB_TOKEN=${token}\n`;
}

writeFileSync(envPath, envContent);
console.log(`GITLAB_TOKEN written to .env (${token.slice(0, 10)}...)`);
