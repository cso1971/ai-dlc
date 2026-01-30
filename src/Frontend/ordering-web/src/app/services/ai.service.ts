import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../environments/environment';

export interface ChatRequest {
  prompt: string;
  systemPrompt?: string;
  temperature?: number;
  maxTokens?: number;
}

export interface ChatResponse {
  response: string;
  model: string;
  duration: string;
}

export interface AnalyzeRequest {
  text: string;
  analysisType?: string;
}

export interface AnalyzeResponse {
  analysis: string;
  analysisType: string;
  duration: string;
}

export interface SummarizeRequest {
  text: string;
  maxLength?: number;
}

export interface SummarizeResponse {
  summary: string;
  duration: string;
}

export interface SemanticSearchRequest {
  query: string;
  limit?: number;
}

export interface SearchResult {
  orderId: string;
  score: number;
  metadata: Record<string, any>;
}

export interface SemanticSearchResponse {
  results: SearchResult[];
  duration: string;
}

export interface ModelInfo {
  chatModel: string;
  embeddingModel: string;
  ollamaEndpoint: string;
  qdrantEndpoint: string;
  qdrantCollection: string;
}

export interface HealthStatus {
  status: string;
  response?: string;
  error?: string;
  collection?: string;
}

@Injectable({
  providedIn: 'root'
})
export class AiService {
  private apiUrl = environment.aiApiUrl;

  constructor(private http: HttpClient) {}

  chat(request: ChatRequest): Observable<ChatResponse> {
    return this.http.post<ChatResponse>(`${this.apiUrl}/api/ai/chat`, request);
  }

  analyze(request: AnalyzeRequest): Observable<AnalyzeResponse> {
    return this.http.post<AnalyzeResponse>(`${this.apiUrl}/api/ai/analyze`, request);
  }

  summarize(request: SummarizeRequest): Observable<SummarizeResponse> {
    return this.http.post<SummarizeResponse>(`${this.apiUrl}/api/ai/summarize`, request);
  }

  semanticSearch(request: SemanticSearchRequest): Observable<SemanticSearchResponse> {
    return this.http.post<SemanticSearchResponse>(`${this.apiUrl}/api/ai/search`, request);
  }

  getModelInfo(): Observable<ModelInfo> {
    return this.http.get<ModelInfo>(`${this.apiUrl}/api/ai/info`);
  }

  checkOllamaHealth(): Observable<HealthStatus> {
    return this.http.get<HealthStatus>(`${this.apiUrl}/api/ai/health/ollama`);
  }

  checkQdrantHealth(): Observable<HealthStatus> {
    return this.http.get<HealthStatus>(`${this.apiUrl}/api/ai/health/qdrant`);
  }
}
