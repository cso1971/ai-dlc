#!/usr/bin/env npx tsx
import "dotenv/config";
import "zx/globals";

// ---------------------------------------------------------------------------
// Config
// ---------------------------------------------------------------------------
const GITLAB_TOKEN = process.env.GITLAB_TOKEN;
const GITLAB_INTERNAL_URL =
	process.env.GITLAB_INTERNAL_URL || "http://gitlab:80";
const N8N_URL = process.env.N8N_URL || "http://localhost:5678";
const N8N_API_KEY = process.env.N8N_API_KEY;
const LLM_BASE_URL = process.env.LLM_BASE_URL || "https://api.openai.com/v1";
const LLM_API_KEY = process.env.LLM_API_KEY || "";
const LLM_MODEL = process.env.LLM_MODEL || "gpt-4o";
const CHAT_WEBHOOK_URL = process.env.CHAT_WEBHOOK_URL;

if (!GITLAB_TOKEN) {
	console.error("GITLAB_TOKEN is required. Set it in .env.");
	process.exit(1);
}

if (!N8N_API_KEY) {
	console.error(
		"N8N_API_KEY is required. Generate one in n8n → Settings → API.",
	);
	process.exit(1);
}

if (!CHAT_WEBHOOK_URL) {
	console.error(
		"CHAT_WEBHOOK_URL is required (e.g. Slack incoming webhook URL).",
	);
	process.exit(1);
}

const force = process.argv.includes("--force");

// ---------------------------------------------------------------------------
// n8n API helpers
// ---------------------------------------------------------------------------
const n8nHeaders: Record<string, string> = {
	"X-N8N-API-KEY": N8N_API_KEY,
	"Content-Type": "application/json",
};

const api = (path: string) => `${N8N_URL}/api/v1${path}`;

async function n8nGet<T = unknown>(path: string): Promise<T> {
	const res = await fetch(api(path), { headers: n8nHeaders });
	if (!res.ok) {
		const text = await res.text();
		throw new Error(`GET ${path} failed (${res.status}): ${text}`);
	}
	return res.json() as Promise<T>;
}

async function n8nPost<T = unknown>(path: string, body: unknown): Promise<T> {
	const res = await fetch(api(path), {
		method: "POST",
		headers: n8nHeaders,
		body: JSON.stringify(body),
	});
	if (!res.ok) {
		const text = await res.text();
		throw new Error(`POST ${path} failed (${res.status}): ${text}`);
	}
	return res.json() as Promise<T>;
}

async function n8nDelete(path: string): Promise<void> {
	const res = await fetch(api(path), {
		method: "DELETE",
		headers: n8nHeaders,
	});
	if (!res.ok) {
		const text = await res.text();
		throw new Error(`DELETE ${path} failed (${res.status}): ${text}`);
	}
}

async function n8nPut<T = unknown>(path: string, body: unknown): Promise<T> {
	const res = await fetch(api(path), {
		method: "PUT",
		headers: n8nHeaders,
		body: JSON.stringify(body),
	});
	if (!res.ok) {
		const text = await res.text();
		throw new Error(`PUT ${path} failed (${res.status}): ${text}`);
	}
	return res.json() as Promise<T>;
}

// ---------------------------------------------------------------------------
// 1. Wait for n8n to be ready
// ---------------------------------------------------------------------------
console.log("Waiting for n8n API...");
const MAX_RETRIES = 10;
for (let i = 0; i < MAX_RETRIES; i++) {
	try {
		await n8nGet("/workflows");
		console.log("n8n API is ready.");
		break;
	} catch {
		if (i === MAX_RETRIES - 1) {
			console.error("n8n did not become ready in time.");
			process.exit(1);
		}
		await sleep(3000);
	}
}

// ---------------------------------------------------------------------------
// 2. Create OpenAI-compatible API credentials
// ---------------------------------------------------------------------------
console.log("Creating OpenAI-compatible API credentials...");

interface N8nCredential {
	id: string;
	name: string;
	type: string;
}

