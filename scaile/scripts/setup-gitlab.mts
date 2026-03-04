#!/usr/bin/env npx tsx
import "dotenv/config";
import "zx/globals";
import { readFileSync, writeFileSync } from "fs";
import { join } from "path";

// ---------------------------------------------------------------------------
// Config
// ---------------------------------------------------------------------------
const GITLAB_URL = process.env.GITLAB_URL || "http://localhost:8090";
const GITLAB_TOKEN = process.env.GITLAB_TOKEN;

if (!GITLAB_TOKEN) {
	console.error("GITLAB_TOKEN is required. Set it in .env after first login.");
	process.exit(1);
}

const headers = {
	"PRIVATE-TOKEN": GITLAB_TOKEN,
	"Content-Type": "application/json",
};

const FORCE = process.argv.includes("--force");

const api = (path: string) => `${GITLAB_URL}/api/v4${path}`;

const DISTRIBUTED_PLAYGROUND_DIR = join(import.meta.dirname, "..", "..", "distributed-playground");
const PROJECT_NAME = "distributed-playground";

// ---------------------------------------------------------------------------
// Helpers
// ---------------------------------------------------------------------------
async function gitlabPost<T = unknown>(
	path: string,
	body: Record<string, unknown>,
): Promise<T> {
	const res = await fetch(api(path), {
		method: "POST",
		headers,
		body: JSON.stringify(body),
	});
	if (!res.ok) {
		const text = await res.text();
		throw new Error(`POST ${path} failed (${res.status}): ${text}`);
	}
	return res.json() as Promise<T>;
}

async function gitlabPut<T = unknown>(
	path: string,
	body: Record<string, unknown>,
): Promise<T> {
	const res = await fetch(api(path), {
		method: "PUT",
		headers,
		body: JSON.stringify(body),
	});
	if (!res.ok) {
		const text = await res.text();
		throw new Error(`PUT ${path} failed (${res.status}): ${text}`);
	}
	return res.json() as Promise<T>;
}

async function gitlabDelete(path: string): Promise<void> {
	const res = await fetch(api(path), { method: "DELETE", headers });
	if (!res.ok && res.status !== 404) {
		const text = await res.text();
		throw new Error(`DELETE ${path} failed (${res.status}): ${text}`);
	}
}

async function gitlabGet<T = unknown>(path: string): Promise<T> {
	console.log(`[GET] ${api(path)}`);
	const res = await fetch(api(path), { headers });
	if (!res.ok) {
		const text = await res.text();
		console.error(`Error response from GitLab API: ${text}`);
		throw new Error(`GET ${path} failed (${res.status}): ${text}`);
	}
	return res.json() as Promise<T>;
}

// ---------------------------------------------------------------------------
// 1. Wait for GitLab to be ready
// ---------------------------------------------------------------------------
console.log("Waiting for GitLab API...");
const MAX_RETRIES = 5;
for (let i = 0; i < MAX_RETRIES; i++) {
	try {
		const version = await gitlabGet<{ version: string }>("/version");
		console.log(`GitLab ${version.version} is ready.`);
		break;
	} catch {
		if (i === MAX_RETRIES - 1) {
			console.error("GitLab did not become ready in time.");
			process.exit(1);
		}
		await sleep(5000);
	}
}

// ---------------------------------------------------------------------------
// 2. Create group
// ---------------------------------------------------------------------------
console.log("Creating group: scaile");
let group: { id: number; web_url: string };
try {
	group = await gitlabPost("/groups", {
		name: "Scaile",
		path: "scaile",
		visibility: "internal",
	});
} catch (err: any) {
	if (err.message.includes("has already been taken")) {
		const groups = await gitlabGet<{ id: number; web_url: string }[]>(
			"/groups?search=scaile",
		);
		group = groups[0];
		console.log("Group already exists, reusing.");
	} else {
		throw err;
	}
}

// ---------------------------------------------------------------------------
// 3. Allow local network requests from webhooks (needed for Docker networking)
// ---------------------------------------------------------------------------
console.log("Allowing local requests from webhooks...");
try {
	await gitlabPut("/application/settings", {
		allow_local_requests_from_web_hooks_and_services: true,
	});
	console.log("  Local requests allowed.");
} catch (err: any) {
	console.warn(`  Could not update setting: ${err.message}`);
}

