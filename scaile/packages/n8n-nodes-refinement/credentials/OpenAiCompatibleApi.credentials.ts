import { ICredentialType, INodeProperties } from "n8n-workflow";

export class OpenAiCompatibleApi implements ICredentialType {
  name = "openAiCompatibleApi";
  displayName = "OpenAI-Compatible API";
  documentationUrl = "https://platform.openai.com/docs/api-reference";

  properties: INodeProperties[] = [
    {
      displayName: "Base URL",
      name: "baseUrl",
      type: "string",
      default: "https://api.openai.com/v1",
      description: "Base URL of the OpenAI-compatible API. For Ollama: http://ollama:11434/v1",
    },
    {
      displayName: "API Key",
      name: "apiKey",
      type: "string",
      typeOptions: { password: true },
      default: "",
      description: "API key. Leave empty for local models like Ollama.",
    },
    {
      displayName: "Model",
      name: "model",
      type: "string",
      default: "gpt-4o",
      description: "Model name. For Ollama: llama3",
    },
  ];
}
