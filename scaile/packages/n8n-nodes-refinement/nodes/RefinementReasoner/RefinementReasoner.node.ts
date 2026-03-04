import type {
	INodeType,
	INodeTypeDescription,
	IWebhookFunctions,
	IWebhookResponseData,
} from "n8n-workflow";

const SYSTEM_PROMPT = `You are a refinement coach for a software development team. When presented with a user story or issue, you ask clarifying questions to help the team refine it. Focus on:

1. **Scope**: Is the scope clear? Are there hidden assumptions?
2. **Acceptance Criteria**: What does "done" look like? Are criteria testable?
3. **Dependencies**: Are there blockers, upstream/downstream dependencies, or integration points?
4. **Risks**: What could go wrong? Are there technical unknowns?
5. **Estimation**: Is this small enough to estimate? Should it be split?

Respond with a numbered list of 3-7 concise, actionable questions. Do not restate the issue — go straight to the questions.`;

interface GitLabWebhookPayload {
	event_type?: string;
	object_kind?: string;
	object_attributes?: {
		title?: string;
		description?: string;
		iid?: number;
		action?: string;
		project_id?: number;
	};
}

export class RefinementReasoner implements INodeType {
	description: INodeTypeDescription = {
		displayName: "Refinement Reasoner",
		name: "refinementReasoner",
		icon: "file:refinementReasoner.svg",
		group: ["trigger"],
		version: 1,
		subtitle: "AI refinement coach for GitLab issues",
		description:
			"Listens for GitLab issue creation webhooks and posts AI-generated refinement questions as comments.",
		defaults: {
			name: "Refinement Reasoner",
		},
		inputs: [],
		outputs: ["main"],
		credentials: [
			{
				name: "openAiCompatibleApi",
				required: true,
			},
		],
		webhooks: [
			{
				name: "default",
				httpMethod: "POST",
				responseMode: "onReceived",
				path: "refinement",
			},
		],
		properties: [
			{
				displayName: "GitLab URL",
				name: "gitlabUrl",
				type: "string",
				default: "http://localhost:8090",
				description: "GitLab instance URL (internal Docker network URL)",
			},
			{
				displayName: "GitLab Token",
				name: "gitlabToken",
				type: "string",
				typeOptions: { password: true },
				default: "",
				description: "GitLab personal access token for posting comments",
			},
		],
	};

	async webhook(this: IWebhookFunctions): Promise<IWebhookResponseData> {
		const req = this.getRequestObject();
		const body = req.body as GitLabWebhookPayload;

		// ------------------------------------------------------------------
		// Filter: only issue creation events with "refinement" in title
		// ------------------------------------------------------------------
		const eventType = body.event_type || body.object_kind;
		const attrs = body.object_attributes;

		const isIssueCreate =
			eventType === "issue" && attrs?.action === "open";

		if (!isIssueCreate) {
			return { webhookResponse: "Ignored: not an issue creation event" };
		}

		const title = attrs?.title ?? "";
		if (!/refinement/i.test(title)) {
			return {
				webhookResponse: "Ignored: title does not contain 'refinement'",
			};
		}

		const issueIid = attrs?.iid;
		const projectId = attrs?.project_id;
		const description = attrs?.description ?? "";

		// ------------------------------------------------------------------
		// Call OpenAI-compatible chat completions
		// ------------------------------------------------------------------
		const credentials = await this.getCredentials("openAiCompatibleApi");
		const baseUrl = (credentials.baseUrl as string).replace(/\/+$/, "");
		const apiKey = credentials.apiKey as string;
		const model = credentials.model as string;

		const llmHeaders: Record<string, string> = {
			"Content-Type": "application/json",
		};
		if (apiKey) {
			llmHeaders["Authorization"] = `Bearer ${apiKey}`;
		}

		const chatRes = await fetch(`${baseUrl}/chat/completions`, {
			method: "POST",
			headers: llmHeaders,
			body: JSON.stringify({
				model,
				messages: [
					{ role: "system", content: SYSTEM_PROMPT },
					{
						role: "user",
						content: `Issue title: ${title}\n\nDescription:\n${description}`,
					},
				],
				temperature: 0.4,
			}),
		});

		if (!chatRes.ok) {
			const errText = await chatRes.text();
			throw new Error(`LLM API error (${chatRes.status}): ${errText}`);
		}

		const chatData = (await chatRes.json()) as {
			choices: { message: { content: string } }[];
		};
		const aiMessage =
			chatData.choices[0]?.message?.content ?? "No response from AI.";

		// ------------------------------------------------------------------
		// Post comment on the issue
		// ------------------------------------------------------------------
		const gitlabUrl = (this.getNodeParameter("gitlabUrl", 0) as string).replace(
			/\/+$/,
			"",
		);
		const gitlabToken = this.getNodeParameter("gitlabToken", 0) as string;

		if (projectId && issueIid && gitlabToken) {
			const noteRes = await fetch(
				`${gitlabUrl}/api/v4/projects/${projectId}/issues/${issueIid}/notes`,
				{
					method: "POST",
					headers: {
						"PRIVATE-TOKEN": gitlabToken,
						"Content-Type": "application/json",
					},
					body: JSON.stringify({
						body: `**Refinement Coach** :robot:\n\n${aiMessage}`,
					}),
				},
			);

			if (!noteRes.ok) {
				const errText = await noteRes.text();
				throw new Error(
					`GitLab note API error (${noteRes.status}): ${errText}`,
				);
			}
		}

		return {
			workflowData: [
				this.helpers.returnJsonArray({
					issueTitle: title,
					issueIid,
					projectId,
					aiResponse: aiMessage,
				}),
			],
		};
	}
}