// ---------------------------------------------------------------------------
// 4. Create distributed-playground project and push code via git
// ---------------------------------------------------------------------------
if (FORCE) {
	console.log(`--force: deleting existing ${PROJECT_NAME} project...`);
	try {
		const projects = await gitlabGet<{ id: number }[]>(
			`/groups/${group.id}/projects?search=${PROJECT_NAME}`,
		);
		for (const p of projects) {
			await gitlabDelete(`/projects/${p.id}`);
			console.log(`  Deleted project id=${p.id}`);
		}
		// Wait for GitLab to finish the async deletion
		await sleep(3000);
	} catch (err: any) {
		console.warn(`  Could not delete project: ${err.message}`);
	}
}

console.log(`Creating project: ${PROJECT_NAME}`);
let sampleProject: { id: number; web_url: string };
try {
	sampleProject = await gitlabPost(`/projects`, {
		name: PROJECT_NAME,
		namespace_id: group.id,
		visibility: "internal",
		initialize_with_readme: false,
	});
} catch (err: any) {
	if (err.message.includes("has already been taken")) {
		const projects = await gitlabGet<{ id: number; web_url: string }[]>(
			`/groups/${group.id}/projects?search=${PROJECT_NAME}`,
		);
		sampleProject = projects[0];
		console.log("Project already exists, reusing.");
	} else {
		throw err;
	}
}

// Push distributed-playground code via git
console.log(`Pushing ${PROJECT_NAME} code via git...`);

// Build the GitLab push URL with root credentials
const gitlabPushUrl = `${GITLAB_URL.replace("://", `://root:${process.env.GITLAB_ROOT_PASSWORD || "changeme"}@`)}/scaile/${PROJECT_NAME}.git`;

try {
	// Initialize a temporary git repo from the distributed-playground folder and push to GitLab
	const tmpDir = join(import.meta.dirname, "..", ".tmp-git-push");
	await $`rm -rf ${tmpDir}`;
	await $`cp -r ${DISTRIBUTED_PLAYGROUND_DIR} ${tmpDir}`;

	// Init a fresh git repo and push all files
	await $`cd ${tmpDir} && git init -b main && git add -A && git commit -m "Initial commit: Distributed Playground (.NET microservices + AI)"`;
	await $`cd ${tmpDir} && git remote add gitlab ${gitlabPushUrl} && git push -u gitlab main --force`;

	// Cleanup
	await $`rm -rf ${tmpDir}`;
	console.log("  Code pushed successfully.");
} catch (err: any) {
	// Cleanup on error too
	const tmpDir = join(import.meta.dirname, "..", ".tmp-git-push");
	await $`rm -rf ${tmpDir}`.catch(() => {});

	if (err.message.includes("already exists") || err.message.includes("up-to-date")) {
		console.log("  Code already up-to-date, skipping push.");
	} else {
		console.warn(`  Warning pushing code: ${err.message}`);
	}
}

// ---------------------------------------------------------------------------
// 5. Create labels for the workflow stages
// ---------------------------------------------------------------------------
const workflowLabels = [
	{ name: "Requirements", color: "#dc143c", description: "Stage 1: Epic requirements (HITL)" },
	{ name: "Breakdown", color: "#e67e22", description: "Stage 2: AI breaks epics into stories" },
	{ name: "Refinement", color: "#f39c12", description: "Stage 3: Review stories (HITL)" },
	{ name: "Ready", color: "#27ae60", description: "Stage 4: AI creates tasks from stories" },
	{ name: "Planned", color: "#2980b9", description: "Stage 5: AI develops code and opens PR" },
	{ name: "Review", color: "#9b59b6", description: "Stage 6: Review and approve PR (HITL)" },
	{ name: "Test", color: "#1abc9c", description: "Stage 7: Test in staging (HITL)" },
	{ name: "Done", color: "#69d100", description: "Stage 8: Completed" },
];

console.log(`Creating workflow labels on ${PROJECT_NAME}...`);
for (const label of workflowLabels) {
	try {
		await gitlabPost(`/projects/${sampleProject.id}/labels`, label);
		console.log(`  Created label: ${label.name}`);
	} catch (err: any) {
		if (err.message.includes("already exists")) {
			console.log(`  Label already exists: ${label.name}`);
		} else {
			throw err;
		}
	}
}