let credentialId: string;
try {
	const cred = await n8nPost<N8nCredential>("/credentials", {
		name: "OpenAI Compatible API",
		type: "openAiCompatibleApi",
		data: {
			baseUrl: LLM_BASE_URL,
			apiKey: LLM_API_KEY,
			model: LLM_MODEL,
		},
	});
	credentialId = cred.id;
	console.log(`  Created credential (id: ${credentialId})`);
} catch (err: any) {
	console.warn(`  Could not create credential: ${err.message}`);
	console.log("  Searching for existing credential...");
	const existing = await n8nGet<{ data: N8nCredential[] }>("/credentials");
	const match = existing.data.find((c) => c.type === "openAiCompatibleApi");
	if (match) {
		credentialId = match.id;
		console.log(`  Reusing existing credential (id: ${credentialId})`);
	} else {
		throw err;
	}
}

// ---------------------------------------------------------------------------
// 3. Check for existing workflow
// ---------------------------------------------------------------------------
const WORKFLOW_NAME = "Refinement Reasoner → Chat";

console.log(`Checking for existing workflow "${WORKFLOW_NAME}"...`);

interface N8nWorkflow {
	id: string;
	name: string;
	active: boolean;
}

const allWorkflows = await n8nGet<{ data: N8nWorkflow[] }>("/workflows");
const existingWf = allWorkflows.data.find((w) => w.name === WORKFLOW_NAME);

if (existingWf) {
	if (force) {
		console.log(`  Deleting existing workflow (id: ${existingWf.id})...`);
		await n8nDelete(`/workflows/${existingWf.id}`);
		console.log("  Deleted.");
	} else {
		console.log(
			`  Workflow already exists (id: ${existingWf.id}), skipping. Use --force to recreate.`,
		);
		console.log(`Workflow: ${N8N_URL}/workflow/${existingWf.id}`);
		process.exit(0);
	}
}

// ---------------------------------------------------------------------------
// 4. Create the workflow
// ---------------------------------------------------------------------------
console.log("Creating workflow...");

const workflowPayload = {
	name: WORKFLOW_NAME,
	nodes: [
		{
			parameters: {
				gitlabUrl: GITLAB_INTERNAL_URL,
				gitlabToken: GITLAB_TOKEN,
			},
			id: "d13d3e4b-a001-4000-8000-000000000001",
			name: "Refinement Reasoner",
			type: "CUSTOM.refinementReasoner",
			typeVersion: 1,
			position: [0, 0],
			credentials: {
				openAiCompatibleApi: {
					id: credentialId,
					name: "OpenAI Compatible API",
				},
			},
		},
		{
			parameters: {
				operation: "sendAndWait",
				message: `={{ "🤖 *Refinement Coach* — " + $json.issueTitle + "\\n\\n" + $json.aiResponse }}`,
				options: {},
			},
			id: "d13d3e4b-a002-4000-8000-000000000002",
			name: "Chat",
			type: "@n8n/n8n-nodes-langchain.chat",
			typeVersion: 1.2,
			position: [220, 0],
		},
	],
	connections: {
		"Refinement Reasoner": {
			main: [
				[
					{
						node: "Chat",
						type: "main",
						index: 0,
					},
				],
			],
		},
	},
	settings: {
		executionOrder: "v1",
	},
};

const workflow = await n8nPost<N8nWorkflow>("/workflows", workflowPayload);
console.log(`  Workflow created (id: ${workflow.id})`);

// ---------------------------------------------------------------------------
// 5. Activate the workflow
// ---------------------------------------------------------------------------
console.log("Activating workflow...");
await n8nPost(`/workflows/${workflow.id}/activate`, {});
console.log("  Workflow activated.");

// ---------------------------------------------------------------------------
// Summary
// ---------------------------------------------------------------------------
console.log("\n=== n8n Workflow Setup Complete ===");
console.log(`Workflow: ${N8N_URL}/workflow/${workflow.id}`);
console.log(`Webhook:  ${N8N_URL}/webhook/refinement`);
console.log(`Chat:     ${CHAT_WEBHOOK_URL}`);
console.log("\nFlow:");
console.log("  GitLab issue webhook → Refinement Reasoner (AI) → Chat message");
console.log("\nNext steps:");
console.log("  1. Open the n8n UI to verify the workflow");
console.log('  2. Create a GitLab issue with "refinement" in the title');
console.log("  3. Check your chat channel for the AI-generated questions");
