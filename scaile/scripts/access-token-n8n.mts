#!/usr/bin/env npx tsx
import "dotenv/config";
import "zx/globals";

// ---------------------------------------------------------------------------
// Config
// ---------------------------------------------------------------------------
const N8N_URL = process.env.N8N_URL || "http://localhost:5678";
const N8N_API_KEY = process.env.N8N_API_KEY;
const force = process.argv.includes("--force");

if (N8N_API_KEY && !force) {
	console.log(
		`N8N_API_KEY is already set (${N8N_API_KEY.slice(0, 10)}...). Skipping. Use --force to regenerate.`,
	);
	process.exit(0);
}

const OWNER_EMAIL = process.env.N8N_OWNER_EMAIL;
const OWNER_PASSWORD = process.env.N8N_OWNER_PASSWORD;
if (!OWNER_EMAIL || !OWNER_PASSWORD) {
	console.error("N8N_OWNER_EMAIL and N8N_OWNER_PASSWORD must be set in .env");
	process.exit(1);
}
const OWNER_FIRST_NAME = "Admin";
const OWNER_LAST_NAME = "Setup";

// ---------------------------------------------------------------------------
// helpers
// ---------------------------------------------------------------------------
type JSONValue = string | number | boolean | null | JSONValue[] | { [k: string]: JSONValue };

async function rest(
	method: string,
	path: string,
	opts: { body?: JSONValue; cookie?: string } = {},
) {
	const headers: Record<string, string> = { "Content-Type": "application/json" };
	if (opts.cookie) headers.Cookie = opts.cookie;
	return fetch(`${N8N_URL}/rest${path}`, {
		method,
		headers,
		...(opts.body !== undefined && { body: JSON.stringify(opts.body) }),
	});
}

// ---------------------------------------------------------------------------
// 1. Wait for n8n to be ready via health check
// ---------------------------------------------------------------------------
console.log("Waiting for n8n to be ready...");
const MAX_RETRIES = 30;
for (let i = 0; i < MAX_RETRIES; i++) {
	try {
		const res = await fetch(`${N8N_URL}/healthz`);
		if (res.ok) {
			console.log("n8n is ready.");
			break;
		}
	} catch {
		// not ready yet
	}
	if (i === MAX_RETRIES - 1) {
		console.error("n8n did not become ready in time.");
		process.exit(1);
	}
	console.log(`  Retrying in 5s... (${i + 1}/${MAX_RETRIES})`);
	await sleep(5000);
}

// ---------------------------------------------------------------------------
// 2. Setup owner account (idempotent – harmless if already done)
// ---------------------------------------------------------------------------
console.log("Setting up owner account...");
const setupRes = await rest("POST", "/owner/setup", {
	body: {
		email: OWNER_EMAIL,
		firstName: OWNER_FIRST_NAME,
		lastName: OWNER_LAST_NAME,
		password: OWNER_PASSWORD,
	},
});
if (setupRes.ok) {
	console.log("  Owner account created.");
} else {
	console.log("  Owner already exists, continuing...");
}

// ---------------------------------------------------------------------------
// 3. Login to obtain session cookie
// ---------------------------------------------------------------------------
console.log("Logging in to n8n...");
const loginRes = await rest("POST", "/login", {
	body: { emailOrLdapLoginId: OWNER_EMAIL, password: OWNER_PASSWORD },
});
if (!loginRes.ok) {
	console.error("Login failed:", loginRes.status, await loginRes.text());
	process.exit(1);
}

const cookies = loginRes.headers.getSetCookie?.() ?? [];
const authCookie = cookies.find((c) => c.startsWith("n8n-auth="));
if (!authCookie) {
	console.error("No n8n-auth cookie in login response.");
	process.exit(1);
}
const cookie = authCookie.split(";")[0];

// ---------------------------------------------------------------------------
// 4. Delete existing API keys (clean slate)
// ---------------------------------------------------------------------------
const listRes = await rest("GET", "/api-keys", { cookie });
if (listRes.ok) {
	const list = (await listRes.json()) as { data?: { id: string }[] };
	for (const key of list.data ?? []) {
		console.log(`  Deleting existing API key (${key.id})...`);
		await rest("DELETE", `/api-keys/${key.id}`, { cookie });
	}
}

// ---------------------------------------------------------------------------
// 5. Fetch available scopes for owner role, then create API key
// ---------------------------------------------------------------------------
console.log("Creating API key...");

const scopesRes = await rest("GET", "/api-keys/scopes", { cookie });
let scopes: string[] = ["workflow:create", "workflow:read", "workflow:update",
	"workflow:delete", "workflow:list", "workflow:execute",
	"credential:create", "credential:read", "credential:update",
	"credential:delete", "credential:list"];
if (scopesRes.ok) {
	const scopesBody = (await scopesRes.json()) as { data?: string[] } | string[];
	const fetched = Array.isArray(scopesBody) ? scopesBody : scopesBody.data;
	if (fetched && fetched.length > 0) {
		scopes = fetched;
	}
}

const createRes = await rest("POST", "/api-keys", {
	cookie,
	body: {
		label: "setup-token",
		scopes,
		expiresAt: null,
	},
});
if (!createRes.ok) {
	console.error("Failed to create API key:", createRes.status, await createRes.text());
	process.exit(1);
}

const body = (await createRes.json()) as {
	data?: { rawApiKey?: string; apiKey?: string };
	rawApiKey?: string;
};
const apiKey = body.data?.rawApiKey ?? body.data?.apiKey ?? body.rawApiKey;
if (!apiKey) {
	console.error("Could not extract API key from response:", JSON.stringify(body));
	process.exit(1);
}

// ---------------------------------------------------------------------------
// 6. Output
// ---------------------------------------------------------------------------
console.log(apiKey);