// Also create issue type labels (epic, story, task)
const typeLabels = [
	{ name: "epic", color: "#6c5ce7", description: "Issue type: Epic (high-level requirement)" },
	{ name: "story", color: "#0984e3", description: "Issue type: User story" },
	{ name: "task", color: "#00b894", description: "Issue type: Development task" },
];

console.log("Creating issue type labels...");
for (const label of typeLabels) {
	try {
		await gitlabPost(`/projects/${sampleProject.id}/labels`, label);
		console.log(`  Created label: ${label.name}`);
	} catch (err: any) {
		if (err.message.includes("already exists")) {
			console.log(`  Label already exists: ${label.name}`);
		} else {
			throw err;
		}
	}
}

// ---------------------------------------------------------------------------
// 6. Create issue board with workflow lanes
// ---------------------------------------------------------------------------
console.log("Creating workflow board...");
let board: { id: number };
try {
	board = await gitlabPost(`/projects/${sampleProject.id}/boards`, {
		name: "Workflow Board",
	});
} catch (err: any) {
	if (err.message.includes("already exists")) {
		const boards = await gitlabGet<{ id: number; name: string }[]>(
			`/projects/${sampleProject.id}/boards`,
		);
		board = boards.find((b) => b.name === "Workflow Board") || boards[0];
		console.log("Board already exists, reusing.");
	} else {
		throw err;
	}
}

// Fetch labels to get their IDs
const existingLabels = await gitlabGet<{ id: number; name: string }[]>(
	`/projects/${sampleProject.id}/labels`,
);

// Create board lists in stage order
const boardLaneOrder = [
	"Requirements",
	"Breakdown",
	"Refinement",
	"Ready",
	"Planned",
	"Review",
	"Test",
	"Done",
];

for (const labelName of boardLaneOrder) {
	const label = existingLabels.find((l) => l.name === labelName);
	if (!label) continue;
	try {
		await gitlabPost(
			`/projects/${sampleProject.id}/boards/${board.id}/lists`,
			{
				label_id: label.id,
			},
		);
		console.log(`  Created board list: ${labelName}`);
	} catch (err: any) {
		if (err.message.includes("already exists")) {
			console.log(`  Board list already exists: ${labelName}`);
		} else {
			console.warn(
				`  Warning creating board list ${labelName}: ${err.message}`,
			);
		}
	}
}

// ---------------------------------------------------------------------------
// 7. Register webhooks
//    First clean up any stale webhooks, then register the current one.
// ---------------------------------------------------------------------------
console.log(`Registering webhooks on ${PROJECT_NAME}...`);
const desiredWebhookUrl = "http://webhook:8000/webhook/gitlab";

// Remove all existing webhooks (cleans up stale /webhook/breakdown, n8n, duplicates, etc.)
try {
	const existingHooks = await gitlabGet<{ id: number; url: string }[]>(
		`/projects/${sampleProject.id}/hooks`,
	);
	for (const hook of existingHooks) {
		try {
			const res = await fetch(
				api(`/projects/${sampleProject.id}/hooks/${hook.id}`),
				{ method: "DELETE", headers },
			);
			if (res.ok) {
				console.log(`  Removed old webhook: ${hook.url}`);
			}
		} catch {
			// ignore deletion errors
		}
	}
} catch {
	console.log("  No existing webhooks to clean up.");
}

// Register the current webhook
try {
	await gitlabPost(`/projects/${sampleProject.id}/hooks`, {
		url: desiredWebhookUrl,
		push_events: false,
		issues_events: true,
		merge_requests_events: true,
		confidential_issues_events: false,
		note_events: true,
		enable_ssl_verification: false,
	});
	console.log(`  Webhook registered: ${desiredWebhookUrl}`);
} catch (err: any) {
	console.warn(`  Webhook registration failed: ${err.message}`);
}

// ---------------------------------------------------------------------------
// 8. Register a GitLab Runner for CI/CD pipelines
// ---------------------------------------------------------------------------
console.log("Registering GitLab Runner...");

// Create a project runner via the API to get an authentication token
let runnerToken: string | undefined;
try {
	const runner = await gitlabPost<{ token: string; id: number }>(
		"/user/runners",
		{
			runner_type: "project_type",
			project_id: sampleProject.id,
			description: "docker-runner",
			tag_list: ["docker"],
			run_untagged: true,
		},
	);
	runnerToken = runner.token;
	console.log(`  Runner created (id=${runner.id}), registering...`);
} catch (err: any) {
	console.warn(`  Could not create runner via API: ${err.message}`);
}

if (runnerToken) {
	try {
		await $`docker compose exec -T gitlab-runner gitlab-runner register \
			--non-interactive \
			--url http://gitlab:80 \
			--clone-url http://gitlab:80 \
			--token ${runnerToken} \
			--executor docker \
			--docker-image mcr.microsoft.com/dotnet/sdk:9.0 \
			--docker-network-mode scaile_devops \
			--docker-pull-policy if-not-present`;
		console.log("  Runner registered successfully.");

		// Restart the runner so it picks up the new config
		await $`docker compose restart gitlab-runner`;
		console.log("  Runner restarted.");
	} catch (err: any) {
		console.warn(`  Runner registration failed: ${err.message}`);
	}
}

// ---------------------------------------------------------------------------
// 9. Create a project bot access token for the webhook server
// ---------------------------------------------------------------------------
console.log("Creating project bot access token...");

// Remove existing bot tokens to avoid duplicates on re-runs
let botToken: string | undefined;
try {
	const existingTokens = await gitlabGet<{ id: number; name: string }[]>(
		`/projects/${sampleProject.id}/access_tokens`,
	);
	for (const t of existingTokens) {
		if (t.name === "workflow-bot") {
			await gitlabDelete(`/projects/${sampleProject.id}/access_tokens/${t.id}`);
			console.log("  Removed existing workflow-bot token.");
		}
	}
} catch {
	// ignore
}

try {
	// Expiry date: 1 year from now
	const expiry = new Date();
	expiry.setFullYear(expiry.getFullYear() + 1);
	const expiresAt = expiry.toISOString().split("T")[0]; // YYYY-MM-DD

	const token = await gitlabPost<{ token: string; id: number }>(
		`/projects/${sampleProject.id}/access_tokens`,
		{
			name: "workflow-bot",
			scopes: ["api", "read_repository", "write_repository"],
			access_level: 40, // Maintainer
			expires_at: expiresAt,
		},
	);
	botToken = token.token;
	console.log(`  Bot token created (id=${token.id}).`);

	// Write the bot token to .env so the webhook server can use it
	const envPath = join(import.meta.dirname, "..", ".env");
	let envContent = "";
	try {
		envContent = readFileSync(envPath, "utf-8");
	} catch {
		// .env doesn't exist yet
	}

	// Update or append GITLAB_BOT_TOKEN
	if (envContent.includes("GITLAB_BOT_TOKEN=")) {
		envContent = envContent.replace(/GITLAB_BOT_TOKEN=.*/g, `GITLAB_BOT_TOKEN=${botToken}`);
	} else {
		envContent = envContent.trimEnd() + `\nGITLAB_BOT_TOKEN=${botToken}\n`;
	}
	writeFileSync(envPath, envContent);
	console.log("  GITLAB_BOT_TOKEN written to .env");
} catch (err: any) {
	console.warn(`  Could not create bot token: ${err.message}`);
}

// ---------------------------------------------------------------------------
// Summary
// ---------------------------------------------------------------------------
console.log("\n=== Setup Complete ===");
console.log(`Group:            ${group.web_url}`);
console.log(`Project:          ${sampleProject.web_url}`);
console.log(`Workflow Board:   ${sampleProject.web_url}/-/boards/${board.id}`);
console.log(`Runner:           ${runnerToken ? "registered (docker executor)" : "not registered"}`);
console.log(`Bot token:        ${botToken ? "created (workflow-bot)" : "not created"}`);
console.log("\nWorkflow stages:");
boardLaneOrder.forEach((s, i) => console.log(`  ${i + 1}. ${s}`));
console.log("\nNext steps:");
console.log("  1. docker compose ps  — verify all services are healthy");
console.log(
	"  2. Open http://localhost:8090 — GitLab (root / $GITLAB_ROOT_PASSWORD)",
);
console.log(
	'  3. Create an epic issue with "Requirements" label to start the workflow',
);
